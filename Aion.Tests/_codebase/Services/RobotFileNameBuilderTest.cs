using Aion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aion.Tests.Services
{
    [TestClass]
    public class RobotFileNameBuilderTest
    {
        [TestMethod]
        public void Build_RootedPath_TheSamePath()
        {
            var builder = new RobotDirectory((x, y) => new[] { "v2.0.1", "v1.0.5", "_v4.2.9" });
            Assert.AreEqual(@"C:\Robots\Robot.exe", builder.GetRobotFileName(@"C:\Test", @"C:\Robots\Robot.exe"));
        }

        [TestMethod]
        public void Build_RobotFileName_RootedPath()
        {
            var builder = new RobotDirectory((x, y) => new[] { "v2.0.1", "v1.0.5", "_v4.2.9" });
            Assert.AreEqual(@"C:\Robots\Robot\v2.0.1\Robot.exe", builder.GetRobotFileName(@"C:\Robots", @"Robot.exe"));
        }
    }
}
