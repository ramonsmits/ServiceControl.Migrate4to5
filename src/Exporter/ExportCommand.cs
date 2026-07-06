namespace ServiceControl.Migrate4to5.Exporter;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using ServiceControl.Migrate4to5.DumpFormat;

public static class ExportCommand
{
    // Full detail and rationale for each of these lives in COLLECTIONS.md.
    const string NotMigratedCollections =
        "EventLogItems, RetryBatches, FailedMessageRetries, FailedMessageEdit, Archive/Unarchive operations, FailedErrorImports, ProcessedMessages, SagaSnapshots, Subscriptions, ReclassifyErrorSettings";

    public static int Run(CliArgs args, TextWriter output) =>
        Run(args, output, new SourceDatabase(args.Required("db-path")));

    public static int Run(CliArgs args, TextWriter output, SourceDatabase source, DateTime? utcNow = null)
    {
        using var _ = source;
        var now = utcNow ?? DateTime.UtcNow;
        TimeSpan? retention = args.Optional("error-retention") is { } r ? TimeSpan.Parse(r) : null;
        var requested = args.Optional("collections")?.Split(',').Select(n => n.Trim()).ToArray();
        var selected = requested ?? Collections.All.Select(s => s.Name).ToArray();

        if (requested is not null)
        {
            var unknown = requested.FirstOrDefault(name => Collections.All.All(s => !string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)));
            if (unknown is not null)
            {
                output.WriteLine($"ERROR: unknown collection '{unknown}'. Valid: {string.Join(", ", Collections.All.Select(s => s.Name))}");
                return 2;
            }
        }

        var outPath = args.Required("out");
        if (Directory.Exists(outPath) && Directory.EnumerateFileSystemEntries(outPath).Any())
        {
            output.WriteLine($"ERROR: output directory '{outPath}' is not empty");
            return 2;
        }

        var paths = new DumpPaths(outPath);
        var bodyStore = new BodyStore(paths);

        var manifest = new Manifest
        {
            ToolVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "dev",
            SourceDbPath = args.Optional("db-path") ?? "",
            ExportedAtUtc = now,
        };

        foreach (var spec in Collections.All.Where(s => selected.Any(name => string.Equals(name, s.Name, StringComparison.OrdinalIgnoreCase))))
        {
            var stats = new CollectionStats { Name = spec.Name };
            using (var writer = new JsonlWriter(paths, spec.Name))
            {
                foreach (var (id, metadata, document) in source.Stream(spec))
                {
                    if (spec.Name == "FailedMessages" && IsPastRetention(document, metadata, retention, now))
                    {
                        stats.SkippedPastRetention++;
                        continue;
                    }

                    var line = new DumpLine { Id = id, Metadata = metadata, Document = document };
                    if (spec.Name == "FailedMessages")
                    {
                        foreach (var attempt in (document["ProcessingAttempts"] as JArray ?? []).OfType<JObject>())
                        {
                            var bodyId = attempt["Headers"]?.Value<string>("NServiceBus.MessageId") ?? attempt.Value<string>("MessageId");
                            if (bodyId is null || line.Bodies.ContainsKey(bodyId))
                            {
                                continue;
                            }
                            if (BodyResolver.Resolve(attempt, source) is { } body)
                            {
                                var bodyRef = bodyStore.Store(body.Content, body.ContentType);
                                line.Bodies[bodyId] = bodyRef;
                                manifest.BodyCount++;
                                manifest.BodyTotalBytes += bodyRef.ContentLength;
                            }
                        }
                    }
                    writer.Write(line);
                }
                stats.ExportedCount = writer.Count;
            }
            manifest.Collections.Add(stats);
            output.WriteLine($"{spec.Name,-22} exported: {stats.ExportedCount,7}  skipped (retention): {stats.SkippedPastRetention}");
        }

        output.WriteLine($"Not migrated (by design, see COLLECTIONS.md): {NotMigratedCollections}");

        manifest.Save(paths); // written LAST — its presence marks the dump complete
        output.WriteLine($"Export finished: {manifest.BodyCount} bodies, {manifest.BodyTotalBytes / (1024 * 1024)} MB of body data.");
        return 0;
    }

    static bool IsPastRetention(JObject document, JObject metadata, TimeSpan? retention, DateTime utcNow)
    {
        if (retention is null || document.Value<int>("Status") is not (2 or 3 or 4))
        {
            return false;
        }
        var raw = metadata.Value<string>("Raven-Last-Modified") ?? metadata.Value<string>("Last-Modified");
        var lastModified = raw is null ? utcNow : DumpJson.ParseUtc(raw);
        return lastModified + retention.Value <= utcNow;
    }
}
