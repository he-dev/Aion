using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aion.Data;
using Aion.Extensions;
using Newtonsoft.Json;
using Reusable;
using Reusable.Logging;

namespace Aion.Services
{
    internal class SchemeReader
    {
        private static readonly ILogger Logger;

        static SchemeReader()
        {
            Logger = LoggerFactory.CreateLogger(nameof(SchemeReader));
        }

        public static IEnumerable<RobotScheme> ReadSchemes(string path) => GetSchemeFileNames(path).Select(ReadeScheme).Where(Conditional.IsNotNull);

        public static string[] GetSchemeFileNames(string path) => Directory.GetFiles(path, "Aion.Schemes.*.json");

        public static RobotScheme ReadeScheme(string fileName)
        {
            try
            {
                var json = File.ReadAllText(fileName);
                var robotScheme = JsonConvert.DeserializeObject<RobotScheme>(json, new JsonSerializerSettings
                {
                    DefaultValueHandling = DefaultValueHandling.Populate
                });
                robotScheme.FileName = fileName;
                return robotScheme;
            }
            catch (Exception ex)
            {
                LogEntry.New().Error().Exception(ex).Message($"Error scheduling {fileName.DoubleQuote()}").Log(Logger);
                return null;
            }
        }
    }
}