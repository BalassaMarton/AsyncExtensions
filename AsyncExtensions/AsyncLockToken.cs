using System.Diagnostics;

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

public static class AsyncLockTokenExtensions
{
    public static AsyncLockToken WithReentrancy(this AsyncLockToken token)
    {
        Debug.Assert(token.Source != null);
        var owner = token.Source.GetOwner();
        if (owner.Options.AllowReentrancy)
        {
            owner.EnableReentrancy();
        }

        return token;
    }
}
