namespace ServiceControl.Migrate4to5.DumpFormat.Tests;

using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

[TestFixture]
public class DumpLineTests
{
    string root = null!;

    [SetUp] public void SetUp() => root = Directory.CreateTempSubdirectory("jsonl").FullName;
    [TearDown] public void TearDown() => Directory.Delete(root, recursive: true);

    [Test]
    public void Line_round_trips_including_bodies()
    {
        var line = new DumpLine
        {
            Id = "FailedMessages/abc",
            Metadata = JObject.Parse("""{"Raven-Entity-Name":"FailedMessages"}"""),
            Document = JObject.Parse("""{"Status":1,"UniqueMessageId":"abc"}"""),
        };
        line.Bodies["msg-1"] = new BodyRef { Sha256 = new string('a', 64), ContentType = "text/xml", ContentLength = 10 };

        var parsed = DumpLine.FromJson(line.ToJson());
        Assert.That(parsed.Id, Is.EqualTo("FailedMessages/abc"));
        Assert.That((int)parsed.Document["Status"]!, Is.EqualTo(1));
        Assert.That(parsed.Bodies["msg-1"].ContentType, Is.EqualTo("text/xml"));
    }

    [Test]
    public void ToJson_is_single_line() =>
        Assert.That(new DumpLine
        {
            Id = "x",
            Metadata = [],
            Document = JObject.Parse("""{"A":{"B":1}}"""),
        }.ToJson(), Does.Not.Contain("\n"));

    [Test]
    public void Writer_then_reader_round_trips_and_counts()
    {
        var paths = new DumpPaths(root);
        using (var w = new JsonlWriter(paths, "CustomChecks"))
        {
            w.Write(new DumpLine { Id = "CustomChecks/1", Metadata = [], Document = [] });
            w.Write(new DumpLine { Id = "CustomChecks/2", Metadata = [], Document = [] });
            Assert.That(w.Count, Is.EqualTo(2));
        }

        var lines = Jsonl.Read(paths, "CustomChecks").ToList();
        Assert.That(lines.Select(l => l.Id), Is.EqualTo(new[] { "CustomChecks/1", "CustomChecks/2" }));
    }

    [Test]
    public void Reading_missing_collection_yields_empty() =>
        Assert.That(Jsonl.Read(new DumpPaths(root), "Nope"), Is.Empty);
}
