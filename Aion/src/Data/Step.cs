using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Aion.Data
{
    [UsedImplicitly]
    public class Step
    {
        [DefaultValue(true)]
        public bool Enabled { get; set; }

        [JsonRequired]
        public string FileName { get; set; }

        public string Arguments { get; set; }

        [DefaultValue(ProcessWindowStyle.Hidden)]
        public ProcessWindowStyle WindowStyle { get; set; }
    }
}