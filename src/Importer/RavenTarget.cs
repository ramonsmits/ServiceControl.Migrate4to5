namespace ServiceControl.Migrate4to5.Importer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json.Linq;
using Raven.Client.Documents;
using Raven.Client.Documents.Attachments;
using Raven.Client.Documents.Commands.Batches;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Operations.Attachments;
using Sparrow.Json.Parsing;
using ServiceControl.Migrate4to5.DumpFormat;

public class ImportResult
{
    public long Imported { get; set; }
    public long SkippedExisting { get; set; }
}

public sealed class RavenTarget : IDisposable
{
    readonly DocumentStore store;

    public RavenTarget(string url, string database, string? certPath, string? certPassword)
    {
        store = new DocumentStore { Urls = [url], Database = database };
        if (certPath is not null)
        {
            store.Certificate = new X509Certificate2(certPath, certPassword);
        }
        store.Conventions.SaveEnumsAsIntegers = true;
        store.Initialize();
    }

    public ImportResult ImportBatch(IReadOnlyList<TransformedDoc> docs, BodyStore bodies)
    {
        var result = new ImportResult();
        using var session = store.OpenSession();
        var streams = new List<MemoryStream>();
        try
        {
            foreach (var doc in docs.Where(d => !d.Skip))
            {
                if (session.Advanced.Exists(doc.Id))
                {
                    result.SkippedExisting++;
                    continue;
                }

                var djv = ToDynamicJsonValue(doc.Document);
                djv["@metadata"] = ToDynamicJsonValue(doc.Metadata);
                session.Advanced.Defer(new PutCommandData(doc.Id, null, djv));

                if (doc.Body is not null)
                {
                    var bodyStream = new MemoryStream(bodies.Load(doc.Body.Sha256));
                    streams.Add(bodyStream);
                    session.Advanced.Defer(new PutAttachmentCommandData(doc.Id, "body", bodyStream, doc.Body.ContentType, changeVector: null));
                }

                result.Imported++;
            }

            session.SaveChanges();
        }
        finally
        {
            streams.ForEach(s => s.Dispose());
        }
        return result;
    }

    // PutCommandData in this client version only accepts a DynamicJsonValue (the blittable-accepting overload,
    // PutCommandDataWithBlittableJson, is internal to Raven.Client). Rather than round-tripping the document through
    // a BlittableJsonReaderObject (which turned out to only carry *modifications* relative to a source, not the full
    // content, when wrapped via `new DynamicJsonValue(blittable)`), build the DynamicJsonValue/DynamicJsonArray tree
    // directly from the JObject so the full document is actually written.
    static DynamicJsonValue ToDynamicJsonValue(JObject obj)
    {
        var djv = new DynamicJsonValue();
        foreach (var prop in obj.Properties())
        {
            djv[prop.Name] = ToRavenValue(prop.Value);
        }
        return djv;
    }

    static object? ToRavenValue(JToken token) => token.Type switch
    {
        JTokenType.Object => ToDynamicJsonValue((JObject)token),
        JTokenType.Array => new DynamicJsonArray(((JArray)token).Select(ToRavenValue)),
        JTokenType.Null or JTokenType.Undefined => null,
        // Date/Integer/Float/Boolean carry their CLR value (DateTime, long, double, bool) - the RavenDB blittable
        // writer knows how to serialize these natively (same as when the client serializes a POCO via Store()).
        _ => ((JValue)token).Value
    };

    public Dictionary<string, long> CollectionCounts() =>
        store.Maintenance.Send(new GetCollectionStatisticsOperation()).Collections;

    public bool AttachmentExists(string docId, out long size)
    {
        using var att = store.Operations.Send(new GetAttachmentOperation(docId, "body", AttachmentType.Document, null));
        size = att?.Details.Size ?? 0;
        return att is not null;
    }

    public JObject? LoadRaw(string docId)
    {
        using var session = store.OpenSession();
        var cmd = new Raven.Client.Documents.Commands.GetDocumentsCommand(docId, includes: null, metadataOnly: false);
        session.Advanced.RequestExecutor.Execute(cmd, session.Advanced.Context);
        var doc = cmd.Result?.Results?.FirstOrDefault() as Sparrow.Json.BlittableJsonReaderObject;
        return doc is null ? null : DumpJson.Parse(doc.ToString());
    }

    public void Dispose() => store.Dispose();
}
