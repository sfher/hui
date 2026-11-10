using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 3×3 传送点：两人同时进入后标记该点；至少两个已标记点时才打开终点选择。
/// 选择列表由 <see cref="TuningLab"/> 在进入时收集，数据里不保存配对。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class LabTeleportPoint : MonoBehaviour
{
    public Color inactiveColor = new Color(0.65f, 0.35f, 1f);
    public Color markedColor = new Color(0.45f, 0.9f, 0.8f);
    public Color activeColor = new Color(1f, 0.7f, 0.25f);

    BoxCollider2D trigger;
    SpriteRenderer renderer;
    TuningLab lab;
    bool aInside;
    bool bInside;
    static Sprite cachedSprite;

    public bool IsMarked { get; private set; }

    void Awake() => ApplyShape();

    /// <summary>
    /// 把 3×3 触发区和贴图应用到碰撞箱和渲染器。关卡的编辑器预览也会直接调它，
    /// 所以不依赖 Awake；重复调用复用同一张贴图。
    /// </summary>
    public void ApplyShape()
    {
        if (trigger == null) trigger = GetComponent<BoxCollider2D>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
            trigger.size = new Vector2(3f, 3f);
        }

        if (renderer == null) renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            if (cachedSprite == null) cachedSprite = MakeSprite();
            renderer.sprite = cachedSprite;
            renderer.color = inactiveColor;
            renderer.sortingOrder = -1;
            LabArt.Apply(renderer, "portal");
        }
    }

    void Start()
    {
        lab = FindAnyObjectByType<TuningLab>();
        if (lab != null) lab.RegisterTeleport(this);
    }

    void OnDestroy()
    {
        if (lab != null) lab.UnregisterTeleport(this);
    }

    void Update()
    {
        if (lab == null || lab.TeleportBusy || lab.Paused) return;

        // 死亡/进膛会关掉碰撞箱但不会发 OnTriggerExit，所以每帧按真实包围盒重新判定。
        aInside = IsInside(lab.PlayerA);
        bInside = IsInside(lab.PlayerB);
        if (!aInside || !bInside) return;

        if (!IsMarked) lab.MarkTeleportPoint(this);
        var kb = Keyboard.current;
        if (kb == null) return;

        // 用绑定的"交互"键进选择，不再借用冲刺键（#152）：
        // 原来读 LeftShift / 句号，玩家想冲刺一下就被拉进选择界面。
        var bindings = lab.Bindings;
        if (bindings == null) return;

        if (ActionPressed(kb, bindings.For(true)[LabAction.Interact])
            || ActionPressed(kb, bindings.For(false)[LabAction.Interact]))
            lab.EnterTeleportSelection(this);
    }

    static bool ActionPressed(Keyboard kb, Key key)
    {
        if (key == Key.None) return false;
        var control = kb[key];
        return control != null && control.wasPressedThisFrame;
    }

    /// <summary>灵体不算"人在里面"：一条边全碎 = 按一人死亡处理，凑不齐两人就不标记、不开选择。</summary>
    bool IsInside(LabPlayer player)
        => player != null && player.IsAlive && !player.Suspended && !player.IsIntangible
           && trigger != null && player.BodyBounds.Intersects(trigger.bounds);

    /// <summary>选择界面里当前选中的终点会亮起来；离开选择界面时由 TuningLab 熄灭。</summary>
    public void SetHighlighted(bool highlighted)
    {
        if (renderer != null)
            renderer.color = highlighted ? activeColor : (IsMarked ? markedColor : inactiveColor);
    }

    public void SetMarked(bool marked)
    {
        IsMarked = marked;
        SetHighlighted(false);
    }

    /// <summary>
    /// 把两名玩家放到这个传送点两侧，保留形态并清零速度。传送会重置终点附近的地图并吞掉钥匙；
    /// 重生点记录不受影响（地图重置不会清 ActiveRespawnPoint）。
    /// </summary>
    public void TeleportPlayersHere(TuningLab owner)
    {
        if (owner == null) return;
        owner.ResetMap(transform.position);

        Vector2 center = transform.position;
        owner.PlayerA?.TeleportPreservingState(center + Vector2.left * 0.65f, Vector2.zero);
        owner.PlayerB?.TeleportPreservingState(center + Vector2.right * 0.65f, Vector2.zero);
        owner.PlayerA?.ClearKeys();
        owner.PlayerB?.ClearKeys();
    }

    static Sprite MakeSprite()
    {
        const int size = TuningLab.CellPixels * 3;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), TuningLab.CellPixels);
    }
}

