using Aion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aion.Tests.Services
{
    [TestClass]
    public class RobotFileNameBuilderTest
    {
        [TestMethod]
        public void Build_ValidParameters_FullPath()
        {
            var fileName = 
                new RobotFileNameBuilder()
                    .RobotDirectoryName(@"C:\foo\bar")
                    .Version("5.3.9")
                    .FileName("baz.qux")
                    .Build();

            Assert.AreEqual(@"C:\foo\bar\baz\v5.3.9\baz.qux", fileName);
        }
    }
}
