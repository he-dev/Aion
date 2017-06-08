using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aion.Data;
using Aion.Extensions;
using Newtonsoft.Json;
using Reusable.Extensions;
using Reusable.Logging;

namespace Aion.Services
{
    internal class SchemeReader
    {
        private static readonly ILogger Logger;

        public const string SearchPattern = "*.json";

        static SchemeReader()
        {
            Logger = LoggerFactory.CreateLogger(nameof(SchemeReader));
        }

        public static IEnumerable<ProcessGroup> ReadProcessGroups(string path) => GetProcessGroupFileNames(path).Select(ReadProcessGroup).Where(Conditional.IsNotNull);

        public static string[] GetProcessGroupFileNames(string path) => Directory.GetFiles(path, SearchPattern);

        public static ProcessGroup ReadProcessGroup(string fileName)
        {
            try
            {
                var json = File.ReadAllText(fileName);
                var robotScheme = JsonConvert.DeserializeObject<ProcessGroup>(json, new JsonSerializerSettings
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