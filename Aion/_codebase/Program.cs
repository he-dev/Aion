using System;
using Aion.Data;
using Aion.Services;
using Reusable.Logging;
using SmartConfig.DataStores.AppConfig;
using Aion.Data.Configs;

namespace Aion
{
    internal static class Program
    {
        public const string InstanceName = "Aion";
        public const string InstanceVersion = "4.0.0";

        private static readonly ILogger Logger;

        static Program()
        {
            InitializeLogging();
            Logger = LoggerFactory.CreateLogger(nameof(Program));
            InitializeConfiguraiton();
        }

        private static void Main()
        {
            //Logger.Info().MessageFormat("*** {Name} v{Version} ***", new { Name = InstanceName, Version = InstanceVersion }).Log();

            CronService.Start();

            if (Environment.UserInteractive)
            {
                Console.ReadKey();
            }
        }

        private static void InitializeLogging()
        {
            Reusable.Logging.NLog.Tools.LayoutRenderers.InvariantPropertiesLayoutRenderer.Register();
            Reusable.Logging.NLog.Tools.DatabaseTargetQueryGenerator.GenerateInsertQueries();

            Reusable.Logging.Logger.ComputedProperties.Add(new Reusable.Logging.ComputedProperties.AppSetting(name: "Environment", key: $"{InstanceName}.Environment"));
            Reusable.Logging.Logger.ComputedProperties.Add(new Reusable.Logging.ComputedProperties.ElapsedSeconds());
            Reusable.Logging.Logger.ComputedProperties.Add(new Reusable.Logging.ComputedProperties.ElapsedHours());

            Reusable.Logging.LoggerFactory.Initialize<Reusable.Logging.Adapters.NLogFactory>();
        }

        private static void InitializeConfiguraiton()
        {
            //var loadSettingsLogger = Logger.Info().Message("Config loaded.").StartStopwatch();
            try
            {
                SmartConfig.Configuration.Builder()
                    .From(new AppSettingsStore())
                    .Select(typeof(AppSettingsConfig));

                //SmartConfig.Configuration.Builder()
                //    .From(new SQLiteStore("name=configdb", builder => builder.Column("Environment", DbType.String, 50)))
                //    .Where("Environment", AppSettingsConfig.Environment)
                //    .Select(typeof(MainConfig));                
            }
            catch (Exception ex)
            {
                //loadSettingsLogger.Error().Exception(ex);
                throw;
            }
            finally
            {
                //loadSettingsLogger.Log();
            }

        }
    }
}
