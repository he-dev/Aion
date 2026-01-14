using Aion.Premise.Services.Commands;
using Aion.Premise.Services.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Aion.Premise.Services;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddWorkflowCommands()
        {
            return
                services
                    .AddScoped<GetWorkflows>()
                    .AddScoped<ScheduleWorkflow>()
                    .AddScoped<SynchronizeProfile>()
                    .AddScoped<ExecuteWorkflow>();
        }

        public IServiceCollection AddScheduleCommands()
        {
            return
                services
                    .AddScoped<GetProfileTriggers>();
        }

        public IServiceCollection AddDowntimeCommands()
        {
            return
                services
                    .AddScoped<GetDowntimes>()
                    .AddScoped<StartDowntime>()
                    .AddScoped<EndDowntime>();
        }
    }
}