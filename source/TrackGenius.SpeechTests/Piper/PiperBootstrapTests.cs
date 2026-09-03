using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TrackGenius.Speech.Piper;

namespace TrackGenius.SpeechTests.Piper;

[TestFixture]
public class PiperBootstrapTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"piper-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private static void WriteCompleteModel(string modelDir)
    {
        Directory.CreateDirectory(modelDir);
        File.WriteAllText(Path.Combine(modelDir, "model.json"), "{}");
        File.WriteAllBytes(Path.Combine(modelDir, "voice.onnx"), new byte[] { 1 });
    }

    [Test]
    public void GivenNothingInstalled_WhenIsReadyRead_ThenFalse()
    {
        var bootstrap = new PiperBootstrap(_root, NullLogger<PiperBootstrap>.Instance);

        Assert.That(bootstrap.IsReady, Is.False);
    }

    [Test]
    public async Task GivenExeAndModelPresent_WhenEnsureReadyCalled_ThenImmediateNoProgress()
    {
        Directory.CreateDirectory(Path.Combine(_root, "piper"));
        File.WriteAllText(bootstrap_exe(_root), "stub");
        WriteCompleteModel(Path.Combine(_root, "piper", "voices", "en_US-amy-medium"));
        var bootstrap = new PiperBootstrap(_root, NullLogger<PiperBootstrap>.Instance);
        var progressReports = new List<string>();

        var setup = await bootstrap.EnsureReadyAsync(new Progress<string>(progressReports.Add));

        Assert.Multiple(() =>
        {
            Assert.That(bootstrap.IsReady, Is.True);
            Assert.That(progressReports, Is.Empty);   // cached path: zero downloads, zero progress
            Assert.That(setup.ExecutablePath, Is.EqualTo(bootstrap_exe(_root)));
            Assert.That(setup.ModelDirectory, Is.EqualTo(Path.Combine(_root, "piper", "voices", "en_US-amy-medium")));
        });
    }

    [Test]
    public async Task GivenExeOnly_WhenIsReadyRead_ThenFalseUntilModelExists()
    {
        Directory.CreateDirectory(Path.Combine(_root, "piper"));
        File.WriteAllText(bootstrap_exe(_root), "stub");
        var bootstrap = new PiperBootstrap(_root, NullLogger<PiperBootstrap>.Instance);

        Assert.That(bootstrap.IsReady, Is.False);   // exe without model is not ready
        WriteCompleteModel(Path.Combine(_root, "piper", "voices", "en_US-amy-medium"));

        Assert.That(bootstrap.IsReady, Is.True);
        var setup = await bootstrap.EnsureReadyAsync();
        Assert.That(setup.WorkingDirectory, Is.EqualTo(Path.Combine(_root, "piper")));
    }

    private static string bootstrap_exe(string root)
        => Path.Combine(root, "piper", "piper.exe");
}
