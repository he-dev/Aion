using System.Linq;
using Aion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aion.Tests.Services
{
    [TestClass]
    public class WmiTest
    {
        [TestMethod]
        public void IsRunning_EmptyArgumentsEmptyCommandLines_True()
        {
            var isRunning = Wmi.IsRunning("", Enumerable.Empty<string>().ToList());
            Assert.IsFalse(isRunning);
        }

        [TestMethod]
        public void IsRunning_EmptyArgumentsOneCommandLine_True()
        {
            var isRunning = Wmi.IsRunning("", new[] { "foo.exe" });
            Assert.IsTrue(isRunning);
        }

        [TestMethod]
        public void IsRunning_OneArgumentOneCommandLine_False()
        {
            var isRunning = Wmi.IsRunning("-baz", new[] { "foo.exe -bar" });
            Assert.IsFalse(isRunning);
        }

        [TestMethod]
        public void IsRunning_OneArgumentOneCommandLine_True()
        {
            var isRunning = Wmi.IsRunning("-bar", new[] { "foo.exe -bar" });
            Assert.IsTrue(isRunning);
        }
    }
}
