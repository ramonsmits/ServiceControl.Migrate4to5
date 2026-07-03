namespace ServiceControl.Migrate4to5.DumpFormat;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public sealed class JsonlWriter(DumpPaths paths, string collectionName) : IDisposable
{
    readonly StreamWriter writer = new(paths.CollectionFile(collectionName), false, new UTF8Encoding(false));

    public long Count { get; private set; }

    public void Write(DumpLine line)
    {
        writer.WriteLine(line.ToJson());
        Count++;
    }

    public void Dispose() => writer.Dispose();
}

public static class Jsonl
{
    public static IEnumerable<DumpLine> Read(DumpPaths paths, string collectionName)
    {
        var file = paths.CollectionFile(collectionName);
        if (!File.Exists(file))
        {
            yield break;
        }
        foreach (var line in File.ReadLines(file))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return DumpLine.FromJson(line);
            }
        }
    }
}
