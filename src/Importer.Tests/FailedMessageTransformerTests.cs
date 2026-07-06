namespace ServiceControl.Migrate4to5.Importer.Tests;

using System;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using ServiceControl.Migrate4to5.DumpFormat;
using ServiceControl.Migrate4to5.Importer.Tests.Fixtures;

[TestFixture]
public class FailedMessageTransformerTests
{
    static readonly DateTime Now = new(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc);
    static readonly TimeSpan Retention = TimeSpan.FromDays(30);
    static readonly byte[] BodyBytes = Encoding.UTF8.GetBytes("<Order>embedded body</Order>");

    static DumpLine LineWithBody(int status = 1)
    {
        var line = FailedMessageFixture.Line(status);
        line.Bodies["msg-2"] = new BodyRef { Sha256 = new string('b', 64), ContentType = "text/xml", ContentLength = BodyBytes.Length };
        return line;
    }

    static TransformedDoc Transform(DumpLine line) =>
        FailedMessageTransformer.Transform(line, Retention, Now, _ => BodyBytes);

    [Test]
    public void RetryIssued_normalizes_to_Unresolved_without_expiry()
    {
        var t = Transform(LineWithBody(status: 3));
        Assert.That((int)t.Document["Status"]!, Is.EqualTo(1));
        Assert.That(t.Metadata.Property("@expires"), Is.Null);
    }

    [Test]
    public void Archived_gets_expires_from_last_modified_plus_retention()
    {
        var t = Transform(LineWithBody(status: 4));
        Assert.That(t.Metadata.Value<DateTime>("@expires"), Is.EqualTo(new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public void Resolved_past_retention_is_skipped()
    {
        var t = FailedMessageTransformer.Transform(LineWithBody(status: 2), TimeSpan.FromDays(5), Now, _ => BodyBytes);
        Assert.That(t.Skip, Is.True);
        Assert.That(t.SkipReason, Does.Contain("retention"));
    }

    [Test]
    public void RetryIssued_past_retention_is_skipped()
    {
        // RetryIssued(3) would otherwise normalize to Unresolved(1) — which never expires — so
        // the retention check must happen against the *original* status, before normalization.
        var t = FailedMessageTransformer.Transform(LineWithBody(status: 3), TimeSpan.FromDays(5), Now, _ => BodyBytes);
        Assert.That(t.Skip, Is.True);
        Assert.That(t.SkipReason, Does.Contain("retention"));
    }

    [Test]
    public void Unresolved_never_expires_and_keeps_status()
    {
        var t = Transform(LineWithBody(status: 1));
        Assert.That((int)t.Document["Status"]!, Is.EqualTo(1));
        Assert.That(t.Metadata.Property("@expires"), Is.Null);
    }

    [Test]
    public void Inline_bodies_are_stripped_and_body_urls_rewritten_to_unique_id()
    {
        var t = Transform(LineWithBody());
        foreach (var attempt in (JArray)t.Document["ProcessingAttempts"]!)
        {
            Assert.That(((JObject)attempt).Property("Body"), Is.Null);
            Assert.That(((JObject)attempt["MessageMetadata"]!).Property("Body"), Is.Null);
            Assert.That((string)attempt["MessageMetadata"]!["BodyUrl"]!, Is.EqualTo($"/messages/{FailedMessageFixture.UniqueId}/body"));
        }
    }

    [Test]
    public void Most_recent_attempts_body_becomes_the_attachment()
    {
        var t = Transform(LineWithBody());
        Assert.That(t.Body, Is.Not.Null);
        Assert.That(t.Body!.ContentType, Is.EqualTo("text/xml"));
        var latest = (JObject)((JArray)t.Document["ProcessingAttempts"]!)[1];
        Assert.That((long)latest["MessageMetadata"]!["ContentLength"]!, Is.EqualTo(BodyBytes.Length));
    }

    [Test]
    public void Small_utf8_body_gets_MsgFullText_on_latest_attempt()
    {
        var t = Transform(LineWithBody());
        var latest = (JObject)((JArray)t.Document["ProcessingAttempts"]!)[1];
        Assert.That((string)latest["MessageMetadata"]!["MsgFullText"]!, Is.EqualTo("<Order>embedded body</Order>"));
    }

    [Test]
    public void Binary_body_gets_no_MsgFullText()
    {
        var line = LineWithBody();
        var t = FailedMessageTransformer.Transform(line, Retention, Now, _ => [0xFF, 0xFE, 0x00, 0x01]);
        var latest = (JObject)((JArray)t.Document["ProcessingAttempts"]!)[1];
        Assert.That(((JObject)latest["MessageMetadata"]!).Property("MsgFullText"), Is.Null);
    }

    [Test]
    public void Missing_body_ref_warns_but_does_not_fail()
    {
        var t = FailedMessageTransformer.Transform(FailedMessageFixture.Line(), Retention, Now, _ => BodyBytes);
        Assert.That(t.Skip, Is.False);
        Assert.That(t.Body, Is.Null);
        Assert.That(t.Warnings, Has.Some.Contains("no body"));
    }

    [Test]
    public void Empty_body_message_imports_without_warning()
    {
        // 4.x stores nothing at all for a genuinely empty body (ContentLength 0/absent) — this
        // is not a body that failed to migrate, so it must not be reported as a warning.
        var line = FailedMessageFixture.Line();
        var latest = (JObject)((JArray)line.Document["ProcessingAttempts"]!)[1];
        latest["Body"] = null;
        ((JObject)latest["MessageMetadata"]!)["ContentLength"] = 0;

        var t = FailedMessageTransformer.Transform(line, Retention, Now, _ => BodyBytes);

        Assert.That(t.Warnings, Is.Empty);
        Assert.That(t.Body, Is.Null);
    }

    [Test]
    public void Expires_math_is_timezone_independent()
    {
        // Raven-Last-Modified fixture value is 2026-06-20T10:00:00.0000000Z; retention 30d
        var t = Transform(LineWithBody(status: 4));
        var expires = t.Metadata.Value<DateTime>("@expires");
        Assert.That(expires.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(expires, Is.EqualTo(new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public void Attempt_ordering_is_dst_safe()
    {
        // Two attempts 15 minutes apart in real time, straddling the 2026-10-25
        // Europe/Amsterdam DST fallback (01:00Z): naive local conversion misorders them.
        var line = FailedMessageFixture.Line();
        var attempts = (JArray)line.Document["ProcessingAttempts"]!;
        attempts[0]["AttemptedAt"] = "2026-10-25T00:50:00.0000000Z"; // earlier instant
        attempts[1]["AttemptedAt"] = "2026-10-25T01:05:00.0000000Z"; // later instant — must win
        line.Bodies["msg-2"] = new BodyRef { Sha256 = new string('b', 64), ContentType = "text/xml", ContentLength = BodyBytes.Length };
        var t = FailedMessageTransformer.Transform(line, TimeSpan.FromDays(365), Now, _ => BodyBytes);
        Assert.That(t.Body, Is.Not.Null, "the later attempt (msg-2) must be selected");
    }
}
