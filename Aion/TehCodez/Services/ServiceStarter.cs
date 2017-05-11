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
        public static TService Start<TService>(params string[] args) where TService : ServiceBase, IService, new()
        {
            var service = new TService();

            if (Environment.UserInteractive && Debugger.IsAttached)
            {
                service.Start(args);
            }
            else
            {
                ServiceBase.Run(service);
            }

            return service;
        }
    }
}