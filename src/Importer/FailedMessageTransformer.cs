namespace ServiceControl.Migrate4to5.Importer;

using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using ServiceControl.Migrate4to5.DumpFormat;

public static class FailedMessageTransformer
{
    const int LargeObjectHeapThreshold = 85_000;
    static readonly Encoding strictUtf8 = new UTF8Encoding(true, true);

    public static TransformedDoc Transform(DumpLine line, TimeSpan errorRetention, DateTime utcNow, Func<string, byte[]> loadBody)
    {
        var doc = (JObject)line.Document.DeepClone();
        var metadata = DocumentTransformer.BuildMetadata(line, "FailedMessages");
        var result = new TransformedDoc { Id = line.Id, Document = doc, Metadata = metadata };

        // 1. Past-retention check against the *original* status, before normalization.
        // RetryIssued(3) normalizes to Unresolved(1) below, which never expires — so a
        // RetryIssued document past retention must be dropped here, or it would otherwise
        // survive forever as Unresolved instead of being cleaned up like 4.x would have done.
        var originalStatus = doc.Value<int>("Status");
        if (originalStatus is 2 or 3 or 4)
        {
            var expires = DocumentTransformer.LastModified(line, utcNow) + errorRetention;
            if (expires <= utcNow)
            {
                result.Skip = true;
                result.SkipReason = "past retention";
                return result;
            }
        }

        // 2. Status normalization: RetryIssued(3) → Unresolved(1)
        var status = originalStatus;
        if (status == 3)
        {
            status = 1;
            doc["Status"] = 1;
        }

        // 3. Expiry for Resolved(2)/Archived(4) (post-normalization, so RetryIssued — now
        // Unresolved — never gets one; the retention check above already excluded documents
        // whose expiry would be in the past)
        if (status is 2 or 4)
        {
            metadata["@expires"] = DocumentTransformer.LastModified(line, utcNow) + errorRetention;
        }

        var uniqueId = doc.Value<string>("UniqueMessageId");
        var attempts = (doc["ProcessingAttempts"] as JArray) ?? [];

        // 4. Strip 4.x inline bodies, rewrite BodyUrl to the 5.x unique-id form
        foreach (var attempt in attempts.OfType<JObject>())
        {
            attempt.Remove("Body");
            if (attempt["MessageMetadata"] is JObject mm)
            {
                mm.Remove("Body");
                mm["BodyUrl"] = $"/messages/{uniqueId}/body";
            }
        }

        // 5. Most recent attempt's body becomes the 5.x "body" attachment.
        // Must use DocumentTransformer.ParseUtc, not JObject.Value<DateTime>() — the latter
        // converts via the host timezone (Kind=Local), which misorders instants across DST
        // transitions and can silently select the wrong attempt's body.
        var latest = attempts.OfType<JObject>()
            .OrderBy(a => a.Value<string>("AttemptedAt") is { } s ? DocumentTransformer.ParseUtc(s) : DateTime.MinValue)
            .LastOrDefault();
        if (latest is null)
        {
            return result;
        }

        var bodyId = latest["Headers"]?.Value<string>("NServiceBus.MessageId") ?? latest.Value<string>("MessageId");
        if (bodyId is null || !line.Bodies.TryGetValue(bodyId, out var bodyRef))
        {
            // 4.x stores nothing at all for a genuinely empty body — ContentLength absent or 0
            // means there was never a body to capture, not a body that failed to migrate.
            var contentLength = latest["MessageMetadata"]?.Value<long?>("ContentLength") ?? 0;
            if (contentLength > 0)
            {
                result.Warnings.Add($"{line.Id}: no body in dump (bodyId={bodyId ?? "?"}) — importing without attachment");
            }
            return result;
        }

        result.Body = bodyRef;
        if (latest["MessageMetadata"] is JObject latestMeta)
        {
            latestMeta["ContentType"] = bodyRef.ContentType;
            latestMeta["ContentLength"] = bodyRef.ContentLength;

            // 6. MsgFullText for small, strictly-UTF-8 bodies (5.x full-text search default)
            if (bodyRef.ContentLength < LargeObjectHeapThreshold)
            {
                try
                {
                    latestMeta["MsgFullText"] = strictUtf8.GetString(loadBody(bodyRef.Sha256));
                }
                catch (Exception e) when (e is DecoderFallbackException or ArgumentException)
                {
                    // binary/non-text body — not indexed, same as 5.x ingestion
                }
            }
        }

        return result;
    }
}
