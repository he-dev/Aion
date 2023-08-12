using AionApi.Models;
using AionApi.Services;
using AionApi.Utilities;
using Reusable;
using Reusable.Wiretap.Services;
using Telerik.JustMock;
using Telerik.JustMock.Helpers;

namespace AionApi.Tests;

public class WorkflowRunnerTest
{
    [Fact]
    public async Task CanInterpolateFileNameVariables()
    {
        var logger = LoggerBuilder.CreateDefault().Build();
        var asyncProcess = Mock.Create<IAsyncProcess>();
        var directories = new EnumerateDirectoriesFunc(_ => new[] { "" });

        Mock
            .Arrange(() => asyncProcess.StartAsync(Arg.Is(@"c:\foo\bar\baz.qux"), Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<string?>(), Arg.IsAny<int>()))
            .ReturnsAsync(() => new AsyncProcess.Result(1))
            .OccursOnce();

        var runner = new WorkflowProcess(logger, asyncProcess, directories);
        var results = await runner.Start(new Workflow
        {
            Name = "foo",
            Variables =
            {
                ["xxx"] = "bar"
            },
            Commands =
            {
                new Workflow.Command { FileName = @"c:\foo\{xxx}\baz.qux" }
            }
        });

        Mock.Assert(asyncProcess);
        Assert.Equal(1, results.First().ExitCode);
    }
    
    [Fact]
    public async Task CanBreakWhenOneCommandFails()
    {
        var logger = LoggerBuilder.CreateDefault().Build();
        var asyncProcess = Mock.Create<IAsyncProcess>();
        var directories = new EnumerateDirectoriesFunc(_ => new[] { "" });

        var exitCode = 0;
        Mock
            .Arrange(() => asyncProcess.StartAsync(Arg.IsAny<string>(), Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<string?>(), Arg.IsAny<int>()))
            .ReturnsAsync(() => new AsyncProcess.Result(exitCode++))
            .Occurs(2);

        var runner = new WorkflowProcess(logger, asyncProcess, directories);
        var results = await runner.Start(new Workflow
        {
            Name = "foo",
            Commands =
            {
                new Workflow.Command { FileName = @"c:\foo\bar\one.qux" },
                new Workflow.Command { FileName = @"c:\foo\bar\two.qux"},
                new Workflow.Command { FileName = @"c:\foo\bar\tre.qux", DependsOnPrevious = true },
            }
        });

        Mock.Assert(asyncProcess);
        Assert.Equal(0, results[0].ExitCode);
        Assert.Equal(1, results[1].ExitCode);
    }
    
    [Fact]
    public async Task CanRunAllCommandsDespiteFailure()
    {
        var logger = LoggerBuilder.CreateDefault().Build();
        var asyncProcess = Mock.Create<IAsyncProcess>();
        var directories = new EnumerateDirectoriesFunc(_ => new[] { "" });

        var exitCode = 0;
        Mock
            .Arrange(() => asyncProcess.StartAsync(Arg.IsAny<string>(), Arg.IsAny<IEnumerable<string>>(), Arg.IsAny<string?>(), Arg.IsAny<int>()))
            .ReturnsAsync(() => new AsyncProcess.Result(exitCode++))
            .Occurs(3);

        var runner = new WorkflowProcess(logger, asyncProcess, directories);
        var results = await runner.Start(new Workflow
        {
            Name = "foo",
            Commands =
            {
                new Workflow.Command { FileName = @"c:\foo\bar\one.qux" },
                new Workflow.Command { FileName = @"c:\foo\bar\two.qux"},
                new Workflow.Command { FileName = @"c:\foo\bar\tre.qux" },
            }
        });

        Mock.Assert(asyncProcess);
        Assert.Equal(0, results[0].ExitCode);
        Assert.Equal(1, results[1].ExitCode);
        Assert.Equal(2, results[2].ExitCode);
    }
}