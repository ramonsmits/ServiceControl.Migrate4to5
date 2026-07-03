namespace ServiceControl.Migrate4to5.Importer.Tests;

using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using ServiceControl.Migrate4to5.DumpFormat;

[TestFixture]
public class RavenTargetTests
{
    string dumpRoot = null!;
    BodyStore bodies = null!;
    RavenTarget target = null!;

    [SetUp]
    public void SetUp()
    {
        dumpRoot = Directory.CreateTempSubdirectory("raventarget").FullName;
        bodies = new BodyStore(new DumpPaths(dumpRoot));
        target = new RavenTarget(RavenTestServer.ServerUrl, RavenTestServer.NewDatabase(), null, null);
    }

    [TearDown]
    public void TearDown()
    {
        target.Dispose();
        Directory.Delete(dumpRoot, recursive: true);
    }

    static TransformedDoc Doc(string id, string json, BodyRef? body = null) => new()
    {
        Id = id,
        Document = JObject.Parse(json),
        Metadata = new JObject { ["@collection"] = "FailedMessages", ["Raven-Clr-Type"] = "ServiceControl.MessageFailures.FailedMessage, ServiceControl.Persistence" },
        Body = body,
    };

    [Test]
    public void Imports_document_with_metadata_and_attachment()
    {
        var bodyRef = bodies.Store(Encoding.UTF8.GetBytes("<xml/>"), "text/xml");
        var result = target.ImportBatch([Doc("FailedMessages/1", """{"Status":1,"UniqueMessageId":"1"}""", bodyRef)], bodies);

        Assert.That(result.Imported, Is.EqualTo(1));
        var raw = target.LoadRaw("FailedMessages/1")!;
        Assert.That((int)raw["Status"]!, Is.EqualTo(1));
        Assert.That((string)raw["@metadata"]!["@collection"]!, Is.EqualTo("FailedMessages"));
        Assert.That(target.AttachmentExists("FailedMessages/1", out var size), Is.True);
        Assert.That(size, Is.EqualTo(6));
        Assert.That(target.CollectionCounts()["FailedMessages"], Is.EqualTo(1));
    }

    [Test]
    public void Existing_documents_are_skipped_not_overwritten()
    {
        target.ImportBatch([Doc("FailedMessages/2", """{"Status":1}""")], bodies);
        var second = target.ImportBatch([Doc("FailedMessages/2", """{"Status":4}""")], bodies);

        Assert.That(second.SkippedExisting, Is.EqualTo(1));
        Assert.That(second.Imported, Is.Zero);
        Assert.That((int)target.LoadRaw("FailedMessages/2")!["Status"]!, Is.EqualTo(1), "must not overwrite");
    }

    [Test]
    public void Expires_metadata_round_trips_as_date()
    {
        var doc = Doc("FailedMessages/3", """{"Status":4}""");
        doc.Metadata["@expires"] = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        target.ImportBatch([doc], bodies);

        Assert.That((DateTime)target.LoadRaw("FailedMessages/3")!["@metadata"]!["@expires"]!,
            Is.EqualTo(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public void Skip_marked_docs_are_not_written()
    {
        var doc = Doc("FailedMessages/4", """{"Status":2}""");
        doc.Skip = true;
        var result = target.ImportBatch([doc], bodies);
        Assert.That(result.Imported, Is.Zero);
        Assert.That(target.LoadRaw("FailedMessages/4"), Is.Null);
    }
}
