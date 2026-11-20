using System.Diagnostics;

namespace Platform.EventBus.Diagnostics;

public static class EventBusDiagnostics
{
    public const string SourceName = "Platform.Messaging";

    public static readonly ActivitySource ActivitySource =
        new(SourceName);
}
