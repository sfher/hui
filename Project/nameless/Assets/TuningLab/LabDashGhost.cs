using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 冲刺图章：冲刺（<see cref="LabPlayer.InDashState"/>）期间每隔一小段时间，
/// 把玩家当前的分层视觉（速度外圈 / 四条边 / 白眼睛 / 像素贴图）原样盖在原地，
/// 每张图章在 `dashGhostLife`（默认 0.3 s）内线性淡到全透明后回收。
///
/// 只认冲刺态，与 <see cref="LabSpeedFeedback"/> 的速度档残影是两条独立通道：
/// 走路 / 跳跃 / 被弹飞不出图章，低速冲刺也照出。图章不跟随玩家、不参与碰撞，
/// 生成后层级整体压到本体之下。对象池到上限回收最老的一张，连续冲刺不分配新物体。
/// </summary>
public sealed class LabDashGhost : MonoBehaviour
{
    /// <summary>图章相对本体部件的排序偏移：压在本体与速度残影之后。</summary>
    const int SortOffset = 4;

    sealed class Stamp
    {
        public GameObject go;
        public SpriteRenderer[] parts;
        public Color[] colors;
        public float age;
        public bool active;
    }

    TuningLab lab;
    LabPlayer player;
    readonly List<Stamp> pool = new List<Stamp>();
    float timer;

    public void Bind(TuningLab tuningLab, LabPlayer target)
    {
        lab = tuningLab;
        player = target;
    }

    void FixedUpdate()
    {
        if (lab == null || player == null) return;
        bool dashing = player.InDashState && !player.Suspended && player.IsAlive;
        if (!dashing || lab.dashGhostLife <= 0f || lab.dashGhostCap <= 0)
        {
            timer = 0f;                 // 下一次冲刺的第一张立刻盖
            return;
        }

        timer -= Time.fixedDeltaTime;
        if (timer > 0f) return;
        timer = Mathf.Max(0.01f, lab.dashGhostInterval);
        StampIt();
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        float life = lab != null ? Mathf.Max(0.0001f, lab.dashGhostLife) : 0.3f;
        for (int i = 0; i < pool.Count; i++)
        {
            var s = pool[i];
            if (s == null || s.go == null) { pool.RemoveAt(i--); continue; }
            if (!s.active) continue;

            s.age += dt;
            float k = 1f - Mathf.Clamp01(s.age / life);
            if (k <= 0f)
            {
                s.active = false;
                s.go.SetActive(false);
                continue;
            }
            for (int p = 0; p < s.parts.Length; p++)
            {
                if (s.parts[p] == null) continue;
                var c = s.colors[p];
                c.a *= k;
                s.parts[p].color = c;
            }
        }
    }

    void StampIt()
    {
        var visual = player.visual;
        if (visual == null) return;
        int count = visual.transform.childCount;
        if (count == 0) return;

        var s = Acquire();
        if (s == null) return;
        EnsureParts(s, count);

        // 图章挂在场景根上，不跟玩家走；位置/朝向取盖下这一刻的本体视觉。
        var root = s.go.transform;
        root.position = visual.transform.position;
        root.rotation = visual.transform.rotation;
        root.localScale = visual.transform.lossyScale;

        float alpha = Mathf.Clamp01(lab.dashGhostAlpha);
        for (int i = 0; i < count; i++)
        {
            var src = visual.transform.GetChild(i).GetComponent<SpriteRenderer>();
            var dst = s.parts[i];
            if (dst == null) continue;
            if (src == null)
            {
                dst.enabled = false;
                s.colors[i] = Color.clear;
                continue;
            }

            dst.sprite = src.sprite;
            dst.sortingLayerID = src.sortingLayerID;
            dst.sortingOrder = src.sortingOrder - SortOffset;
            dst.enabled = src.enabled;

            var c = src.color;
            c.a *= alpha;
            dst.color = c;
            s.colors[i] = c;

            dst.transform.localPosition = src.transform.localPosition;
            dst.transform.localRotation = src.transform.localRotation;
            dst.transform.localScale = src.transform.localScale;
        }

        s.age = 0f;
        s.active = true;
        s.go.SetActive(true);
    }

    Stamp Acquire()
    {
        Stamp oldest = null;
        for (int i = 0; i < pool.Count; i++)
        {
            var s = pool[i];
            if (s == null || s.go == null) { pool.RemoveAt(i--); continue; }
            if (!s.active) return s;
            if (oldest == null || s.age > oldest.age) oldest = s;
        }
        if (pool.Count < Mathf.Max(1, lab.dashGhostCap))
        {
            var created = new Stamp { go = new GameObject("DashStamp") };
            created.go.SetActive(false);
            pool.Add(created);
            return created;
        }
        return oldest ?? (pool.Count > 0 ? pool[0] : null);
    }

    static void EnsureParts(Stamp s, int count)
    {
        if (s.parts != null && s.parts.Length == count) return;

        var root = s.go.transform;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        s.parts = new SpriteRenderer[count];
        s.colors = new Color[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Part");
            go.transform.SetParent(root, false);
            s.parts[i] = go.AddComponent<SpriteRenderer>();
            s.parts[i].sprite = TuningLab.WhiteSprite;
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < pool.Count; i++)
            if (pool[i] != null && pool[i].go != null) Destroy(pool[i].go);
        pool.Clear();
    }
}
