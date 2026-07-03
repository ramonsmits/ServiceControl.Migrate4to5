namespace ServiceControl.Migrate4to5.DumpFormat;

using System;
using System.Collections.Generic;

public class CliArgsException(string message) : Exception(message);

public class CliArgs
{
    readonly Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase);

    public string Verb { get; private set; } = "";

    public static CliArgs Parse(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("--"))
        {
            throw new CliArgsException("Usage: <verb> [--option value ...]");
        }

        var result = new CliArgs { Verb = args[0] };
        for (var i = 1; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--") || i + 1 >= args.Length)
            {
                throw new CliArgsException($"Option '{args[i]}' is missing a value");
            }
            result.options[args[i].Substring(2)] = args[i + 1];
        }
        return result;
    }

    public string Required(string name) =>
        options.TryGetValue(name, out var v) ? v : throw new CliArgsException($"Required option --{name} is missing");

    public string? Optional(string name) => options.TryGetValue(name, out var v) ? v : null;
}
