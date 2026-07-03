namespace ServiceControl.Migrate4to5.DumpFormat;

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
}