sealed class LabTeleportAnimation
{
    static readonly Color HighlightA = new Color(0.2f, 0.8f, 1f, 1f);
    static readonly Color HighlightB = new Color(1f, 0.2f, 0.16f, 1f);

    readonly TuningLab owner;
    GameObject root;
    static Sprite beamSprite;
    static Material particleMaterial;

    public LabTeleportAnimation(TuningLab owner)
    {
        this.owner = owner;
    }

    sealed class Actor
    {
        public LabPlayer player;
        public LabVisual visual;
        public Color color;
        public SpriteRenderer beam;
        public float startY;
        public bool offscreen;
    }

    public IEnumerator PlayDeparture(LabPlayer playerA, LabPlayer playerB, Camera camera)
    {
        EnsureRoot();
        Actor a = MakeActor(playerA, HighlightA);
        Actor b = MakeActor(playerB, HighlightB);
        ParticleSystem particlesA = CreateAmbient(a.player.transform, a.color, owner.teleportHighlightSeconds);
        ParticleSystem particlesB = CreateAmbient(b.player.transform, b.color, owner.teleportHighlightSeconds);

        float elapsed = 0f;
        while (elapsed < owner.teleportHighlightSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / owner.teleportHighlightSeconds);
            a.visual.SetTeleportHighlight(a.color, progress);
            b.visual.SetTeleportHighlight(b.color, progress);
            yield return null;
        }

        a.visual.SetTeleportHighlight(a.color, 1f);
        b.visual.SetTeleportHighlight(b.color, 1f);
        DestroyParticle(particlesA);
        DestroyParticle(particlesB);
        a.visual.ClearTeleportHighlight();
        b.visual.ClearTeleportHighlight();
        a.visual.SetVisible(false);
        b.visual.SetVisible(false);
        a.beam = CreateBeam(a);
        b.beam = CreateBeam(b);

        while (!a.offscreen || !b.offscreen)
        {
            MoveBeam(a, camera);
            MoveBeam(b, camera);
            yield return null;
        }

