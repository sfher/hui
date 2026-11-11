using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 分辨率档位表（LabSettingsUI.GenerateResolutions）：档位必须全部与
/// 原生同宽高比——此前采信系统模式表，抽出的档位大半是异比例。
/// </summary>
public sealed class LabResolutionTableTests
{
    [Test]
    public void Native4K_AllTiersKeepNativeAspect()
    {
        var tiers = LabSettingsUI.GenerateResolutions(3840, 2160);
        Assert.IsTrue(tiers.Count >= 5, "4K 原生应生成至少 5 档");
        Assert.Contains(new Vector2Int(3840, 2160), tiers);
        Assert.Contains(new Vector2Int(1920, 1080), tiers);
        foreach (var t in tiers)
        {
            float aspect = t.x / (float)t.y;
            Assert.AreEqual(16f / 9f, aspect, 0.01f, $"档位 {t} 与原生宽高比不符");
            Assert.IsTrue(t.x >= 800 && t.y >= 450, $"档位 {t} 低于下限");
        }
    }

    [Test]
    public void Native1080p_TiersStayAboveFloor()
    {
        var tiers = LabSettingsUI.GenerateResolutions(1920, 1080);
        Assert.Contains(new Vector2Int(1920, 1080), tiers);
        Assert.Contains(new Vector2Int(1280, 720), tiers);
        foreach (var t in tiers)
            Assert.IsTrue(t.x >= 800 && t.y >= 450, $"档位 {t} 低于下限");
    }

    [Test]
    public void NoDuplicates()
    {
        var tiers = LabSettingsUI.GenerateResolutions(2560, 1440);
        for (int i = 0; i < tiers.Count; i++)
            for (int j = i + 1; j < tiers.Count; j++)
                Assert.AreNotEqual(tiers[i], tiers[j]);
    }

    [Test]
    public void UnknownNative_ReturnsEmpty()
    {
        Assert.IsEmpty(LabSettingsUI.GenerateResolutions(0, 0));
        Assert.IsEmpty(LabSettingsUI.GenerateResolutions(-1, 1080));
    }
}
