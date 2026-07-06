namespace ServiceControl.Migrate4to5.Exporter;

using System;
using ServiceControl.Migrate4to5.DumpFormat;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var cli = CliArgs.Parse(args);
            return cli.Verb switch
            {
                "export" => ExportCommand.Run(cli, Console.Out),
                _ => Fail($"Unknown verb '{cli.Verb}'. Usage: sc-migrate-export export --db-path <ravendb35-path> --out <dump-dir> [...]"),
            };
        }
        catch (CliArgsException e)
        {
            return Fail(e.Message);
        }
    }

    static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 2;
    }
}
