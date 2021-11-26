using System.Linq;
using Aion.Data;
using Aion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aion.Tests
{
    [TestClass]
    public class CronSchedulerTest
    {
        private CronScheduler _cronScheduler;

        [TestInitialize]
        public void TestInitialize()
        {
            _cronScheduler = new CronScheduler();
        }

        [TestMethod]
        public void ScheduleRobots_OneRobot_ScheduledOneRobot()
        {
            _cronScheduler.ScheduleRobots(new Workflow
            {
                Enabled = true,
                Name = @"c:\tests\test.exe",
                Schedule = "0/6 * * * * ? ",
                Steps =
                {
                    new Step { FileName = "robot-1.exe" }
                }

            });

            Assert.AreEqual("0/6 * * * * ?", _cronScheduler.GetJobSchedules("test").Single());
        }

        [TestMethod]
        public void ScheduleRobots_RobotSequence_ScheduledFirstRobot()
        {
            _cronScheduler.ScheduleRobots(new Workflow
            {
                Enabled = true,
                Name = @"c:\tests\test2.json",
                Schedule = "0/3 * * * * ?",
                Steps =
                {
                    new Step { FileName = "robot-1.exe" },
                    new Step { FileName = "robot-3.exe" },
                    new Step { FileName = "robot-2.exe" },
                }
            });

            Assert.IsTrue(_cronScheduler.TryGetProcessGroup("test2", out Workflow scheme));
            Assert.AreEqual(3, scheme.Count());
            Assert.AreEqual("0/3 * * * * ?", _cronScheduler.GetJobSchedules("test2").Single());
        }

        [TestMethod]
        public void ScheduleRobots_SingleRobotAndSequence_ScheduledTwoRobots()
        {
            var robotConfigs = new[]
            {
                new Workflow
                {
                    Enabled = true,
                    Name = @"c:\tests\test2.json",
                    Schedule = "0/6 * * * * ?",
                    Steps =
                    {
                        new Step { FileName = "robot-2.exe" },
                    }
                },
                new Workflow
                {
                    Enabled = true,
                    Name = @"c:\tests\test3.json",
                    Schedule = "0/6 * * * * ?",
                    Steps =
                    {
                        new Step { FileName = "robot-1.exe" },
                        new Step { FileName = "robot-3.exe" },
                    }
                }
            };
            _cronScheduler.ScheduleRobots(robotConfigs);

            Assert.IsTrue(_cronScheduler.TryGetProcessGroup("test2", out Workflow scheme2));
            Assert.AreEqual(1, scheme2.Count());

            Assert.IsTrue(_cronScheduler.TryGetProcessGroup("test3", out Workflow scheme3));
            Assert.AreEqual(2, scheme3.Count());
        }

        [TestMethod]
        public void RescheduleRobot_OneRobotChanged_RescheduledRobot()
        {
            _cronScheduler.ScheduleRobots(new Workflow
            {
                Enabled = true,
                Name = @"c:\tests\test.json",
                Schedule = "0/6 * * * * ?",
                Steps =
                {
                    new Step { FileName = "robot-1.exe" },
                }
            });

            _cronScheduler.ScheduleRobots(new Workflow
            {
                Enabled = true,
                Name = @"c:\tests\test.json",
                Schedule = "0/7 * * * * ?",
                Steps =
                {
                    new Step { FileName = "robot-1.exe" },
                }
            });

            //Assert.AreEqual(1, _cronScheduler.GetRobotScheme(robotConfigs.Single().FileName).Count());
            Assert.AreEqual(
                "0/7 * * * * ?",
                _cronScheduler.GetJobSchedules("test").Single()
            );
        }

        [TestMethod]
        public void RescheduleRobot_RobotGroupChanged()
        {
            var robotConfigs = new[]
            {
                new Workflow
                {
                    Enabled = true,
                    Name = @"c:\tests\test2.json",
                    Schedule = "0/9 * * * * ?",
                    Steps =
                    {
                        new Step { FileName = "robot-1.exe" },
                        new Step { FileName = "robot-3.exe" },
                        new Step { FileName = "robot-2.exe" },
                    }
                },
            };

            _cronScheduler.ScheduleRobots(robotConfigs);
            //Assert.AreEqual("0/7 * * * * ?", cronScheduler.Robots["test1"][0].Schedule);

            var robotConfigsChanged = new[]
            {
                new Workflow
                {
                    Enabled = true,
                    Name = @"c:\tests\test2.json",
                    Schedule = "0/8 * * * * ?",
                    Steps =
                    {
                        new Step { FileName = "robot-1.exe" },
                        new Step { FileName = "robot-3.exe" },
                        new Step { FileName = "robot-2.exe" },
                    }
                },
            };
            _cronScheduler.ScheduleRobots(robotConfigsChanged);

            Assert.AreEqual("0/8 * * * * ?", _cronScheduler.GetJobSchedules("test2").Single());
        }
    }
}
