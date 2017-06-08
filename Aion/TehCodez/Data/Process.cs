using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;

namespace Aion.Data
{
    public class Process : IEquatable<Process>
    {
        [DefaultValue(true)]
        public bool Enabled { get; set; }

        [JsonRequired]
        public string FileName { get; set; }

        public string Arguments { get; set; }

        [DefaultValue(ProcessWindowStyle.Hidden)]
        public ProcessWindowStyle WindowStyle { get; set; }

        #region IEquatable

        public bool Equals(Process other)
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
            return Equals((Process)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return new[]
                {
                    FileName.GetHashCode(),
                    Enabled.GetHashCode(),
                    Arguments?.GetHashCode() ?? 0,
                    WindowStyle.GetHashCode()
                }.Aggregate(0, (current, next) => (current * 397) ^ next);
            }
        }

        #endregion
    }
}