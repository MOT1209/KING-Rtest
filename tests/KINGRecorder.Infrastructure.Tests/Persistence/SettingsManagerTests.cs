using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.Models;
using KINGRecorder.Infrastructure;

namespace KINGRecorder.Infrastructure.Tests.Persistence;

public class SettingsManagerTests : IDisposable
{
    private readonly string _tempFilePath;

    public SettingsManagerTests()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"king_rec_test_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            try { File.Delete(_tempFilePath); } catch { }
        }
    }

    [Fact]
    public async Task LoadSettings_WhenFileMissing_ReturnsDefaultOptions()
    {
        var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, _tempFilePath);
        var options = await manager.LoadSettingsAsync();

        Assert.NotNull(options);
        Assert.Equal(VideoResolutionPreset.P1080, options.Resolution);
        Assert.Equal(VideoFrameRate.Fps60, options.FrameRate);
    }

    [Fact]
    public async Task SaveAndLoadSettings_RoundtripsSuccessfully()
    {
        var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, _tempFilePath);
        var original = await manager.LoadSettingsAsync();

        var modified = original with
        {
            Resolution = VideoResolutionPreset.P720,
            MicrophoneVolume = 0.5f
        };

        await manager.SaveSettingsAsync(modified);
        var loaded = await manager.LoadSettingsAsync();

        Assert.Equal(VideoResolutionPreset.P720, loaded.Resolution);
        Assert.Equal(0.5f, loaded.MicrophoneVolume);
    }

    [Fact]
    public async Task SaveSettings_WithInvalidVolume_ThrowsArgumentOutOfRange()
    {
        var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, _tempFilePath);
        var options = await manager.LoadSettingsAsync();
        var invalid = options with { MicrophoneVolume = 1.5f };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.SaveSettingsAsync(invalid));
    }

    [Fact]
    public async Task SaveSettings_WithNanOrInfinityVolume_ThrowsArgumentOutOfRange()
    {
        var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, _tempFilePath);
        var options = await manager.LoadSettingsAsync();
        var invalidNan = options with { SystemAudioVolume = float.NaN };
        var invalidInf = options with { MicrophoneVolume = float.PositiveInfinity };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.SaveSettingsAsync(invalidNan));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.SaveSettingsAsync(invalidInf));
    }
}
