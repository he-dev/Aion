using Aion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aion.Tests
{
    [TestClass]
    public class RobotFileNameResolverTest
    {
        [TestMethod]
        public void Resolve_MultipleVersions_PathWithLatestVersion()
        {
            var resolver = new RobotFileNameResolver((path, searchPattern) => new []
            {
                @"C:\foo\bar\v3.4.1",
                @"C:\foo\bar\v5.0.1",
                @"C:\foo\bar\v1.2.0",
            });

            var fileName = resolver.Resolve(@"C:\foo", "bar.exe");

            Assert.AreEqual(@"C:\foo\bar\v5.0.1\bar.exe", fileName);
        }

        [TestMethod]
        public void GetLatestVersion_MultipleVersions_Latest()
        {
            var latestVersion = RobotFileNameResolver.GetLatestVersion(new[] { "v2.0.0", "v2.0.5", "v2.0.1" });
            Assert.AreEqual(@"2.0.5", latestVersion);
        }

        [TestMethod]
        public void GetLatestVersion_MultipleVersionsWithDisabled_Latest()
        {
            var latestVersion = RobotFileNameResolver.GetLatestVersion(new[] { "v2.0.0", "_v2.0.5", "v2.0.1" });
            Assert.AreEqual(@"2.0.1", latestVersion);
        }

        [TestMethod]
        public void GetLatestVersion_NoValidVersions_null()
        {
            var latestVersion = RobotFileNameResolver.GetLatestVersion(new[] { "_v2.0.0", "_v2.0.5", "abc" });
            Assert.IsNull(latestVersion);
        }
    }
}
