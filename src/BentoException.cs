namespace Bento;

/// <summary>
/// A user-facing failure: printed as a clean error (plus hint), no stack trace.
/// </summary>
public class BentoException(string message, string? hint = null) : Exception(message)
{
    public string? Hint { get; } = hint;
}

/// <summary>
/// A build stage failure; the runner prints the tail of the stage's log file.
/// </summary>
public sealed class StageFailedException(string stage, string message, string? hint = null)
    : BentoException(message, hint)
{
    public string Stage { get; } = stage;
}
