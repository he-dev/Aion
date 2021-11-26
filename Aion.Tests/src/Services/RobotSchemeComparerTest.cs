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
                new Workflow { Name = "foo", Schedule = "1" },
                new Workflow { Name = "bar", Schedule = "2" },
                new Workflow { Name = "baz", Schedule = "3" },
            };

            var other = new[]
            {
                new Workflow { Name = "bar", Schedule = "2" },
                new Workflow { Name = "baz", Schedule = "4" },
                new Workflow { Name = "qux", Schedule = "5" },
            };

            var result = current.Compare(other).ToList();
        }
    }
}
