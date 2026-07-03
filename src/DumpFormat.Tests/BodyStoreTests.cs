namespace ServiceControl.Migrate4to5.DumpFormat.Tests;

using System.IO;
using System.Text;
using NUnit.Framework;

[TestFixture]
public class BodyStoreTests
{
    string root = null!;
    BodyStore store = null!;

    [SetUp]
    public void SetUp()
    {
        root = Directory.CreateTempSubdirectory("bodystore").FullName;
        store = new BodyStore(new DumpPaths(root));
    }

    [TearDown] public void TearDown() => Directory.Delete(root, recursive: true);

    [Test]
    public void Store_and_load_round_trips()
    {
        var bytes = Encoding.UTF8.GetBytes("<xml>hello</xml>");
        var r = store.Store(bytes, "text/xml");
        Assert.That(r.Sha256, Has.Length.EqualTo(64));
        Assert.That(r.ContentLength, Is.EqualTo(bytes.Length));
        Assert.That(store.Load(r.Sha256), Is.EqualTo(bytes));
    }

    [Test]
    public void Identical_content_is_deduplicated()
    {
        var bytes = Encoding.UTF8.GetBytes("same");
        var a = store.Store(bytes, "text/xml");
        var b = store.Store(bytes, "text/xml");
        Assert.That(b.Sha256, Is.EqualTo(a.Sha256));
        Assert.That(Directory.GetFiles(Path.Combine(root, "bodies")), Has.Length.EqualTo(1));
    }

    [Test]
    public void Load_of_missing_body_throws() =>
        Assert.Throws<InvalidDumpException>(() => store.Load(new string('0', 64)));
}
