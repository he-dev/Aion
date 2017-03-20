using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using System.IO;

namespace Aion.Data
{
    public class RobotScheme
    {
        [JsonRequired]
        public string Schedule { get; set; }

        [DefaultValue(true)]
        public bool Enabled { get; set; }

        [DefaultValue(false)]
        public bool StartImmediately { get; set; }

        [JsonRequired]
        public List<RobotInfo> Robots { get; set; } = new List<RobotInfo>();

        [JsonIgnore]
        public string FileName { get; set; }

        public override string ToString() => Path.GetFileNameWithoutExtension(FileName);

        public static implicit operator string(RobotScheme scheme) => scheme.ToString();
    }

    public class RobotInfo
    {
        [JsonRequired]
        public string FileName { get; set; }

        [DefaultValue(true)]
        public bool Enabled { get; set; }

        public string Arguments { get; set; }

        [DefaultValue(ProcessWindowStyle.Hidden)]
        public ProcessWindowStyle WindowStyle { get; set; }
    }
}
