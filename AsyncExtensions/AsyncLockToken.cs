namespace AsyncExtensions;

public readonly struct AsyncLockToken : IDisposable
{
    internal readonly AsyncLockTokenSource? Source;
    internal readonly short Version;

    internal AsyncLockToken(AsyncLockTokenSource source, short version)
    {
        Source = source;
        Version = version;
    }

    public void Dispose()
    {
        Source?.Release(in this);
    }

    public bool IsEmpty => Source == null;
}
