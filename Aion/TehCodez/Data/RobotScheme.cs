using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Aion.Data
{
    public class RobotScheme : IEquatable<RobotScheme>
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

        #region IEquatable<RobotScheme>

        public bool Equals(RobotScheme other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return
                string.Equals(FileName, other.FileName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Schedule, other.Schedule, StringComparison.OrdinalIgnoreCase) &&
                Enabled == other.Enabled &&
                StartImmediately == other.StartImmediately &&
                Robots.SequenceEqual(other.Robots);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RobotScheme)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return new[]
                {
                    FileName.ToLowerInvariant().GetHashCode(),
                    StartImmediately.GetHashCode(),
                    Schedule.ToLowerInvariant().GetHashCode(),
                    Robots.GetHashCode(),
                    Enabled.GetHashCode(),
                }.Aggregate(0, (current, next) => (current * 397) ^ next);
            }
        }

        #endregion    
    }

    public class RobotInfo : IEquatable<RobotInfo>
    {
        [JsonRequired]
        public string FileName { get; set; }

        [DefaultValue(true)]
        public bool Enabled { get; set; }

        public string Arguments { get; set; }

        [DefaultValue(ProcessWindowStyle.Hidden)]
        public ProcessWindowStyle WindowStyle { get; set; }

        #region IEquatable

        public bool Equals(RobotInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return
                Enabled == other.Enabled &&
                WindowStyle == other.WindowStyle &&
                string.Equals(FileName, other.FileName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Arguments, other.Arguments, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RobotInfo)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return new[]
                {
                    FileName.ToLowerInvariant().GetHashCode(),
                    Enabled.GetHashCode(),
                    Arguments?.ToLowerInvariant().GetHashCode() ?? 0,
                    WindowStyle.GetHashCode()
                }.Aggregate(0, (current, next) => (current * 397) ^ next);
            }
        }

        #endregion
    }
}
