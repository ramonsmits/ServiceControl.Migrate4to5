namespace ServiceControl.Migrate4to5.DumpFormat;

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class DumpLine
{
    public required string Id { get; init; }
    public required JObject Metadata { get; init; }
    public required JObject Document { get; init; }
    public Dictionary<string, BodyRef> Bodies { get; init; } = [];

    public string ToJson() => JsonConvert.SerializeObject(this, Formatting.None);

    public static DumpLine FromJson(string json) =>
        JsonConvert.DeserializeObject<DumpLine>(json)
        ?? throw new InvalidDumpException("Empty JSONL line");
}
