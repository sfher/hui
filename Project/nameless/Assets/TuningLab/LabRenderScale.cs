using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全屏渲染缩放（#153）。全屏表面恒为显示器原生尺寸（#151/#154 的修复，
/// 保证 IMGUI 命中与指针坐标对齐），所选档位改走游戏侧中间纹理：
/// 主相机渲染到所选分辨率的 RenderTexture，再由一个呈现相机把这张
/// 纹理按比例贴满原生后端缓冲。IMGUI 在所有相机之后直接画到后端
/// 缓冲，始终原生分辨率、原生坐标，不受缩放影响。
///
/// 不走 URP renderScale 与 ScalableBufferManager：前者在本工程实测
/// 无效（2D 渲染器直通后端缓冲，见 #153），后者只要缩放真正生效就
/// 黑屏。中间纹理这条路完全在游戏自己手里，不依赖管线内部路径。
///
/// 窗口化、原生档位、拿不到原生尺寸的兜底分支都不启用，行为与
/// 未引入本类时完全一致；呈现搭建失败（无空闲层、材质缺失）时
/// 同样整体退回不缩放，绝不把主相机留在无处呈现的状态。
/// </summary>
public static class LabRenderScale
{
    static Presenter presenter;
    static bool sceneHookInstalled;

    /// <summary>呈现是否正在生效（主相机正渲染到中间纹理）。</summary>
    public static bool IsActive => presenter != null && presenter.Active;

    /// <summary>
    /// 按「所选档位 × 原生表面」更新缩放目标。每个场景的设置界面在
    /// Start 时都会经 LabSettingsStore.Apply 调到这里，新场景的相机
    /// 由 sceneLoaded 钩子重新绑定，档位切换直接改本方法入参即可。
    /// </summary>
    public static void SetTarget(int selectedWidth, int selectedHeight, int nativeWidth, int nativeHeight)
    {
        if (!ShouldActivate(selectedWidth, selectedHeight, nativeWidth, nativeHeight))
        {
            Disable();
            return;
        }

        EnsurePresenter();
        if (presenter == null) return;
        presenter.Apply(selectedWidth, selectedHeight);
    }

    /// <summary>关闭缩放：恢复主相机直渲后端缓冲，释放中间纹理。</summary>
    public static void Disable()
    {
        if (presenter != null) presenter.Deactivate();
    }

    /// <summary>仅当所选档位任一边小于原生时才需要缩放；参数不全一律不启用。</summary>
    public static bool ShouldActivate(int selectedWidth, int selectedHeight, int nativeWidth, int nativeHeight)
    {
        if (selectedWidth <= 0 || selectedHeight <= 0 || nativeWidth <= 0 || nativeHeight <= 0)
            return false;
        return selectedWidth < nativeWidth || selectedHeight < nativeHeight;
    }

    /// <summary>
    /// 呈现四边形在呈现相机空间里的尺寸：呈现相机正交半高恒为 1，
    /// 半宽为其宽高比（即表面宽高比）。纹理等比放大到恰好装进画面，
    /// 宽高比不一致时留黑边，不拉伸变形——与真模式切换下游戏相机
    /// 按所选宽高比取景再由显示器呈现的几何一致。
    /// </summary>
    public static Vector2 ComputeQuadSize(float textureAspect, float surfaceAspect)
    {
        if (textureAspect <= 0f || surfaceAspect <= 0f) return new Vector2(2f, 2f);
        if (textureAspect >= surfaceAspect)
        {
            float width = 2f * surfaceAspect;
            return new Vector2(width, width / textureAspect);
        }
        return new Vector2(2f * textureAspect, 2f);
    }

