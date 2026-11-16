using System.Drawing;
using KINGRecorder.Capture.ScreenCapture;
using KINGRecorder.Core.Models;
using Xunit;

namespace KINGRecorder.Capture.Tests.ScreenCapture;

public class CaptureRegionTranslationTests
{
    [Theory]
    [InlineData(100, 100, 500, 400, 1.0, 100, 100, 500, 400)]
    [InlineData(100, 100, 500, 400, 1.25, 125, 125, 625, 500)]
    [InlineData(100, 100, 500, 400, 1.5, 150, 150, 750, 600)]
    [InlineData(100, 100, 500, 400, 2.0, 200, 200, 1000, 800)]
    public void TranslateCaptureRegionToPixels_AppliesDpiScale(
        int x, int y, int w, int h, double scale,
        int ex, int ey, int ew, int eh)
    {
        var region = new CaptureRegion(x, y, w, h, scale);
        var result = Direct3D11CaptureEngine.TranslateCaptureRegionToPixels(region);

        Assert.Equal(new Rectangle(ex, ey, ew, eh), result);
    }

    [Fact]
    public void TranslateCaptureRegionToPixels_NullRegion_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Direct3D11CaptureEngine.TranslateCaptureRegionToPixels(null!));
    }

    [Fact]
    public void TranslateCaptureRegionToPixels_NonPositiveScale_Throws()
    {
        var region = new CaptureRegion(0, 0, 10, 10, 0.0);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Direct3D11CaptureEngine.TranslateCaptureRegionToPixels(region));
    }
}
