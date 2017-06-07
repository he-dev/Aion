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
                new RobotScheme { FileName = "foo", Schedule = "1" },
                new RobotScheme { FileName = "bar", Schedule = "2" },
                new RobotScheme { FileName = "baz", Schedule = "3" },
            };

            var other = new[]
            {
                new RobotScheme { FileName = "bar", Schedule = "2" },
                new RobotScheme { FileName = "baz", Schedule = "4" },
                new RobotScheme { FileName = "qux", Schedule = "5" },
            };

            var result = current.Compare(other).ToList();
        }
    }
}
