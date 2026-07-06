namespace ServiceControl.Migrate4to5.Importer.Tests;

using System.IO;
using NUnit.Framework;
using ServiceControl.Migrate4to5.DumpFormat;
using ServiceControl.Migrate4to5.Importer.Tests.Fixtures;

[TestFixture]
public class ImportCommandTests
{
    string dumpRoot = null!;
    string database = null!;

    [SetUp]
    public void SetUp() => (dumpRoot, database) = DumpBuilder.Build();

    [TearDown] public void TearDown() => Directory.Delete(dumpRoot, recursive: true);

    int Run(out string output)
    {
        var writer = new StringWriter();
        var exit = ImportCommand.Run(CliArgs.Parse([
            "import", "--in", dumpRoot, "--url", RavenTestServer.ServerUrl,
            "--database", database, "--error-retention", "15.00:00:00"]), writer);
        output = writer.ToString();
        return exit;
    }

    [Test]
    public void Imports_dump_and_reports_summary()
    {
        var exit = Run(out var output);
        Assert.That(exit, Is.Zero);
        Assert.That(output, Does.Contain("FailedMessages").And.Contain("CustomChecks"));
        Assert.That(output, Does.Contain("imported:       1"));

        using var target = new RavenTarget(RavenTestServer.ServerUrl, database, null, null);
        Assert.That(target.LoadRaw($"FailedMessages/{FailedMessageFixture.UniqueId}"), Is.Not.Null);
        Assert.That(target.AttachmentExists($"FailedMessages/{FailedMessageFixture.UniqueId}", out _), Is.True);
        Assert.That(target.LoadRaw("CustomChecks/11111111-1111-1111-1111-111111111111"), Is.Not.Null);
    }

    [Test]
    public void Rerun_skips_everything_and_still_exits_zero()
    {
        Run(out _);
        var exit = Run(out var output);
        Assert.That(exit, Is.Zero);
        Assert.That(output, Does.Contain("skipped (existing): 1"));
    }

    [Test]
    public void Missing_manifest_exits_nonzero()
    {
        File.Delete(Path.Combine(dumpRoot, "manifest.json"));
        Assert.That(Run(out _), Is.Not.Zero);
    }

    [Test]
    public void Jsonl_file_not_listed_in_manifest_is_ignored()
    {
        // A .jsonl file can end up on disk without being part of the manifest (leftover from a
        // different export, hand-edited, etc). Import must be driven by the manifest, not by
        // what files happen to exist under collections/.
        var paths = new DumpPaths(dumpRoot);
        const string strayId = "KnownEndpoints/22222222-2222-2222-2222-222222222222";
        using (var w = new JsonlWriter(paths, "KnownEndpoints"))
        {
            w.Write(new DumpLine
            {
                Id = strayId,
                Metadata = DumpJson.Parse("""{"Raven-Entity-Name":"KnownEndpoints"}"""),
                Document = DumpJson.Parse("""{"EndpointDetails":{"Name":"Sales"},"HasTemporaryId":false}"""),
            });
        }

        var exit = Run(out var output);

        Assert.That(exit, Is.Zero);
        Assert.That(output, Does.Not.Contain("KnownEndpoints"));

        using var target = new RavenTarget(RavenTestServer.ServerUrl, database, null, null);
        Assert.That(target.LoadRaw(strayId), Is.Null);
    }
}
