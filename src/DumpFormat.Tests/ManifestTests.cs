namespace ServiceControl.Migrate4to5.DumpFormat.Tests;

using System;
using System.IO;
using NUnit.Framework;

[TestFixture]
public class ManifestTests
{
    string root = null!;

    [SetUp] public void SetUp() => root = Directory.CreateTempSubdirectory("dumpfmt").FullName;
    [TearDown] public void TearDown() => Directory.Delete(root, recursive: true);

    [Test]
    public void Save_then_load_round_trips()
    {
        var paths = new DumpPaths(root);
        var m = new Manifest
        {
            ToolVersion = "0.1.0",
            SourceDbPath = @"C:\ProgramData\Particular\ServiceControl\HOST-33333",
            ExportedAtUtc = new DateTime(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc),
        };
        m.Collections.Add(new CollectionStats { Name = "FailedMessages", ExportedCount = 42, SkippedPastRetention = 3 });
        m.Save(paths);

        var loaded = Manifest.Load(paths);
        Assert.That(loaded.FormatVersion, Is.EqualTo(Manifest.CurrentFormatVersion));
        Assert.That(loaded.Collections[0].ExportedCount, Is.EqualTo(42));
        Assert.That(loaded.ExportedAtUtc, Is.EqualTo(m.ExportedAtUtc));
    }

    [Test]
    public void Load_without_manifest_throws_invalid_dump()
    {
        var paths = new DumpPaths(root);
        var ex = Assert.Throws<InvalidDumpException>(() => Manifest.Load(paths));
        Assert.That(ex!.Message, Does.Contain("manifest"));
    }

    [Test]
    public void Load_with_newer_format_version_throws()
    {
        var paths = new DumpPaths(root);
        File.WriteAllText(paths.ManifestPath, """{"FormatVersion": 999}""");
        Assert.Throws<InvalidDumpException>(() => Manifest.Load(paths));
    }

    [Test]
    public void Collection_file_name_sanitizes_slashes()
    {
        var paths = new DumpPaths(root);
        Assert.That(Path.GetFileName(paths.CollectionFile("RetryHistory")), Is.EqualTo("RetryHistory.jsonl"));
    }

    [Test]
    public void Seven_collections_in_import_order()
    {
        Assert.That(Collections.All, Has.Length.EqualTo(7));
        Assert.That(Collections.All[0].Name, Is.EqualTo("FailedMessages"));
        Assert.That(Collections.All[0].IdPrefix, Is.EqualTo("FailedMessages/"));
    }
}
