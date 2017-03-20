using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aion.Extensions
{
    public static class StringExtensions
    {
        public static string DoubleQuote(this string value) => $"\"{value}\"";
    }
}
