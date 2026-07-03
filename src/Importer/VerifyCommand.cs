namespace ServiceControl.Migrate4to5.Importer;

using System.IO;
using ServiceControl.Migrate4to5.DumpFormat;

public static class VerifyCommand
{
    public static int Run(CliArgs args, TextWriter output)
    {
        output.WriteLine("verify: not implemented yet");
        return 2;
    }
}
