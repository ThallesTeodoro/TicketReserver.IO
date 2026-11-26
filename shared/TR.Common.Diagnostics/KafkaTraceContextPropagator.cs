namespace TicketReserver.Diagnostics;

using System.Diagnostics;
using System.Text;
using Confluent.Kafka;

public static class KafkaTraceContextPropagator
{
    public const string TraceParentHeader = "traceparent";
    public const string TraceStateHeader = "tracestate";

    public static void Inject(Headers headers, Activity? activity)
    {
        if (activity == null) return;

        var traceparent = activity.Id;
        if (!string.IsNullOrEmpty(traceparent))
        {
            headers.Remove(TraceParentHeader);
            headers.Add(TraceParentHeader, Encoding.UTF8.GetBytes(traceparent));
        }

        var tracestate = activity.TraceStateString;
        if (!string.IsNullOrEmpty(tracestate))
        {
            headers.Remove(TraceStateHeader);
            headers.Add(TraceStateHeader, Encoding.UTF8.GetBytes(tracestate));
        }
    }

    public static Activity? ExtractAndStartActivity(
        Headers? headers,
        string activityName,
        ActivityKind kind = ActivityKind.Consumer)
    {
        ActivityContext parentContext = default;

        if (headers != null)
        {
            string? traceparent = null;
            string? tracestate = null;

            if (headers.TryGetLastBytes(TraceParentHeader, out var tpBytes) && tpBytes != null)
            {
                traceparent = Encoding.UTF8.GetString(tpBytes);
            }

            if (headers.TryGetLastBytes(TraceStateHeader, out var tsBytes) && tsBytes != null)
            {
                tracestate = Encoding.UTF8.GetString(tsBytes);
            }

            if (!string.IsNullOrEmpty(traceparent) && ActivityContext.TryParse(traceparent, tracestate, out var parsedContext))
            {
                parentContext = parsedContext;
            }
        }

        return TicketReserverDiagnostics.KafkaActivitySource.StartActivity(
            activityName,
            kind,
            parentContext);
    }
}
