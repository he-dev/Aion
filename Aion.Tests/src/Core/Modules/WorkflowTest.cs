using Aion.Core.Modules;

namespace Aion.Tests.Core.Modules;

public class WorkflowTest
{
    [Fact]
    public async Task ThrowsWhenFileNotFound()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => Workflow.FromFile(@"C:\fake\path\to\workflow.json"));
    }

    [Fact]
    public async Task ThrowsWhenFileNameContainsInvalidCharacters()
    {
        await Assert.ThrowsAsync<WorkflowNameNotUrlSafeException>(() => Workflow.FromFile(@"workflows\invalid\contains-illegal-chars-$.json"));
    }

    [Fact]
    public async Task ThrowsWhenTemplatesUseUndefinedVariables()
    {
        var ex = await Assert.ThrowsAsync<Scriban.Syntax.ScriptRuntimeException>(() => Workflow.FromFile(@"workflows\invalid\uses-undefined-vars.json"));
        Assert.Equal("<input>(1,8) : error : Variable 'fakeVar' not found.", ex.Message);
    }

    [Fact]
    public async Task ThrowsWhenCronIsInvalid()
    {
        await Assert.ThrowsAsync<FormatException>(() => Workflow.FromFile(@"workflows\invalid\uses-invalid-cron.json"));
    }
}