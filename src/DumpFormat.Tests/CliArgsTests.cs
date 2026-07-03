namespace ServiceControl.Migrate4to5.DumpFormat.Tests;

using NUnit.Framework;

[TestFixture]
public class CliArgsTests
{
    [Test]
    public void Parses_verb_and_options()
    {
        var a = CliArgs.Parse(["import", "--url", "http://localhost:33334", "--in", "/tmp/dump"]);
        Assert.That(a.Verb, Is.EqualTo("import"));
        Assert.That(a.Required("url"), Is.EqualTo("http://localhost:33334"));
        Assert.That(a.Optional("cert"), Is.Null);
    }

    [Test]
    public void Missing_required_option_throws_with_option_name()
    {
        var a = CliArgs.Parse(["export", "--out", "x"]);
        var ex = Assert.Throws<CliArgsException>(() => a.Required("db-path"));
        Assert.That(ex!.Message, Does.Contain("--db-path"));
    }

    [Test]
    public void No_verb_throws() =>
        Assert.Throws<CliArgsException>(() => CliArgs.Parse([]));

    [Test]
    public void Option_without_value_throws() =>
        Assert.Throws<CliArgsException>(() => CliArgs.Parse(["export", "--out"]));
}
