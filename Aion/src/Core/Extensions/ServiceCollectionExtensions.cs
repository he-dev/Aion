using Aion.Core.Commands.Downtimes;
using Aion.Core.Commands.Profiles;
using Aion.Core.Commands.Schedules;
using Aion.Core.Commands.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace Aion.Core.Extensions;

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
                .AddScoped<GetWorkflowSchedules>();
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