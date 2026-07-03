namespace ServiceControl.Migrate4to5.Importer;

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using ServiceControl.Migrate4to5.DumpFormat;

public static class VerifyCommand
{
    public static int Run(CliArgs args, TextWriter output)
    {
        var paths = new DumpPaths(args.Required("in"));
        var manifest = Manifest.Load(paths);
        var retention = TimeSpan.Parse(args.Required("error-retention"));
        var samples = int.Parse(args.Optional("samples") ?? "100");
        var random = new Random(int.Parse(args.Optional("seed") ?? "20260703"));
        var bodies = new BodyStore(paths);
        var problems = 0;

        using var target = new RavenTarget(
            args.Required("url"), args.Optional("database") ?? "primary",
            args.Optional("cert"), args.Optional("cert-password"));

        var counts = target.CollectionCounts();
        foreach (var spec in Collections.All)
        {
            var exported = manifest.Collections.FirstOrDefault(c => c.Name == spec.Name)?.ExportedCount ?? 0;
            counts.TryGetValue(ClrTypeMap.CollectionFor(spec.Name), out var actual);
            output.WriteLine($"{spec.Name,-22} exported: {exported,7}  in target: {actual,7}");

            var sampled = Jsonl.Read(paths, spec.Name)
                .ToList()
                .OrderBy(_ => random.Next()).Take(samples);

            foreach (var line in sampled)
            {
                var expected = DocumentTransformer.Transform(line, spec.Name, retention, DateTime.UtcNow, bodies.Load);
                var raw = target.LoadRaw(line.Id);

                if (expected.Skip)
                {
                    continue; // legitimately absent (past retention)
                }
                if (raw is null)
                {
                    output.WriteLine($"PROBLEM: {line.Id} missing from target");
                    problems++;
                    continue;
                }

                raw.Remove("@metadata");
                if (!JToken.DeepEquals(expected.Document, raw))
                {
                    output.WriteLine($"PROBLEM: {line.Id} differs from expected transform");
                    problems++;
                }

                if (expected.Body is not null)
                {
                    if (!target.AttachmentExists(line.Id, out var size) || size != expected.Body.ContentLength)
                    {
                        output.WriteLine($"PROBLEM: {line.Id} body attachment missing or wrong size");
                        problems++;
                    }
                }
            }
        }

        output.WriteLine(problems == 0 ? "VERIFY PASSED" : $"VERIFY FAILED ({problems} problems)");
        return problems == 0 ? 0 : 1;
    }
}
