namespace ServiceControl.Migrate4to5.Importer;

using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using ServiceControl.Migrate4to5.DumpFormat;

public static class DocumentTransformer
{
    public static TransformedDoc Transform(DumpLine line, string collectionName, TimeSpan errorRetention, DateTime utcNow, Func<string, byte[]> loadBody)
    {
        if (collectionName == "FailedMessages")
        {
            return FailedMessageTransformer.Transform(line, errorRetention, utcNow, loadBody);
        }

        var doc = (JObject)line.Document.DeepClone();
        if (collectionName == "KnownEndpoints")
        {
            doc.Remove("HasTemporaryId");
        }

        return new TransformedDoc { Id = line.Id, Document = doc, Metadata = BuildMetadata(line, collectionName) };
    }

    internal static JObject BuildMetadata(DumpLine line, string collectionName) => new()
    {
        ["@collection"] = line.Metadata.Value<string>("Raven-Entity-Name") ?? ClrTypeMap.CollectionFor(collectionName),
        ["Raven-Clr-Type"] = ClrTypeMap.For(collectionName),
    };

    internal static DateTime LastModified(DumpLine line, DateTime utcNow)
    {
        var raw = line.Metadata.Value<string>("Raven-Last-Modified")
                  ?? line.Metadata.Value<string>("Last-Modified");
        return raw is null
            ? utcNow
            : ParseUtc(raw);
    }

    /// <summary>
    /// All date strings in dumps are parsed to genuine UTC ticks: explicit offsets
    /// are honored, offset-less values are treated as UTC. JToken.Value&lt;DateTime&gt;()
    /// must never be used for these — it converts via the host timezone (Kind=Local),
    /// which misorders instants across DST transitions.
    /// </summary>
    public static DateTime ParseUtc(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).UtcDateTime;
}
