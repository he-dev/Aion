using System;
using Aion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aion.Tests.Services
{
    [TestClass]
    public class RobotPathTest
    {
        [TestMethod]
        public void CreateMainDirectoryName_DirectoryAndFileName_Name()
        {
            Assert.AreEqual(
                @"c:\foo\bar\baz",
                RobotPath.Combine(@"c:\foo\bar", @"baz.exe"));
        }        
    }
}
