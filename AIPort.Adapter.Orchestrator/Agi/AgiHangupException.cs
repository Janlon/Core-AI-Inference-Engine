namespace AIPort.Adapter.Orchestrator.Agi;

public sealed class AgiHangupException : Exception
{
    public AgiHangupException(string message)
        : base(message)
    {
    }

    public AgiHangupException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
