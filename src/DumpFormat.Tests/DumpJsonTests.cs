namespace ServiceControl.Migrate4to5.DumpFormat.Tests;

using System;
using NUnit.Framework;

[TestFixture]
public class DumpJsonTests
{
    [Test]
    public void ParseUtc_of_Z_string_yields_exact_utc_ticks()
    {
        var result = DumpJson.ParseUtc("2026-06-20T10:00:00.1234567Z");
        Assert.That(result.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(result, Is.EqualTo(new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc).AddTicks(1234567)));
    }

    [Test]
    public void ParseUtc_of_offset_string_converts_to_correct_utc_instant()
    {
        var result = DumpJson.ParseUtc("2026-06-19T09:00:00.0000000+02:00");
        Assert.That(result.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(result, Is.EqualTo(new DateTime(2026, 6, 19, 7, 0, 0, DateTimeKind.Utc)));
    }
}
