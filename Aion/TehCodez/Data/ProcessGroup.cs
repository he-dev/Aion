using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Aion.Data
{
    [JsonObject]
    public class ProcessGroup : IEquatable<ProcessGroup>, IGrouping<string, Process>
    {
        #region IGrouping

        [JsonIgnore]
        public string Key => FileName;

        #endregion

        [DefaultValue(true)]
        public bool Enabled { get; set; }

        [JsonRequired]
        public string Schedule { get; set; }

        [JsonIgnore]
        public string FileName { get; set; }

        [DefaultValue(false)]
        public bool StartImmediately { get; set; }

        [JsonRequired]
        public List<Process> Processes { get; set; } = new List<Process>();

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
                    Enabled.GetHashCode(),
                    FileName.GetHashCode(),
                    Schedule.GetHashCode(),
                    StartImmediately.GetHashCode(),
                }
                .Concat(this.Select(x => x.GetHashCode()))
                .Aggregate(0, (current, next) => (current * 397) ^ next);
            }
        }

        public IEnumerator<Process> GetEnumerator() => Processes.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}
