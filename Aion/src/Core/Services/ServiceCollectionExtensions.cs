using Aion.Core.Services.Commands;
using Aion.Core.Services.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Aion.Core.Services;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddWorkflowCommands()
        {
            return
                services
                    .AddScoped<GetWorkflowsInfo>()
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