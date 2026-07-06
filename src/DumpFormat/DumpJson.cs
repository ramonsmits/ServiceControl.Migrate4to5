namespace ServiceControl.Migrate4to5.DumpFormat;

using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// JSON parsing that never reinterprets date-like strings — dumps must be
/// faithful snapshots, so all document/metadata JSON must go through here.
/// </summary>
public static class DumpJson
{
    public static JObject Parse(string json)
    {
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
        return JObject.Load(reader);
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
