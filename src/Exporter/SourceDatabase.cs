#nullable disable // Raven 3.5 API predates NRT
namespace ServiceControl.Migrate4to5.Exporter;

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Raven.Client.Embedded;
using ServiceControl.Migrate4to5.DumpFormat;

public sealed class SourceDatabase : IDisposable
{
    readonly EmbeddableDocumentStore store;

    public SourceDatabase(string dbPath)
        : this(CreateStore(dbPath))
    {
    }

    static EmbeddableDocumentStore CreateStore(string dbPath)
    {
        if (!Directory.Exists(dbPath))
        {
            throw new InvalidOperationException($"Database path not found: {dbPath}");
        }

        return new EmbeddableDocumentStore
        {
            DataDirectory = dbPath,
            UseEmbeddedHttpServer = false,
            EnlistInDistributedTransactions = false,
        };
    }

    internal SourceDatabase(EmbeddableDocumentStore store)
    {
        this.store = store;
        if (store.WasDisposed == false && store.DatabaseCommands == null)
        {
            store.Conventions.SaveEnumsAsIntegers = true;
            store.Initialize();
        }
    }

    public IEnumerable<(string Id, JObject Metadata, JObject Document)> Stream(CollectionSpec spec)
    {
        if (spec.FixedId != null)
        {
            var doc = store.DatabaseCommands.Get(spec.FixedId);
            if (doc != null)
            {
                yield return (doc.Key, DumpJson.Parse(doc.Metadata.ToString()), DumpJson.Parse(doc.DataAsJson.ToString()));
            }
            yield break;
        }

        using var enumerator = store.DatabaseCommands.StreamDocs(startsWith: spec.IdPrefix, pageSize: int.MaxValue);
        while (enumerator.MoveNext())
        {
            var raw = enumerator.Current;
            var metadata = (Raven.Json.Linq.RavenJObject)raw["@metadata"];
            var id = metadata.Value<string>("@id");
            raw.Remove("@metadata");
            yield return (id, DumpJson.Parse(metadata.ToString()), DumpJson.Parse(raw.ToString()));
        }
    }

    public (byte[] Content, string ContentType)? GetLegacyAttachment(string bodyId)
    {
#pragma warning disable 618
        var attachment = store.DatabaseCommands.GetAttachment("messagebodies/" + bodyId);
#pragma warning restore 618
        if (attachment == null)
        {
            return null;
        }

        using var stream = attachment.Data();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var contentType = attachment.Metadata.Value<string>("ContentType") ?? "text/xml";
        return (ms.ToArray(), contentType);
    }

    public void Dispose() => store.Dispose();
}
