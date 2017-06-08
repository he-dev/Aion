using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Aion.Data
{
    [JsonObject]
    public class ProcessGroup : IEquatable<ProcessGroup>, IGrouping<string, ProcessInfo>
    {
        #region IGrouping

        [JsonIgnore]
        public string Key => FileName;

        #endregion

        [JsonRequired]
        public string Schedule { get; set; }

        [DefaultValue(true)]
        public bool Enabled { get; set; }

        [DefaultValue(false)]
        public bool StartImmediately { get; set; }

        [JsonRequired]
        public List<ProcessInfo> Items { get; set; } = new List<ProcessInfo>();

        [JsonIgnore]
        public string FileName { get; set; }


        public override string ToString() => Path.GetFileNameWithoutExtension(FileName);

        public static implicit operator string(ProcessGroup scheme) => scheme.ToString();

        #region IEquatable<ProcessGroup>

        public bool Equals(ProcessGroup other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return
                string.Equals(FileName, other.FileName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Schedule, other.Schedule, StringComparison.OrdinalIgnoreCase) &&
                Enabled == other.Enabled &&
                StartImmediately == other.StartImmediately &&
                this.SequenceEqual(other);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ProcessGroup)obj);
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
                    Enabled.GetHashCode(),
                }
                .Concat(this.Select(x => x.GetHashCode()))
                .Aggregate(0, (current, next) => (current * 397) ^ next);
            }
        }

        public IEnumerator<ProcessInfo> GetEnumerator() => Items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }

    public class ProcessInfo : IEquatable<ProcessInfo>
    {
        [JsonRequired]
        public string FileName { get; set; }

        [DefaultValue(true)]
        public bool Enabled { get; set; }

        public string Arguments { get; set; }

        [DefaultValue(ProcessWindowStyle.Hidden)]
        public ProcessWindowStyle WindowStyle { get; set; }

        #region IEquatable

        public bool Equals(ProcessInfo other)
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
            return Equals((ProcessInfo)obj);
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
