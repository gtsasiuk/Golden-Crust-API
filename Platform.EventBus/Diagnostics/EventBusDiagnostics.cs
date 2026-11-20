using System.Diagnostics;

namespace Platform.EventBus.Diagnostics;

public static class EventBusDiagnostics
{
    public const string SourceName =
        "GoldenCrust.Platform.EventBus";

    public static readonly ActivitySource ActivitySource =
        new(SourceName);
}
