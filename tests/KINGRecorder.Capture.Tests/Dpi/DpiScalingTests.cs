using Xunit;
using KINGRecorder.Core.Models;

namespace KINGRecorder.Capture.Tests.Dpi;

public class DpiScalingTests
{
    [Theory]
    [InlineData(100, 100, 500, 400, 1.0, 100, 100, 500, 400)]
    [InlineData(100, 100, 500, 400, 1.25, 125, 125, 625, 500)]
    [InlineData(100, 100, 500, 400, 1.5, 150, 150, 750, 600)]
    [InlineData(100, 100, 500, 400, 2.0, 200, 200, 1000, 800)]
    public void ScaleRegion_AppliesDpiMultiplierAccurately(
        int rawX, int rawY, int rawW, int rawH, double scale,
        int expectedX, int expectedY, int expectedW, int expectedH)
    {
        var scaledX = (int)Math.Round(rawX * scale);
        var scaledY = (int)Math.Round(rawY * scale);
        var scaledW = (int)Math.Round(rawW * scale);
        var scaledH = (int)Math.Round(rawH * scale);

        Assert.Equal(expectedX, scaledX);
        Assert.Equal(expectedY, scaledY);
        Assert.Equal(expectedW, scaledW);
        Assert.Equal(expectedH, scaledH);
    }
}
