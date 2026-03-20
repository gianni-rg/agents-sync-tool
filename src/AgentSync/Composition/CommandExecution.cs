using AgentSync.Presentation;

namespace AgentSync.Composition;

internal static class CommandExecution
{
    public static int Execute(Func<int> action, IConsoleRenderer renderer)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            renderer.ShowError(ex.Message);
            return 1;
        }
    }
}
