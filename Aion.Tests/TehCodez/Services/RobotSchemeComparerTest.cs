using System;
using System.Linq;
using Aion.Data;
using Aion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aion.Tests.Services
{
    [TestClass]
    public class RobotSchemeComparerTest
    {
        [TestMethod]
        public void TestMethod1()
        {
            var current = new[]
            {
                new ProcessGroup { FileName = "foo", Schedule = "1" },
                new ProcessGroup { FileName = "bar", Schedule = "2" },
                new ProcessGroup { FileName = "baz", Schedule = "3" },
            };

            var other = new[]
            {
                new ProcessGroup { FileName = "bar", Schedule = "2" },
                new ProcessGroup { FileName = "baz", Schedule = "4" },
                new ProcessGroup { FileName = "qux", Schedule = "5" },
            };

            var result = current.Compare(other).ToList();
        }
    }
}
