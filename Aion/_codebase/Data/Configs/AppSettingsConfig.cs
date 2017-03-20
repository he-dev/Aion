using System.ComponentModel.DataAnnotations;
using SmartConfig.Data.Annotations;

namespace Aion.Data.Configs
{
    [SmartConfig]
    [SettingName(Program.InstanceName)]
    static class AppSettingsConfig
    {
        [Required]
        static public string Environment { get; set; }

        public static class Jobs
        {
            public static class RobotConfigUpdater
            {
                [Required]
                public static string Schedule { get; set; }
            }
        }

        public static class Paths
        {
            [Required]
            public static string RobotsDirectoryName { get; set; }
        }
    }
}
