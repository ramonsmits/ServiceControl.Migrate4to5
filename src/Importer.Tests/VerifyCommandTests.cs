namespace ServiceControl.Migrate4to5.Importer.Tests;

using System.IO;
using NUnit.Framework;
using Raven.Client.Documents;
using ServiceControl.Migrate4to5.DumpFormat;
using ServiceControl.Migrate4to5.Importer.Tests.Fixtures;

[TestFixture]
public class VerifyCommandTests
{
    string dumpRoot = null!;
    string database = null!;

    [SetUp]
    public void SetUp() => (dumpRoot, database) = DumpBuilder.BuildAndImport();

    [TearDown] public void TearDown() => Directory.Delete(dumpRoot, recursive: true);

    int Run(out string output)
    {
        var writer = new StringWriter();
        var exit = VerifyCommand.Run(CliArgs.Parse([
            "verify", "--in", dumpRoot, "--url", RavenTestServer.ServerUrl,
            "--database", database, "--error-retention", "15.00:00:00"]), writer);
        output = writer.ToString();
        return exit;
    }

    [Test]
    public void Verify_passes_after_clean_import()
    {
        var exit = Run(out var output);
        Assert.That(output, Does.Contain("VERIFY PASSED"));
        Assert.That(exit, Is.Zero);
    }

    [Test]
    public void Verify_fails_when_a_document_is_missing()
    {
        using (var store = new DocumentStore { Urls = [RavenTestServer.ServerUrl], Database = database })
        {
            store.Initialize();
            using var session = store.OpenSession();
            session.Delete($"FailedMessages/{FailedMessageFixture.UniqueId}");
            session.SaveChanges();
        }

        var exit = Run(out var output);
        Assert.That(output, Does.Contain("VERIFY FAILED"));
        Assert.That(exit, Is.EqualTo(1));
    }
}
