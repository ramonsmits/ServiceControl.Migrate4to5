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

    int Run(out string output, string? retention = null)
    {
        string[] args = ["export", "--db-path", "ignored-by-test-ctor", "--out", outDir];
        if (retention != null) args = [.. args, "--error-retention", retention];
        var writer = new StringWriter();
        var exit = ExportCommand.Run(CliArgs.Parse(args), writer, new SourceDatabase(store));
        output = writer.ToString();
        return exit;
    }

    [Test]
    public void Exports_docs_bodies_and_manifest()
    {
        PutFailedMessage("aaa", 1, "2026-07-01T00:00:00.0000000Z", embeddedBody: "<inline/>");
        PutFailedMessage("bbb", 1, "2026-07-01T00:00:00.0000000Z", attachmentBodyId: "m-bbb");

        var exit = Run(out _);
        Assert.That(exit, Is.Zero);

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
        PutFailedMessage("old-archived", 4, "2026-01-01T00:00:00.0000000Z");
        PutFailedMessage("old-unresolved", 1, "2026-01-01T00:00:00.0000000Z");

        Run(out _, retention: "15.00:00:00");

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
}
