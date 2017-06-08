using System;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Process = Aion.Data.Process;

namespace Aion.Tests.Data
{
    [TestClass]
    public class ProcessTest
    {
        [TestMethod]
        public void GetHashCode_SameObjects_SameHashCodes()
        {
            var process1 = new Process { FileName = "foo", Arguments = "bar", Enabled = false, WindowStyle = ProcessWindowStyle.Maximized };
            var process2 = new Process { FileName = "foo", Arguments = "bar", Enabled = false, WindowStyle = ProcessWindowStyle.Maximized };
            Assert.IsTrue(process1.GetHashCode() > 0);
            Assert.IsTrue(process2.GetHashCode() > 0);
            Assert.IsTrue(process1.GetHashCode() == process2.GetHashCode());
        }

        [TestMethod]
        public void GetHashCode_DifferentObjects_DifferentHashCodes()
        {
            var process1 = new Process { FileName = "foo", Arguments = "bar", Enabled = false, WindowStyle = ProcessWindowStyle.Maximized };
            var process2 = new Process { FileName = "bar", Arguments = "baz", Enabled = true, WindowStyle = ProcessWindowStyle.Maximized };
            Assert.IsTrue(process1.GetHashCode() > 0);
            Assert.IsTrue(process2.GetHashCode() > 0);
            Assert.IsFalse(process1.GetHashCode() == process2.GetHashCode());
        }

        [TestMethod]
        public void Equals_SameObjects_True()
        {
            var process1 = new Process { FileName = "foo", Arguments = "bar", Enabled = false, WindowStyle = ProcessWindowStyle.Maximized };
            var process2 = new Process { FileName = "foo", Arguments = "bar", Enabled = false, WindowStyle = ProcessWindowStyle.Maximized };
            Assert.AreEqual(process1, process2);
        }

        [TestMethod]
        public void Equals_DifferentObjects_False()
        {
            var process1 = new Process { FileName = "foo", Arguments = "bar", Enabled = false, WindowStyle = ProcessWindowStyle.Maximized };
            var process2 = new Process { FileName = "bar", Arguments = "baz", Enabled = true, WindowStyle = ProcessWindowStyle.Maximized };
            Assert.AreNotEqual(process1, process2);
        }
    }
}
