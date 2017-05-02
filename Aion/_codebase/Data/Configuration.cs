using System.ComponentModel.DataAnnotations;

// ReSharper disable once CheckNamespace
namespace Aion.Data.Configuration
{
    internal class Global
    {
        [Required]
        public string Environment { get; set; }

        [Required]
        public string RobotsDirectoryName { get; set; }

        [Required]
        public string RobotConfigUpdaterSchedule { get; set; }
    }    
}
