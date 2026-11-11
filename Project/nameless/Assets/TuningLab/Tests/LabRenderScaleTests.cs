using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 全屏渲染缩放（#153）的纯逻辑：何时启用、呈现四边形的等比装框。
/// 实际像素效果（世界按所选档位变粗、IMGUI 保持原生）需真机目验，
/// 这里只钉住可计算的契约，避免装框算错把画面拉伸变形。
/// </summary>
public sealed class LabRenderScaleTests
{
    [Test]
    public void ActivatesOnlyWhenSelectedIsSmallerThanNative()
    {
        Assert.IsTrue(LabRenderScale.ShouldActivate(1920, 1080, 3840, 2160));
        Assert.IsTrue(LabRenderScale.ShouldActivate(800, 480, 3840, 2160));
        Assert.IsTrue(LabRenderScale.ShouldActivate(3840, 1080, 3840, 2160),
            "只有一边更小时也要缩放，另一边由装框留黑边");
        Assert.IsFalse(LabRenderScale.ShouldActivate(3840, 2160, 3840, 2160), "原生档位不缩放");
        Assert.IsFalse(LabRenderScale.ShouldActivate(4096, 2160, 3840, 2160), "大于原生不缩放");
    }

    [Test]
    public void InvalidSizesNeverActivate()
    {
        Assert.IsFalse(LabRenderScale.ShouldActivate(0, 1080, 3840, 2160));
        Assert.IsFalse(LabRenderScale.ShouldActivate(1920, 0, 3840, 2160));
        Assert.IsFalse(LabRenderScale.ShouldActivate(1920, 1080, 0, 2160));
        Assert.IsFalse(LabRenderScale.ShouldActivate(1920, 1080, 3840, 0));
    }

    [Test]
    public void QuadFillsSurfaceWhenAspectsMatch()
    {
        var size = LabRenderScale.ComputeQuadSize(16f / 9f, 16f / 9f);
        Assert.AreEqual(2f * (16f / 9f), size.x, 1e-4f);
        Assert.AreEqual(2f, size.y, 1e-4f);
    }

    [Test]
    public void WiderTextureLetterboxesWithoutStretching()
    {
        // 纹理 21:9、表面 16:9：宽度拉满，高度按比例缩小，上下留黑边。
        var size = LabRenderScale.ComputeQuadSize(21f / 9f, 16f / 9f);
        Assert.AreEqual(2f * (16f / 9f), size.x, 1e-4f);
        Assert.AreEqual(size.x / (21f / 9f), size.y, 1e-4f);
        Assert.Less(size.y, 2f);
        Assert.AreEqual(21f / 9f, size.x / size.y, 1e-4f, "装框后宽高比必须仍是纹理的，不能拉伸");
    }

    [Test]
    public void TallerTexturePillarboxesWithoutStretching()
    {
        // 纹理 4:3、表面 16:9：高度拉满，宽度按比例缩小，左右留黑边。
        var size = LabRenderScale.ComputeQuadSize(4f / 3f, 16f / 9f);
        Assert.AreEqual(2f, size.y, 1e-4f);
        Assert.AreEqual(2f * (4f / 3f), size.x, 1e-4f);
        Assert.AreEqual(4f / 3f, size.x / size.y, 1e-4f);
    }
}
