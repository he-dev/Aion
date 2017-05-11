using System;
using Aion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aion.Tests.Services
{
    [TestClass]
    public class RobotDirectoryTest
    {
        [TestMethod]
        public void CreateMainDirectoryName_DirectoryAndFileName_Name()
        {
            Assert.AreEqual(
                @"c:\foo\bar\baz",
                RobotDirectory.CreateMainDirectoryName(@"c:\foo\bar", @"baz.exe"));
        }

        [TestMethod]
        public void GetLatestVersion_MiscVersions_LatestVersion()
        {
            var paths = new[]
            {
                @"c:\foo\bar\baz\v2.0.1",
                @"c:\foo\bar\baz\v1.0.5",
                @"c:\foo\bar\baz\qux",
                @"c:\foo\bar\baz\v4.2.9",
            };

            Assert.AreEqual(@"4.2.9", paths.GetLatestVersion());
        }

        [TestMethod]
        public void GetLatestVersion_DisabledVersion_LatestVersion()
        {
            var paths = new[]
            {
                @"c:\foo\bar\baz\v2.0.1",
                @"c:\foo\bar\baz\v1.0.5",
                @"c:\foo\bar\baz\_v4.2.9"
            };

            Assert.AreEqual(@"2.0.1", paths.GetLatestVersion());
        }

        [TestMethod]
        public void GetLatestVersion_NoValidVersion_LatestVersion()
        {
            var paths = new[]
            {
                @"c:\foo\bar\baz\qux",
                @"c:\foo\bar\baz\_v4.2.9"
            };

            Assert.IsNull(paths.GetLatestVersion());
        }
    }
}
