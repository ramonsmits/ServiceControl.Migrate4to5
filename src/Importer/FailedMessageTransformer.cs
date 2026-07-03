namespace ServiceControl.Migrate4to5.Importer;

using System;
using ServiceControl.Migrate4to5.DumpFormat;

public static class FailedMessageTransformer
{
    public static TransformedDoc Transform(DumpLine line, TimeSpan errorRetention, DateTime utcNow, Func<string, byte[]> loadBody) =>
        throw new NotImplementedException("Task 7");
}
