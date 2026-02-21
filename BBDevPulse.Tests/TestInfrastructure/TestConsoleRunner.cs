using Spectre.Console;
using Spectre.Console.Testing;

namespace BBDevPulse.Tests.TestInfrastructure;

internal static class TestConsoleRunner
{
    public static async Task<string> RunAsync(Func<TestConsole, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var originalConsole = AnsiConsole.Console;
        var testConsole = new TestConsole();
        AnsiConsole.Console = testConsole;

        try
        {
            await action(testConsole).ConfigureAwait(false);
            return testConsole.Output;
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    public static string Run(Action<TestConsole> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var originalConsole = AnsiConsole.Console;
        var testConsole = new TestConsole();
        AnsiConsole.Console = testConsole;

        try
        {
            action(testConsole);
            return testConsole.Output;
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }
}
