namespace ServiceControl.Migrate4to5.Importer;

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
                "import" => ImportCommand.Run(cli, Console.Out),
                "verify" => VerifyCommand.Run(cli, Console.Out),
                _ => Fail($"Unknown verb '{cli.Verb}'. Usage: sc-migrate import|verify --in <dump> --url <ravendb5-url> [...]"),
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
