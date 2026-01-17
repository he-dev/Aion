using Aion.Context.Services.Commands;
using Aion.Context.Services.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Aion.Context.Services;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddWorkflowCommands()
        {
            return
                services
                    .AddScoped<GetWorkflows>()
                    .AddScoped<SynchronizeWorkflow>()
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