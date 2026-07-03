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

    static readonly JsonSerializerSettings settings = new() { DateParseHandling = DateParseHandling.None };

    public string ToJson() => JsonConvert.SerializeObject(this, Formatting.None, settings);

    public static DumpLine FromJson(string json) =>
        JsonConvert.DeserializeObject<DumpLine>(json, settings)
        ?? throw new InvalidDumpException("Empty JSONL line");
}
