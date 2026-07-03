namespace ServiceControl.Migrate4to5.Importer;

using System;
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

    internal static DateTime LastModified(DumpLine line) =>
        line.Metadata.Value<DateTime?>("Raven-Last-Modified")
        ?? line.Metadata.Value<DateTime?>("Last-Modified")
        ?? DateTime.UtcNow;
}
