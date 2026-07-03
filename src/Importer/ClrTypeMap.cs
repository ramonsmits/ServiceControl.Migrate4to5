namespace ServiceControl.Migrate4to5.Importer;

using System.Collections.Generic;

public static class ClrTypeMap
{
    // Short-form CLR type names (Raven client convention: no version/culture/token),
    // matching the 5.x types in the ServiceControl.Persistence assembly.
    static readonly Dictionary<string, (string ClrType, string Collection)> map = new()
    {
        ["FailedMessages"] = ("ServiceControl.MessageFailures.FailedMessage, ServiceControl.Persistence", "FailedMessages"),
        ["GroupComments"] = ("ServiceControl.MessageFailures.GroupComment, ServiceControl.Persistence", "GroupComments"),
        ["CustomChecks"] = ("ServiceControl.Contracts.CustomChecks.CustomCheck, ServiceControl.Persistence", "CustomChecks"),
        ["KnownEndpoints"] = ("ServiceControl.Persistence.KnownEndpoint, ServiceControl.Persistence", "KnownEndpoints"),
        ["RetryHistory"] = ("ServiceControl.Recoverability.RetryHistory, ServiceControl.Persistence", "RetryHistories"),
        ["MessageRedirects"] = ("ServiceControl.Persistence.MessageRedirects.MessageRedirectsCollection, ServiceControl.Persistence", "MessageRedirectsCollections"),
        ["NotificationsSettings"] = ("ServiceControl.Notifications.NotificationsSettings, ServiceControl.Persistence", "NotificationsSettings"),
    };

    public static string For(string collectionName) => map[collectionName].ClrType;
    public static string CollectionFor(string collectionName) => map[collectionName].Collection;
}