    static void EnsurePresenter()
    {
        if (presenter != null) return;
        var go = new GameObject("LabRenderScalePresenter");
        Object.DontDestroyOnLoad(go);
        presenter = go.AddComponent<Presenter>();
        if (!sceneHookInstalled)
        {
            sceneHookInstalled = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (presenter != null) presenter.Rebind();
    }

    sealed class Presenter : MonoBehaviour
    {
        RenderTexture texture;
        Camera source;
        Camera presentCamera;
        MeshRenderer quadRenderer;
        Material material;
        int presentLayer = -1;
        int targetWidth;
        int targetHeight;
        int lastSurfaceWidth;
        int lastSurfaceHeight;
        bool warnedSetupFailure;

        public bool Active { get; private set; }

        public void Apply(int width, int height)
        {
            targetWidth = width;
            targetHeight = height;

            // 先把呈现端搭好，确认无误后才把主相机切到中间纹理；
            // 搭建失败就整体不启用，游戏照常原生直渲。
            if (!EnsurePresentationRig())
            {
                Deactivate();
                return;
            }

            EnsureTexture(width, height);
            material.SetTexture("_MainTex", texture);
            Active = true;
            presentCamera.enabled = true;
            quadRenderer.enabled = true;
            Rebind();
            FitQuad();
            Debug.Log($"[LabRenderScale] 呈现启用：中间纹理 {width}x{height}，呈现层 {presentLayer}");
        }

        public void Deactivate()
        {
            Active = false;
            if (source != null && texture != null && source.targetTexture == texture)
                source.targetTexture = null;
            if (source != null && presentLayer >= 0)
                source.cullingMask |= 1 << presentLayer;
            source = null;
            if (presentCamera != null) presentCamera.enabled = false;
            if (quadRenderer != null) quadRenderer.enabled = false;
            ReleaseTexture();
        }

        /// <summary>场景切换后把中间纹理重新挂到新场景的主相机上。</summary>
        public void Rebind()
        {
            if (!Active) return;

            var next = FindSourceCamera();
            if (source != null && source != next && texture != null && source.targetTexture == texture)
                source.targetTexture = null;
            source = next;
            if (source != null && texture != null)
            {
                source.targetTexture = texture;
                // 呈现四边形只许呈现相机看：主相机若也画它，就会把中间
                // 纹理采样着写回它自己（反馈回路），必须从主相机剔除。
                source.cullingMask &= ~(1 << presentLayer);
                presentCamera.depth = source.depth + 10f;
            }
        }

        void Update()
        {
            if (!Active) return;

            // 主相机被换掉或纹理被别的逻辑摘掉时补挂（场景加载钩子之外的兜底）。
            if (source == null || source.targetTexture != texture)
                Rebind();

            // 表面尺寸变化（换显示器、全屏协商落地）时重算呈现比例。
            if (Screen.width != lastSurfaceWidth || Screen.height != lastSurfaceHeight)
                FitQuad();
        }

        Camera FindSourceCamera()
        {
            var main = Camera.main;
            if (main != null && main != presentCamera) return main;
            foreach (var cam in Camera.allCameras)
            {
                if (cam != null && cam != presentCamera && cam.enabled) return cam;
            }
            return null;
        }

        bool EnsurePresentationRig()
        {
            if (presentCamera != null) return true;

            if (presentLayer < 0) presentLayer = PickFreeLayer();
            if (presentLayer < 0)
            {
                WarnSetupFailure("没有空闲层可供呈现相机使用");
                return false;
            }

            var baseMaterial = Resources.Load<Material>("LabPresent");
            if (baseMaterial == null)
            {
                WarnSetupFailure("Resources/LabPresent 呈现材质缺失");
                return false;
            }
            // 复制一份再绑纹理，避免运行时改到材质资产本身（编辑器里会弄脏资产）。
            material = new Material(baseMaterial);

            var camGo = new GameObject("LabPresentCamera");
            camGo.transform.SetParent(transform, false);
            presentCamera = camGo.AddComponent<Camera>();
            presentCamera.clearFlags = CameraClearFlags.SolidColor;
            presentCamera.backgroundColor = Color.black;
            presentCamera.cullingMask = 1 << presentLayer;
            presentCamera.orthographic = true;
            presentCamera.orthographicSize = 1f;
            presentCamera.nearClipPlane = 0.1f;
            presentCamera.farClipPlane = 10f;
            presentCamera.allowHDR = false;
            presentCamera.allowMSAA = false;
            presentCamera.enabled = false;

            var quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadGo.name = "LabPresentQuad";
            quadGo.layer = presentLayer;
            quadGo.transform.SetParent(transform, false);
            quadGo.transform.position = new Vector3(0f, 0f, 5f);
            // 不许旋转：纹理按 UV 钉在顶点上，成像只由顶点投影位置决定，
            // 背面可见靠着色器的 Cull Off；绕 Y 转 180° 会把 u=1 的顶点
            // 甩到画面左侧、整幅左右镜像（2026-11-11 真机实测教训）。
            var collider = quadGo.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            quadRenderer = quadGo.GetComponent<MeshRenderer>();
            quadRenderer.sharedMaterial = material;
            quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quadRenderer.receiveShadows = false;
            quadRenderer.enabled = false;
            return true;
        }

        void EnsureTexture(int width, int height)
        {
            if (texture != null && texture.width == width && texture.height == height) return;
            ReleaseTexture();
            texture = new RenderTexture(width, height, 24, RenderTextureFormat.Default)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.Create();
        }

        void ReleaseTexture()
        {
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
            texture = null;
        }

        void FitQuad()
        {
            if (quadRenderer == null || targetWidth <= 0 || targetHeight <= 0) return;
            lastSurfaceWidth = Mathf.Max(1, Screen.width);
            lastSurfaceHeight = Mathf.Max(1, Screen.height);
            float surfaceAspect = lastSurfaceWidth / (float)lastSurfaceHeight;
            var size = ComputeQuadSize(targetWidth / (float)targetHeight, surfaceAspect);
            quadRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        void WarnSetupFailure(string reason)
        {
            if (warnedSetupFailure) return;
            warnedSetupFailure = true;
            Debug.LogWarning($"LabRenderScale: 呈现搭建失败（{reason}），全屏渲染缩放不启用，画面按原生分辨率直渲。");
        }

        static int PickFreeLayer()
        {
            for (int i = 31; i >= 8; i--)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) return i;
            }
            return -1;
        }
    }
}