        Dispose();
    }

    public IEnumerator PlayArrival(LabPlayer playerA, LabPlayer playerB)
    {
        EnsureRoot();
        Actor a = MakeActor(playerA, HighlightA);
        Actor b = MakeActor(playerB, HighlightB);
        PrepareArrival(a);
        PrepareArrival(b);
        float visibleSeconds = owner.teleportArrivalFadeSeconds + owner.teleportArrivalHoldSeconds;
        ParticleSystem particlesA = CreateAmbient(a.player.transform, a.color, visibleSeconds);
        ParticleSystem particlesB = CreateAmbient(b.player.transform, b.color, visibleSeconds);

        float elapsed = 0f;
        while (elapsed < owner.teleportArrivalFadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / owner.teleportArrivalFadeSeconds);
            a.visual.SetTeleportHighlight(a.color, 1f, progress);
            b.visual.SetTeleportHighlight(b.color, 1f, progress);
            yield return null;
        }

        a.visual.SetTeleportHighlight(a.color, 1f);
        b.visual.SetTeleportHighlight(b.color, 1f);
        yield return WaitUnscaled(owner.teleportArrivalHoldSeconds);
        DestroyParticle(particlesA);
        DestroyParticle(particlesB);
        CreateBurst(a.player.transform, a.color);
        CreateBurst(b.player.transform, b.color);
        a.visual.ClearTeleportHighlight();
        b.visual.ClearTeleportHighlight();
        yield return WaitUnscaled(owner.teleportBurstSeconds);
        Dispose();
    }

    public void Abort(LabPlayer playerA, LabPlayer playerB)
    {
        Dispose();
        RestoreAfterAbort(playerA);
        RestoreAfterAbort(playerB);
    }

    Actor MakeActor(LabPlayer player, Color color)
    {
        return new Actor
        {
            player = player,
            visual = player.GetComponentInChildren<LabVisual>(),
            color = color,
            startY = player.transform.position.y
        };
    }

    void PrepareArrival(Actor actor)
    {
        actor.visual.ClearTeleportHighlight();
        actor.visual.SetVisible(true);
        actor.visual.SetTeleportHighlight(actor.color, 1f, 0f);
    }

    SpriteRenderer CreateBeam(Actor actor)
    {
        var go = new GameObject(actor.player.name + "_TeleportBeam");
        go.transform.SetParent(root.transform, false);
        go.transform.position = actor.player.transform.position;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = BeamSprite();
        renderer.color = actor.color;
        renderer.sortingOrder = 22;
        return renderer;
    }

    void MoveBeam(Actor actor, Camera camera)
    {
        if (actor.offscreen) return;
        actor.beam.transform.position += Vector3.up * (owner.teleportBeamSpeed * Time.unscaledDeltaTime);
        bool outside = camera != null
            ? actor.beam.bounds.min.y > camera.transform.position.y + camera.orthographicSize
            : actor.beam.bounds.min.y > actor.startY + TuningLab.DefaultCameraMinSize * 2f;
        if (!outside) return;
        actor.offscreen = true;
        actor.beam.enabled = false;
    }

    ParticleSystem CreateAmbient(Transform anchor, Color color, float seconds)
    {
        if (seconds <= 0f) return null;
        float emissionSeconds = seconds * 0.7f;
        float lifetime = seconds * 0.3f;
        ParticleSystem particles = CreateParticles(anchor, color, emissionSeconds, lifetime, 0.65f, 0.1f, 0.55f);
        var emission = particles.emission;
        emission.rateOverTime = 36f;
        particles.Play();
        return particles;
    }

    void CreateBurst(Transform anchor, Color color)
    {
        float seconds = owner.teleportBurstSeconds;
        if (seconds <= 0f) return;
        ParticleSystem particles = CreateParticles(anchor, color, seconds, seconds, 3.5f, 0.12f, 0.1f);
        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });
        particles.Play();
    }

    ParticleSystem CreateParticles(Transform anchor, Color color, float duration,
        float lifetime, float speed, float size, float radius)
    {
        var go = new GameObject("TeleportParticles");
        go.transform.SetParent(root.transform, false);
        go.transform.position = anchor.position;
        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop();

        var main = particles.main;
        main.duration = Mathf.Max(0.01f, duration);
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = Mathf.Max(0.01f, lifetime);
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = color;
        main.maxParticles = 64;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true;

        var emission = particles.emission;
        emission.enabled = true;
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 1f;

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 18;
        renderer.sharedMaterial = ParticleMaterial();
        return particles;
    }

    static Material ParticleMaterial()
    {
        if (particleMaterial == null)
        {
            particleMaterial = new Material(Shader.Find("Sprites/Default"));
            particleMaterial.mainTexture = Texture2D.whiteTexture;
        }
        return particleMaterial;
    }

    static Sprite BeamSprite()
    {
        if (beamSprite != null) return beamSprite;
        const int width = 4;
        const int height = 48;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels32(pixels);
        texture.Apply();
        beamSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f), TuningLab.CellPixels);
        return beamSprite;
    }

    static IEnumerator WaitUnscaled(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    static void DestroyParticle(ParticleSystem particles)
    {
        if (particles != null) Object.Destroy(particles.gameObject);
    }

    static void RestoreAfterAbort(LabPlayer player)
    {
        if (player == null) return;
        var visual = player.GetComponentInChildren<LabVisual>();
        if (visual == null) return;
        visual.ClearTeleportHighlight();
        if (player.IsAlive && !player.Suspended) visual.SetVisible(true);
    }

    void EnsureRoot()
    {
        if (root != null) return;
        root = new GameObject("TeleportAnimation");
        root.transform.SetParent(owner.transform, false);
    }

    void Dispose()
    {
        if (root != null) Object.Destroy(root);
        root = null;
    }
}
