using System;
using Serilog.Core;
using Serilog.Events;

namespace Aion.Util.Flow.Serilog;

// util: Enriches log events by converting the TimeSpan into the precision specified by the select parameter.
public class EnrichesLogEventWithDuration(Func<TimeSpan, int> selectsTimeComponent, string propertyName = "Duration") : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Properties.TryGetValue(propertyName, out var value) && value is ScalarValue { Value: TimeSpan timespan })
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(propertyName, selectsTimeComponent(timespan)));
        }
    }
}