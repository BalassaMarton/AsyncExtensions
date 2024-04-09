namespace AsyncExtensions;

public readonly struct AsyncLockOptions
{
    /// <summary>
    /// Enables recursion without passing an <see cref="AsyncLockToken"/>.
    /// Note that automatic recursion comes with a performance penalty.
    /// </summary>
    public bool AllowReentrancy { get; init; }
}