namespace ServiceControl.Migrate4to5.Importer.Tests.Fixtures;

using System;
using System.IO;
using System.Text;
using ServiceControl.Migrate4to5.DumpFormat;

public static class DumpBuilder
{
    // Builds a small dump (one FailedMessage with an embedded body, one CustomCheck) and
    // imports it into a fresh database. Shared by ImportCommandTests and VerifyCommandTests
    // so both fixtures exercise the same known-good starting state.
    public static (string dumpRoot, string database) BuildAndImport()
    {
        var dumpRoot = Directory.CreateTempSubdirectory("importcmd").FullName;
        var database = RavenTestServer.NewDatabase();

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

        var writer = new StringWriter();
        var exit = ImportCommand.Run(CliArgs.Parse([
            "import", "--in", dumpRoot, "--url", RavenTestServer.ServerUrl,
            "--database", database, "--error-retention", "15.00:00:00"]), writer);
        if (exit != 0)
        {
            throw new InvalidOperationException($"DumpBuilder: import failed (exit {exit}):{Environment.NewLine}{writer}");
        }

        return (dumpRoot, database);
    }
}
