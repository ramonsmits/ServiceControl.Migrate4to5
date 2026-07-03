namespace ServiceControl.Migrate4to5.Importer.Tests;

using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using ServiceControl.Migrate4to5.DumpFormat;
using ServiceControl.Migrate4to5.Importer.Tests.Fixtures;

[TestFixture]
public class ImportCommandTests
{
    string dumpRoot = null!;
    string database = null!;

    [SetUp]
    public void SetUp()
    {
        dumpRoot = Directory.CreateTempSubdirectory("importcmd").FullName;
        database = RavenTestServer.NewDatabase();

        var paths = new DumpPaths(dumpRoot);
        var bodies = new BodyStore(paths);
        var bodyRef = bodies.Store(Encoding.UTF8.GetBytes("<Order>embedded body</Order>"), "text/xml");

        var failed = FailedMessageFixture.Line(status: 1);
        failed.Bodies["msg-2"] = bodyRef;
        using (var w = new JsonlWriter(paths, "FailedMessages")) { w.Write(failed); }
        using (var w = new JsonlWriter(paths, "CustomChecks"))
        {
            w.Write(new DumpLine
            {
                Id = "CustomChecks/11111111-1111-1111-1111-111111111111",
                Metadata = DumpJson.Parse("""{"Raven-Entity-Name":"CustomChecks"}"""),
                Document = DumpJson.Parse("""{"CustomCheckId":"MyCheck","Status":0}"""),
            });
        }

        var manifest = new Manifest { ToolVersion = "test", SourceDbPath = "x", ExportedAtUtc = DateTime.UtcNow };
        manifest.Collections.Add(new CollectionStats { Name = "FailedMessages", ExportedCount = 1 });
        manifest.Collections.Add(new CollectionStats { Name = "CustomChecks", ExportedCount = 1 });
        manifest.BodyCount = 1;
        manifest.Save(paths);
    }

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
}
