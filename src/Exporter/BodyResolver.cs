namespace ServiceControl.Migrate4to5.Exporter;

using System.Text;
using Newtonsoft.Json.Linq;

public static class BodyResolver
{
    public static (byte[] Content, string ContentType)? Resolve(JObject attempt, SourceDatabase source)
    {
        var contentType = attempt["MessageMetadata"]?.Value<string>("ContentType") ?? "text/xml";

        if (attempt.Value<string>("Body") is { } inline)
        {
            return (Encoding.UTF8.GetBytes(inline), contentType);
        }
        if (attempt["MessageMetadata"]?.Value<string>("Body") is { } metaBody)
        {
            return (Encoding.UTF8.GetBytes(metaBody), contentType);
        }

        var bodyId = attempt["Headers"]?.Value<string>("NServiceBus.MessageId") ?? attempt.Value<string>("MessageId");
        return bodyId is null ? null : source.GetLegacyAttachment(bodyId);
    }
}
