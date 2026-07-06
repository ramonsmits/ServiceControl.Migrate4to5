namespace ServiceControl.Migrate4to5.Exporter.Tests;

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Raven.Client.Embedded;
using Raven.Json.Linq;
using ServiceControl.Migrate4to5.DumpFormat;

[TestFixture]
public class SourceDatabaseTests
{
    EmbeddableDocumentStore store = null!;
    SourceDatabase source = null!;

    [SetUp]
    public void SetUp()
    {
        store = new EmbeddableDocumentStore { RunInMemory = true };
        store.Conventions.SaveEnumsAsIntegers = true;
        store.Initialize();
        source = new SourceDatabase(store); // internal test ctor: SourceDatabase owns disposal

        Put("FailedMessages/aaa", "FailedMessages", """{"UniqueMessageId":"aaa","Status":1}""");
        Put("FailedMessages/bbb", "FailedMessages", """{"UniqueMessageId":"bbb","Status":4}""");
        Put("CustomChecks/ccc", "CustomChecks", """{"CustomCheckId":"Check1","Status":0}""");
        Put("RetryOperations/History", "RetryHistories", """{"HistoricOperations":[]}""");
    }

    void Put(string id, string entityName, string json) =>
        store.DatabaseCommands.Put(id, null, RavenJObject.Parse(json),
            new RavenJObject { { "Raven-Entity-Name", entityName } });

    [TearDown]
    public void TearDown() => source.Dispose();

    [Test]
    public void Streams_all_docs_with_prefix_and_metadata()
    {
        var docs = source.Stream(new CollectionSpec { Name = "FailedMessages", IdPrefix = "FailedMessages/" }).ToList();
        Assert.That(docs.Select(d => d.Id), Is.EquivalentTo(new[] { "FailedMessages/aaa", "FailedMessages/bbb" }));
        var aaa = docs.Single(d => d.Id == "FailedMessages/aaa");
        Assert.That((string)aaa.Metadata["Raven-Entity-Name"]!, Is.EqualTo("FailedMessages"));
        Assert.That((int)aaa.Document["Status"]!, Is.EqualTo(1));
    }

    [Test]
    public void Fixed_id_spec_yields_one_doc_or_none()
    {
        Assert.That(source.Stream(new CollectionSpec { Name = "RetryHistory", FixedId = "RetryOperations/History" }).Count(), Is.EqualTo(1));
        Assert.That(source.Stream(new CollectionSpec { Name = "MessageRedirects", FixedId = "messageredirects" }), Is.Empty);
    }

    [Test]
    public void Reads_legacy_attachment_with_content_type()
    {
        var bytes = Encoding.UTF8.GetBytes("<big/>");
#pragma warning disable 618
        store.DatabaseCommands.PutAttachment("messagebodies/msg-9", null, new MemoryStream(bytes),
            new RavenJObject { { "ContentType", "application/json" }, { "ContentLength", bytes.Length } });
#pragma warning restore 618

        var att = source.GetLegacyAttachment("msg-9");
        Assert.That(att, Is.Not.Null);
        Assert.That(att!.Value.Content, Is.EqualTo(bytes));
        Assert.That(att.Value.ContentType, Is.EqualTo("application/json"));
        Assert.That(source.GetLegacyAttachment("nope"), Is.Null);
    }

    [Test]
    public void Missing_db_path_throws_before_touching_the_filesystem()
    {
        var missing = Path.Combine(Path.GetTempPath(), "sc-migrate-no-such-dir-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidOperationException>(() => new SourceDatabase(missing));
        Assert.That(Directory.Exists(missing), Is.False, "guard must fire before RavenDB can auto-create the directory");
    }
}
