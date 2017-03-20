using System;
using Quartz;

namespace Aion.Services
{
    internal class TriggerFactory
    {
        public static ITrigger CreateTrigger(string name, string cronExpression, bool startImmediately)
        {
            var trigger = TriggerBuilder.Create()
                .WithIdentity(new TriggerKey(name))
                .StartAt(startImmediately ? DateTime.Now.AddDays(-1) : DateTime.Now)
                .WithCronSchedule(cronExpression)
                .Build();
            return trigger;
        }
    }
}
