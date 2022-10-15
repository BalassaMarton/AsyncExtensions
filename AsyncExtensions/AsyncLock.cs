using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks.Sources;
using Microsoft.Extensions.ObjectPool;

namespace AsyncExtensions;

public sealed class AsyncLock
{
    public AsyncLock? Parent { get; set; }

    public ValueTask<AsyncLockToken> LockAsync(CancellationToken cancellationToken)
    {
        return LockAsync(default, cancellationToken);
    }

    public ValueTask<AsyncLockToken> LockAsync(
        AsyncLockToken token = default,
        CancellationToken cancellationToken = default)
    {
        if (token.IsEmpty)
            return Parent == null
                ? LockAsyncCore(default, cancellationToken)
                : LockAsyncWithParent(cancellationToken);

        if (_currentOwner != token.Source)
            throw ThrowHelper.RecursiveLockNotOwned();

        token.Source!.Acquire(in token);

        return new ValueTask<AsyncLockToken>(token);
    }

    public static void Chain(params AsyncLock[] locks)
    {
        for (var i = 1; i < locks.Length; i++)
        {
            locks[i].Parent = locks[i - 1];
        }
    }

    public static void Chain(IEnumerable<AsyncLock> locks)
    {
        locks.Aggregate(
            (AsyncLock?)null,
            (parent, next) =>
            {
                next.Parent = parent;

                return next;
            });
    }

    internal void Release(AsyncLockTokenSource tokenSource)
    {
        if (_currentOwner != tokenSource)
            throw ThrowHelper.ReleasedLockNotOwned();

        AsyncLockTokenSource? nextTokenSource = null;

        if (!tokenSource.ParentToken.IsEmpty)
            tokenSource.ParentToken.Dispose();

        lock (_mutex)
        {
            while (_queue.TryDequeue(out var next))
            {
                if (next.GetStatus(next.Version) == ValueTaskSourceStatus.Pending)
                {
                    nextTokenSource = next;

                    break;
                }
                else
                {
                    RecycleTokenSource(next);
                }
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

    private AsyncLockTokenSource? _currentOwner;
    private readonly object _mutex = new();
    private readonly Queue<AsyncLockTokenSource> _queue = new();

    private ValueTask<AsyncLockToken> LockAsyncCore(AsyncLockToken parentToken, CancellationToken cancellationToken)
    {
        var tokenSource = GetTokenSource();

        if (!parentToken.IsEmpty)
            tokenSource.ParentToken = parentToken;

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
                        cancellationToken.Register(
                            state =>
                            {
                                var token = (AsyncLockToken)state!;

                                if (token.Version != token.Source!.Version
                                    || token.Source.GetStatus(token.Version) != ValueTaskSourceStatus.Pending)
                                    return;

                                token.Source.SetException(new OperationCanceledException());
                            },
                            tokenSource.GetToken());

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

    private ValueTask<AsyncLockToken> LockAsyncWithParent(CancellationToken cancellationToken)
    {
        var parentTokenTask = Parent!.LockAsync(cancellationToken);

        return parentTokenTask.IsCompleted
            ? LockAsyncCore(parentTokenTask.Result, cancellationToken)
            : LockAsyncWithParentImpl(parentTokenTask, cancellationToken);

        [SuppressMessage("ReSharper", "VariableHidesOuterVariable")]
        async ValueTask<AsyncLockToken> LockAsyncWithParentImpl(
            ValueTask<AsyncLockToken> parentTokenTask,
            CancellationToken cancellationToken)
        {
            return await LockAsyncCore(await parentTokenTask, cancellationToken);
        }
    }

    private static readonly ObjectPool<AsyncLockTokenSource> Pool =
        new DefaultObjectPool<AsyncLockTokenSource>(new AsyncLockTokenSourcePoolPolicy());

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





