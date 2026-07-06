namespace ServiceControl.Migrate4to5.Importer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ServiceControl.Migrate4to5.DumpFormat;

public static class ImportCommand
{
    public static int Run(CliArgs args, TextWriter output)
    {
        DumpPaths paths;
        Manifest manifest;
        try
        {
            paths = new DumpPaths(args.Required("in"));
            manifest = Manifest.Load(paths);
        }
        catch (InvalidDumpException e)
        {
            output.WriteLine($"ERROR: {e.Message}");
            return 1;
        }

        var retention = TimeSpan.Parse(args.Required("error-retention"));
        var batchSize = int.Parse(args.Optional("batch-size") ?? "128");
        var bodies = new BodyStore(paths);

        using var target = new RavenTarget(
            args.Required("url"), args.Optional("database") ?? "primary",
            args.Optional("cert"), args.Optional("cert-password"));

        output.WriteLine($"Importing dump from {paths.Root} (exported {manifest.ExportedAtUtc:u} from {manifest.SourceDbPath})");

        // Only import collections the manifest actually recorded — a stray .jsonl file left
        // over from an unrelated or partial export must not be picked up just because it's
        // physically present on disk.
        var manifestCollections = new HashSet<string>(manifest.Collections.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var spec in Collections.All.Where(s => manifestCollections.Contains(s.Name)))
        {
            long imported = 0, skippedExisting = 0, skippedRetention = 0, warnings = 0;
            foreach (var chunk in Jsonl.Read(paths, spec.Name).Chunk(batchSize))
            {
                var transformed = chunk
                    .Select(line => DocumentTransformer.Transform(line, spec.Name, retention, DateTime.UtcNow, bodies.Load))
                    .ToList();

                foreach (var w in transformed.SelectMany(t => t.Warnings))
                {
                    output.WriteLine($"WARN: {w}");
                    warnings++;
                }
                skippedRetention += transformed.Count(t => t.Skip);

                var result = target.ImportBatch(transformed, bodies);
                imported += result.Imported;
                skippedExisting += result.SkippedExisting;
            }
            output.WriteLine($"{spec.Name,-22} imported: {imported,7}  skipped (existing): {skippedExisting}  skipped (retention): {skippedRetention}  warnings: {warnings}");
        }

        output.WriteLine("Import finished.");
        return 0;
    }
}
