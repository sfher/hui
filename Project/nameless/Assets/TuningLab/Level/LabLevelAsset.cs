using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 独立存放的关卡数据资源（Issue #109）。
///
/// 之前关卡数据直接序列化在场景的 <see cref="LabLevel"/> 组件上，导致：
///   - 一个场景只能有一个关卡，拆开搭的部分没法合并
///   - 在同一个场景里堆太多内容会卡
///   - 没法用 git 对比两块区域的差异
///
/// 现在数据可以搬到独立的 .asset 上，场景只持有引用。合并就是纯数据合并，
/// 不需要打开别的场景。
///
/// 兼容：<see cref="LabLevel"/> 不引用资源时仍然用自己内嵌的那份数据，
/// 老场景和新场景都能跑。
/// </summary>
[CreateAssetMenu(fileName = "LabLevelAsset", menuName = "GameJam/Lab Level Asset")]
public sealed class LabLevelAsset : ScriptableObject
{
    [SerializeField] LabLevelData data = new LabLevelData();

    public LabLevelData Data
    {
        get
        {
            if (data == null) data = new LabLevelData();
            return data;
        }
    }
}
