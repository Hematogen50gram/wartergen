namespace Wartergen.App.Services;

public sealed class DrawTerrainWorkflowException : Exception
{
    public DrawTerrainWorkflowException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
