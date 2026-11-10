using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 合并碰撞体的帧末统一重建（Issue #150）。
///
/// 冰、岩浆和地形共用一个 <see cref="CompositeCollider2D"/>。原来那个 composite 是
/// Synchronous 的，于是每碎掉一块冰（`body.enabled = false`）都会自动重建**整张几何**
/// —— 关卡上万个格子、八百多块冰，撞碎一组就是几十到上百毫秒的停顿。
///
/// 现在 composite 保持 Manual：碎冰/碎岩浆只把 composite 标脏，真正的
/// `GenerateGeometry()` 推迟到这一帧的 LateUpdate，同一帧里碎多少块都只算一次。
///
/// 放在 LateUpdate 而不是下一次 FixedUpdate，是为了让几何在本帧渲染前就更新，
/// 玩家不会看到"冰已经消失了但碰撞还在"的一帧。
/// </summary>
public sealed class LabCompositeRebuild : MonoBehaviour
{
    static LabCompositeRebuild instance;
    static readonly List<CompositeCollider2D> pending = new List<CompositeCollider2D>();

    /// <summary>把一个 composite 标脏，等帧末重建。</summary>
    public static void Enqueue(CompositeCollider2D composite)
    {
        if (composite == null) return;
        if (!pending.Contains(composite)) pending.Add(composite);

        if (instance != null) return;
        var go = new GameObject("CompositeRebuild")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        DontDestroyOnLoad(go);
        instance = go.AddComponent<LabCompositeRebuild>();
    }

    void LateUpdate()
    {
        if (pending.Count == 0) return;

        for (int i = 0; i < pending.Count; i++)
            if (pending[i] != null) pending[i].GenerateGeometry();

        pending.Clear();
        Destroy(gameObject);
        instance = null;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
