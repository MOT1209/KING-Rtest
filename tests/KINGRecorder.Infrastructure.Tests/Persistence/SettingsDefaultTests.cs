using System;
using System.IO;
using KINGRecorder.Infrastructure;
using Xunit;

namespace KINGRecorder.Infrastructure.Tests.Persistence;

public class SettingsDefaultTests
{
    [Fact]
    public void GetDefaultOptions_OutputDirectory_IsNotHardcodedAndUsesMyVideos()
    {
        var options = SettingsManager.GetDefaultOptions();
        var expectedBase = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

        Assert.NotNull(options.OutputDirectory);
        Assert.StartsWith(expectedBase, options.OutputDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("KING Recorder", options.OutputDirectory, StringComparison.OrdinalIgnoreCase);
    }
}
