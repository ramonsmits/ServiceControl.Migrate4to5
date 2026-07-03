namespace ServiceControl.Migrate4to5.Importer.Tests.Fixtures;

using ServiceControl.Migrate4to5.DumpFormat;

public static class FailedMessageFixture
{
    public const string UniqueId = "a3c5e1f0-0000-4000-8000-000000000001";

    // Two attempts; the second (2026-06-20) is most recent. Body of attempt 2 was stored
    // embedded in 4.x (attempt.Body); the transform must strip it and re-source from Bodies.
    public static DumpLine Line(int status = 1) => new()
    {
        Id = $"FailedMessages/{UniqueId}",
        Metadata = DumpJson.Parse("""{"Raven-Entity-Name":"FailedMessages","Raven-Clr-Type":"ServiceControl.MessageFailures.FailedMessage, ServiceControl","Raven-Last-Modified":"2026-06-20T10:00:00.0000000Z","@etag":"01000000-0000-0008-0000-0000000000D5"}"""),
        Document = DumpJson.Parse($$"""
        {
          "Id": "FailedMessages/{{UniqueId}}",
          "UniqueMessageId": "{{UniqueId}}",
          "Status": {{status}},
          "ProcessingAttempts": [
            {
              "AttemptedAt": "2026-06-19T09:00:00.0000000Z",
              "MessageId": "msg-1",
              "Headers": { "NServiceBus.MessageId": "msg-1", "NServiceBus.ContentType": "text/xml" },
              "Body": null,
              "MessageMetadata": { "ContentLength": 20, "ContentType": "text/xml", "BodyUrl": "/messages/msg-1/body" },
              "FailureDetails": { "AddressOfFailingEndpoint": "Sales", "TimeOfFailure": "2026-06-19T09:00:00.0000000Z" }
            },
            {
              "AttemptedAt": "2026-06-20T10:00:00.0000000Z",
              "MessageId": "msg-2",
              "Headers": { "NServiceBus.MessageId": "msg-2", "NServiceBus.ContentType": "text/xml" },
              "Body": "<Order>embedded body</Order>",
              "MessageMetadata": { "ContentLength": 28, "ContentType": "text/xml", "BodyUrl": "/messages/msg-2/body" },
              "FailureDetails": { "AddressOfFailingEndpoint": "Sales", "TimeOfFailure": "2026-06-20T10:00:00.0000000Z" }
            }
          ],
          "FailureGroups": [ { "Id": "group-1", "Title": "Sales grouping", "Type": "ExceptionTypeAndStackTraceFailureClassifier" } ]
        }
        """),
    };
}
