namespace ServiceControl.Migrate4to5.Importer.Tests;

using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Raven.Embedded;

[SetUpFixture]
public class RavenTestServer
{
    static int dbCounter;
    public static string ServerUrl { get; private set; } = null!;

    [OneTimeSetUp]
    public void StartServer()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), "sc-migrate-tests", Guid.NewGuid().ToString("N"));
        EmbeddedServer.Instance.StartServer(new ServerOptions
        {
            DataDirectory = dataDir,
            ServerUrl = "http://127.0.0.1:0",
        });
        ServerUrl = EmbeddedServer.Instance.GetServerUriAsync().GetAwaiter().GetResult().AbsoluteUri.TrimEnd('/');
    }

    // Each test class gets its own database so tests stay independent
    public static string NewDatabase()
    {
        var name = $"test-{Interlocked.Increment(ref dbCounter)}";
        using var store = new Raven.Client.Documents.DocumentStore { Urls = [ServerUrl], Database = name };
        store.Initialize();
        store.Maintenance.Server.Send(new Raven.Client.ServerWide.Operations.CreateDatabaseOperation(
            new Raven.Client.ServerWide.DatabaseRecord(name)));
        return name;
    }
}
