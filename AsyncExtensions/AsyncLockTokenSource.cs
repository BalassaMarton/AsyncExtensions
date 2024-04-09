using System.Diagnostics;
using System.Threading.Tasks.Sources;

namespace AsyncExtensions;

internal sealed class AsyncLockTokenSource : IValueTaskSource<AsyncLockToken>
{
    public short Version => _valueTaskSource.Version;

    public AsyncLockToken ParentToken;

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
        Debug.Assert(_owner != null);

        if (!_owner.Options.AllowReentrancy)
        {
            _valueTaskSource.OnCompleted(
                continuation,
                state,
                token,
                flags);

            return;
        }

        _owner.EnableReentrancy();

        _valueTaskSource.OnCompleted(
            continuation,
            state,
            token,
            flags | ValueTaskSourceOnCompletedFlags.FlowExecutionContext);
    }

    public AsyncLockToken GetToken()
    {
        return new AsyncLockToken(this, _valueTaskSource.Version);
    }

    public void SetResult(in AsyncLockToken lockToken)
    {
        _valueTaskSource.SetResult(lockToken);
    }

    public void SetException(Exception e)
    {
        _valueTaskSource.SetException(e);
    }

    public AsyncLock GetOwner()
    {
        Debug.Assert(_owner != null);
        return _owner;
    }

    public void SetOwner(AsyncLock owner)
    {
        Debug.Assert(_owner == null);
        _owner = owner;
    }

    public void Acquire()
    {
        Debug.Assert(_refCount == 0);
        Interlocked.Increment(ref _refCount);
    }

    public void Acquire(in AsyncLockToken token)
    {
        ValidateToken(in token);
        Interlocked.Increment(ref _refCount);
    }

    public void Release(in AsyncLockToken token)
    {
        ValidateToken(in token);

        if (Interlocked.Decrement(ref _refCount) == 0)
        {
            _owner!.Release(this);
        }
    }

    public void Reset()
    {
        _valueTaskSource.Reset();
        _owner = null;
        _refCount = 0;
        ParentToken = default;
    }

    private AsyncLock? _owner;
    private uint _refCount;

    private ManualResetValueTaskSourceCore<AsyncLockToken> _valueTaskSource = new()
    {
        RunContinuationsAsynchronously = true,
    };

    private void ValidateToken(in AsyncLockToken token)
    {
        if (token.Source != this || token.Version != _valueTaskSource.Version)
            throw ThrowHelper.InvalidToken();
    }
}
