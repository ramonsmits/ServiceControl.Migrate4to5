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
public class ExportCommandTests
{
    EmbeddableDocumentStore store = null!;
    string outDir = null!;

    [SetUp]
    public void SetUp()
    {
        store = new EmbeddableDocumentStore { RunInMemory = true };
        store.Initialize();
        outDir = Path.Combine(Path.GetTempPath(), "sc-export-test-" + Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        store.Dispose();
        if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
    }

    void PutFailedMessage(string uniqueId, int status, string lastModified, string? embeddedBody = null, string? attachmentBodyId = null)
    {
        var attemptBody = embeddedBody != null ? $"\"{embeddedBody}\"" : "null";
        var doc = RavenJObject.Parse($$"""
        {
          "UniqueMessageId": "{{uniqueId}}", "Status": {{status}},
          "ProcessingAttempts": [{
            "AttemptedAt": "2026-06-20T10:00:00.0000000Z", "MessageId": "m-{{uniqueId}}",
            "Headers": {"NServiceBus.MessageId": "m-{{uniqueId}}"},
            "Body": {{attemptBody}},
            "MessageMetadata": {"ContentType": "text/xml", "ContentLength": 10}
          }]
        }
        """);
        store.DatabaseCommands.Put($"FailedMessages/{uniqueId}", null, doc,
            new RavenJObject { { "Raven-Entity-Name", "FailedMessages" }, { "Raven-Last-Modified", lastModified } });

        if (attachmentBodyId != null)
        {
#pragma warning disable 618
            store.DatabaseCommands.PutAttachment($"messagebodies/{attachmentBodyId}", null,
                new MemoryStream(Encoding.UTF8.GetBytes("<attached/>")), new RavenJObject { { "ContentType", "text/xml" } });
#pragma warning restore 618
        }
    }

    int Run(out string output, string? retention = null, DateTime? utcNow = null, string? collections = null)
    {
        string[] args = ["export", "--db-path", "ignored-by-test-ctor", "--out", outDir];
        if (retention != null) args = [.. args, "--error-retention", retention];
        if (collections != null) args = [.. args, "--collections", collections];
        var writer = new StringWriter();
        var exit = ExportCommand.Run(CliArgs.Parse(args), writer, new SourceDatabase(store), utcNow);
        output = writer.ToString();
        return exit;
    }

    [Test]
    public void Exports_docs_bodies_and_manifest()
    {
        PutFailedMessage("aaa", 1, "2026-07-01T00:00:00.0000000Z", embeddedBody: "<inline/>");
        PutFailedMessage("bbb", 1, "2026-07-01T00:00:00.0000000Z", attachmentBodyId: "m-bbb");

        var exit = Run(out var output);
        Assert.That(exit, Is.Zero);
        Assert.That(output, Does.Contain("Not migrated (by design"));

        var paths = new DumpPaths(outDir);
        var manifest = Manifest.Load(paths);
        Assert.That(manifest.Collections.Single(c => c.Name == "FailedMessages").ExportedCount, Is.EqualTo(2));
        Assert.That(manifest.BodyCount, Is.EqualTo(2));

        var lines = Jsonl.Read(paths, "FailedMessages").ToList();
        var bodyStore = new BodyStore(paths);
        var inline = lines.Single(l => l.Id == "FailedMessages/aaa");
        Assert.That(Encoding.UTF8.GetString(bodyStore.Load(inline.Bodies["m-aaa"].Sha256)), Is.EqualTo("<inline/>"));
        var attached = lines.Single(l => l.Id == "FailedMessages/bbb");
        Assert.That(Encoding.UTF8.GetString(bodyStore.Load(attached.Bodies["m-bbb"].Sha256)), Is.EqualTo("<attached/>"));
    }

    [Test]
    public void Retention_filter_skips_old_resolved_but_keeps_unresolved()
    {
        // RavenDB always stamps Raven-Last-Modified/Last-Modified with the real write time on
        // Put (Raven.Database.Actions.DocumentActions strips any client-supplied value before
        // storing), so the "2026-01-01" passed here is discarded — the documents' real
        // last-modified is "now". Retention is exercised by advancing the "as of" clock instead
        // of trying to backdate the documents.
        PutFailedMessage("old-archived", 4, "2026-01-01T00:00:00.0000000Z");
        PutFailedMessage("old-unresolved", 1, "2026-01-01T00:00:00.0000000Z");

        Run(out _, retention: "15.00:00:00", utcNow: DateTime.UtcNow.AddDays(20));

        var paths = new DumpPaths(outDir);
        var ids = Jsonl.Read(paths, "FailedMessages").Select(l => l.Id).ToList();
        Assert.That(ids, Does.Contain("FailedMessages/old-unresolved"));
        Assert.That(ids, Does.Not.Contain("FailedMessages/old-archived"));
        Assert.That(Manifest.Load(paths).Collections.Single(c => c.Name == "FailedMessages").SkippedPastRetention, Is.EqualTo(1));
    }

    [Test]
    public void Crash_before_manifest_leaves_invalid_dump()
    {
        // Simulated by just not running export: a dump dir without manifest must not load
        Assert.Throws<InvalidDumpException>(() => Manifest.Load(new DumpPaths(outDir)));
    }

    [Test]
    public void Unknown_collection_name_fails_fast()
    {
        var exit = Run(out var output, collections: "FailedMessages, NotARealCollection ");

        Assert.That(exit, Is.EqualTo(2));
        Assert.That(output, Does.Contain("ERROR: unknown collection 'NotARealCollection'"));
        Assert.That(output, Does.Contain("Valid:"));
        Assert.That(Directory.Exists(outDir), Is.False, "nothing should have been opened/created before validation");
    }
}
