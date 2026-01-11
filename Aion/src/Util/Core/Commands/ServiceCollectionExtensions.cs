using Aion.Util.Core.Commands.Downtimes;
using Aion.Util.Core.Commands.Profiles;
using Aion.Util.Core.Commands.Schedules;
using Aion.Util.Core.Commands.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace Aion.Util.Core.Commands;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowCommands(this IServiceCollection services)
    {
        return
            services
                .AddScoped<GetWorkflows>()
                .AddScoped<ScheduleWorkflow>()
                .AddScoped<SynchronizeProfile>()
                .AddScoped<ExecuteWorkflow>();
    }

    public static IServiceCollection AddScheduleCommands(this IServiceCollection services)
    {
        return
            services
                .AddScoped<GetWorkflowsTriggers>();
    }

    public static IServiceCollection AddDowntimeCommands(this IServiceCollection services)
    {
        return
            services
                .AddScoped<GetDowntimes>()
                .AddScoped<StartDowntime>()
                .AddScoped<EndDowntime>();
    }
}