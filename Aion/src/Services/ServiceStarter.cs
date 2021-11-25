using System;
using System.Diagnostics;
using System.ServiceProcess;

namespace Aion.Services
{
    public interface IService
    {
        void Start(params string[] args);
    }

    internal class ServiceStarter
    {
        public static void Start<TService>(TService service, params string[] args) where TService : ServiceBase, IService, new()
        {
            if (Environment.UserInteractive && Debugger.IsAttached)
            {
                service.Start(args);
            }
            else
            {
                ServiceBase.Run(service);
            }
        }
    }
}