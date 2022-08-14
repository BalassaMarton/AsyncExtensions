using System.Diagnostics;
using System.Threading.Tasks.Sources;

namespace AsyncExtensions;

internal sealed class AsyncLockTokenSource : IValueTaskSource<AsyncLockToken>
{
    private AsyncLock? _lock;
    private uint _refCount;

    private ManualResetValueTaskSourceCore<AsyncLockToken> _valueTaskSource = new()
        { RunContinuationsAsynchronously = true };

    public short Version => _valueTaskSource.Version;
    
    public AsyncLockToken GetResult(short token)
    {
        return _valueTaskSource.GetResult(token);
    }

    public ValueTaskSourceStatus GetStatus(short token)
    {
        return _valueTaskSource.GetStatus(token);
    }

    public void OnCompleted(
        Action<object?> continuation,
        object? state,
        short token,
        ValueTaskSourceOnCompletedFlags flags)
    {
        _valueTaskSource.OnCompleted(continuation, state, token, flags);
    }

    public AsyncLockToken GetToken() => new(this, _valueTaskSource.Version);

    internal void Acquire(AsyncLock @lock)
    {
        _lock = @lock;
        Debug.Assert(_refCount == 0);
        Interlocked.Increment(ref _refCount);
    }

    internal void Acquire(in AsyncLockToken token)
    {
        ValidateToken(in token);
        Interlocked.Increment(ref _refCount);
    }

    internal void Release(in AsyncLockToken token)
    {
        ValidateToken(in token);
        if (Interlocked.Decrement(ref _refCount) == 0)
        {
            _lock!.Release(this);
        }
    }

    internal void Reset()
    {
        _valueTaskSource.Reset();
        _lock = null;
        _refCount = 0;
    }

    private void ValidateToken(in AsyncLockToken token)
    {
        if (token.Source != this || token.Version != _valueTaskSource.Version) throw ThrowHelper.InvalidToken();
    }

    public void SetResult(in AsyncLockToken lockToken)
    {
        _valueTaskSource.SetResult(lockToken);
    }

    public void SetException(Exception e)
    {
        _valueTaskSource.SetException(e);
    }
}
