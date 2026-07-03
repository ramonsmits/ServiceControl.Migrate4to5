namespace ServiceControl.Migrate4to5.DumpFormat;

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

public class InvalidDumpException(string message) : Exception(message);

public class CollectionStats
{
    public required string Name { get; init; }
    public long ExportedCount { get; set; }
    public long SkippedPastRetention { get; set; }
}

public class Manifest
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public string ToolVersion { get; set; } = "";
    public string SourceDbPath { get; set; } = "";
    public DateTime ExportedAtUtc { get; set; }
    public List<CollectionStats> Collections { get; } = [];
    public long BodyCount { get; set; }
    public long BodyTotalBytes { get; set; }

    static readonly JsonSerializerSettings jsonSettings = new() { DateTimeZoneHandling = DateTimeZoneHandling.Utc, Formatting = Formatting.Indented };

    public void Save(DumpPaths paths) =>
        File.WriteAllText(paths.ManifestPath, JsonConvert.SerializeObject(this, jsonSettings));

    public static Manifest Load(DumpPaths paths)
    {
        if (!File.Exists(paths.ManifestPath))
        {
            throw new InvalidDumpException($"No manifest found at {paths.ManifestPath} — the dump is incomplete or the path is wrong. A manifest is written only when an export finishes successfully.");
        }
        var manifest = JsonConvert.DeserializeObject<Manifest>(File.ReadAllText(paths.ManifestPath), jsonSettings)
                       ?? throw new InvalidDumpException("Manifest is empty");
        if (manifest.FormatVersion != CurrentFormatVersion)
        {
            throw new InvalidDumpException($"Dump format version {manifest.FormatVersion} is not supported by this tool (expected {CurrentFormatVersion})");
        }
        return manifest;
    }
}
