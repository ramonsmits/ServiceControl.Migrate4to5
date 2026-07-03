namespace ServiceControl.Migrate4to5.Importer;

using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ServiceControl.Migrate4to5.DumpFormat;

public class TransformedDoc
{
    public required string Id { get; init; }
    public required JObject Document { get; init; }
    public required JObject Metadata { get; init; }
    public BodyRef? Body { get; set; }
    public List<string> Warnings { get; } = [];
    public bool Skip { get; set; }
    public string? SkipReason { get; set; }
}
