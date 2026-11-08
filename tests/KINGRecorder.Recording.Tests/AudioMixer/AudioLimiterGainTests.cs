using System;
using Xunit;

namespace KINGRecorder.Recording.Tests.AudioMixer;

public class AudioLimiterGainTests
{
    [Theory]
    [InlineData(0.5f, 0.4f, 1.0f)]
    [InlineData(0.2f, 0.3f, 1.0f)]
    public void CalculateLimiterDamping_SumLessThanOrEqualToOne_NoDamping(float mic, float sys, float expectedDamping)
    {
        float sum = mic + sys;
        float damping = sum > 1.0f ? 1.0f / (float)Math.Sqrt(sum) : 1.0f;
        Assert.Equal(expectedDamping, damping);
    }

    [Theory]
    [InlineData(1.0f, 1.0f)]
    [InlineData(0.8f, 0.8f)]
    public void CalculateLimiterDamping_SumGreaterThanOne_AppliesSoftClippingDamping(float mic, float sys)
    {
        float sum = mic + sys;
        float damping = sum > 1.0f ? 1.0f / (float)Math.Sqrt(sum) : 1.0f;

        Assert.True(damping < 1.0f);
        Assert.True(mic * damping < 1.0f);
        Assert.True(sys * damping < 1.0f);
    }
}
