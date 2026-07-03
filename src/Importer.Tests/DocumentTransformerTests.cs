namespace ServiceControl.Migrate4to5.Importer.Tests;

using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using ServiceControl.Migrate4to5.DumpFormat;

[TestFixture]
public class DocumentTransformerTests
{
    static readonly DateTime Now = new(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc);
    static readonly TimeSpan Retention = TimeSpan.FromDays(15);
    static byte[] NoBody(string sha) => throw new InvalidOperationException("no body expected");

    static DumpLine Line(string id, string entityName, string docJson) => new()
    {
        Id = id,
        Metadata = DumpJson.Parse($$"""{"Raven-Entity-Name":"{{entityName}}","Raven-Clr-Type":"Old.Type, OldAssembly","Last-Modified":"2026-07-01T00:00:00.0000000Z","@etag":"01000000-0000-0008-0000-00000000BEEF","Non-Authoritative-Information":false}"""),
        Document = DumpJson.Parse(docJson),
    };

    [Test]
    public void Rewrites_clr_type_and_collection_and_drops_35_system_keys()
    {
        var t = DocumentTransformer.Transform(Line("CustomChecks/guid1", "CustomChecks", """{"Status":1}"""), "CustomChecks", Retention, Now, NoBody);
        Assert.That(t.Skip, Is.False);
        Assert.That((string)t.Metadata["@collection"]!, Is.EqualTo("CustomChecks"));
        Assert.That((string)t.Metadata["Raven-Clr-Type"]!, Is.EqualTo("ServiceControl.Contracts.CustomChecks.CustomCheck, ServiceControl.Persistence"));
        Assert.That(t.Metadata.Properties(), Has.None.Matches<JProperty>(p => p.Name == "Raven-Entity-Name" || p.Name == "@etag" || p.Name == "Last-Modified" || p.Name == "Non-Authoritative-Information"));
    }

    [Test]
    public void KnownEndpoint_drops_HasTemporaryId()
    {
        var t = DocumentTransformer.Transform(Line("KnownEndpoints/guid1", "KnownEndpoints", """{"HostDisplayName":"h","Monitored":true,"HasTemporaryId":false}"""), "KnownEndpoints", Retention, Now, NoBody);
        Assert.That(t.Document.Property("HasTemporaryId"), Is.Null);
        Assert.That((bool)t.Document["Monitored"]!, Is.True);
    }

    [Test]
    public void Singleton_without_entity_name_gets_collection_from_map()
    {
        var line = Line("messageredirects", "MessageRedirectsCollections", """{"Redirects":[]}""");
        line.Metadata.Remove("Raven-Entity-Name");
        var t = DocumentTransformer.Transform(line, "MessageRedirects", Retention, Now, NoBody);
        Assert.That((string)t.Metadata["@collection"]!, Is.EqualTo("MessageRedirectsCollections"));
    }

    [Test]
    public void Non_failedmessage_docs_never_get_expires()
    {
        var t = DocumentTransformer.Transform(Line("CustomChecks/guid1", "CustomChecks", """{"Status":1}"""), "CustomChecks", Retention, Now, NoBody);
        Assert.That(t.Metadata.Property("@expires"), Is.Null);
    }
}
