using System.Threading.Tasks.Sources;
using Microsoft.Extensions.ObjectPool;

namespace AsyncExtensions;

public sealed class AsyncLock
{
    public ValueTask<AsyncLockToken> LockAsync(CancellationToken cancellationToken) =>
        LockAsync(default, cancellationToken);
    
    public ValueTask<AsyncLockToken> LockAsync(AsyncLockToken token = default, CancellationToken cancellationToken = default)
    {
        if (token.Source != null)
        {
            if (_currentOwner != token.Source) throw ThrowHelper.RecursiveLockNotOwned();
            token.Source.Acquire(in token);
            return new ValueTask<AsyncLockToken>(token);
        }
        
        var tokenSource = GetTokenSource();

        try
        {
            lock (_mutex)
            {
                if (_currentOwner == null)
                {
                    _currentOwner = tokenSource;
                    tokenSource.Acquire(this);
                    return new ValueTask<AsyncLockToken>(tokenSource.GetToken());
                }
                else
                {
                    if (cancellationToken.CanBeCanceled)
                    {
                        cancellationToken.Register(
                            state =>
                            {
                                var token = (AsyncLockToken)state!;
                        
                                if (token.Version != token.Source.Version
                                    || token.Source.GetStatus(token.Version) != ValueTaskSourceStatus.Pending)
                                {
                                    return;
                                }
                        
                                token.Source.SetException(new OperationCanceledException());
                            },
                            tokenSource.GetToken());
                    }
            
                    _queue.Enqueue(tokenSource);
                    
                    return new ValueTask<AsyncLockToken>(tokenSource, tokenSource.Version);
                }
            }
        }
        catch
        {
            RecycleTokenSource(tokenSource);
            throw;
        }
    }

    private AsyncLockTokenSource? _currentOwner;
    private readonly Queue<AsyncLockTokenSource> _queue = new();
    private readonly object _mutex = new object();

    private static readonly ObjectPool<AsyncLockTokenSource> Pool =
        new DefaultObjectPool<AsyncLockTokenSource>(new AsyncLockTokenSourcePoolPolicy());

    internal void Release(AsyncLockTokenSource tokenSource)
    {
        if (_currentOwner != tokenSource) throw ThrowHelper.ReleasedLockNotOwned();

        AsyncLockTokenSource? nextTokenSource = null;
        lock (_mutex)
        {
            while (_queue.TryDequeue(out var next))
            {
                if (next.GetStatus(next.Version) == ValueTaskSourceStatus.Pending)
                {
                    nextTokenSource = next;
                    break;
                }
                else RecycleTokenSource(next);
            }

            _currentOwner = nextTokenSource;
        }

        if (nextTokenSource != null)
        {
            nextTokenSource.Acquire(this);
            nextTokenSource.SetResult(nextTokenSource.GetToken());    
        }
        
        RecycleTokenSource(tokenSource);
    }

    private static AsyncLockTokenSource GetTokenSource()
    {
        return Pool.Get();
    }

    private static void RecycleTokenSource(AsyncLockTokenSource tokenSource)
    {
        Pool.Return(tokenSource);
    }
    
    private sealed class AsyncLockTokenSourcePoolPolicy : PooledObjectPolicy<AsyncLockTokenSource>
    {
        public override AsyncLockTokenSource Create()
        {
            return new AsyncLockTokenSource();
        }

        public override bool Return(AsyncLockTokenSource obj)
        {
            obj.Reset();
            return true;
        }
    }
}
