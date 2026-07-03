namespace ServiceControl.Migrate4to5.DumpFormat;

public class CollectionSpec
{
    public required string Name { get; init; }
    public string? IdPrefix { get; init; }
    public string? FixedId { get; init; }
}

public static class Collections
{
    // Import order: FailedMessages first (largest; fail fast), singletons last.
    public static readonly CollectionSpec[] All =
    [
        new() { Name = "FailedMessages", IdPrefix = "FailedMessages/" },
        new() { Name = "GroupComments", IdPrefix = "GroupComment/" },
        new() { Name = "CustomChecks", IdPrefix = "CustomChecks/" },
        new() { Name = "KnownEndpoints", IdPrefix = "KnownEndpoints/" },
        new() { Name = "RetryHistory", FixedId = "RetryOperations/History" },
        new() { Name = "MessageRedirects", FixedId = "messageredirects" },
        new() { Name = "NotificationsSettings", FixedId = "NotificationsSettings/All" },
    ];
}
