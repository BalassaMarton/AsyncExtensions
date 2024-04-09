using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks.Sources;
using Microsoft.Extensions.ObjectPool;

namespace AsyncExtensions;

public sealed class AsyncLock
{
    public AsyncLock(AsyncLockOptions options = default)
    {
        Options = options;

        if (options.AllowReentrancy)
        {
            _recursiveTokenSource = new AsyncLocal<AsyncLockTokenSource?>();
        }
    }

    public AsyncLockOptions Options { get; }
    public string? LockId { get; }
    public AsyncLock? Parent { get; set; }

    public ValueTask<AsyncLockToken> LockAsync(CancellationToken cancellationToken)
    {
        return LockAsync(default, cancellationToken);
    }

    public ValueTask<AsyncLockToken> LockAsync(
        AsyncLockToken token = default,
        CancellationToken cancellationToken = default)
    {
        if (token.IsEmpty && _recursiveTokenSource != null)
        {
            token = _recursiveTokenSource.Value?.GetToken() ?? default;
        }

        if (token.IsEmpty)
            return Parent == null
                ? LockAsyncCore(default, cancellationToken)
                : LockAsyncWithParent(cancellationToken);

        if (_currentTokenSource != token.Source)
            throw ThrowHelper.RecursiveLockNotOwned();

        token.Source!.Acquire(in token);

        return new ValueTask<AsyncLockToken>(token);
    }

    public async Task RunAsync(Func<ValueTask> action)
    {
        using (await LockAsync())
        {
            if (_recursiveTokenSource != null)
            {
                _recursiveTokenSource.Value = _currentTokenSource;
            }

            await action();
        }
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

    internal void EnableReentrancy()
    {
        Debug.Assert(_recursiveTokenSource != null);
        Debug.Assert(_currentTokenSource != null);
        _recursiveTokenSource.Value = _currentTokenSource;
    }

    internal void Release(AsyncLockTokenSource tokenSource)
    {
        if (_currentTokenSource != tokenSource)
            throw ThrowHelper.ReleasedLockNotOwned();

        AsyncLockTokenSource? nextTokenSource = null;

        lock (_mutex)
        {
            while (_queue.TryDequeue(out var next))
            {
                if (next.GetStatus(next.Version) != ValueTaskSourceStatus.Pending)
                {
                    RecycleTokenSource(next);
                }
                else
                {
                    nextTokenSource = next;

                    break;
                }
            }

            _currentTokenSource = nextTokenSource;
        }

        if (!tokenSource.ParentToken.IsEmpty)
            tokenSource.ParentToken.Dispose();

        if (nextTokenSource == null)
        {
            if (_recursiveTokenSource != null)
            {
                _recursiveTokenSource.Value = default;
            }
        }
        else
        {
            nextTokenSource.Acquire();
            var nextToken = nextTokenSource.GetToken();
            
            if (_recursiveTokenSource != null)
            {
                _recursiveTokenSource.Value = nextTokenSource;
            }

            nextTokenSource.SetResult(nextToken);
        }

        RecycleTokenSource(tokenSource);
    }

    private static readonly ObjectPool<AsyncLockTokenSource> Pool =
        new DefaultObjectPool<AsyncLockTokenSource>(new AsyncLockTokenSourcePoolPolicy());

    private AsyncLockTokenSource? _currentTokenSource;
    private readonly object _mutex = new();
    private readonly Queue<AsyncLockTokenSource> _queue = new();
    private readonly AsyncLocal<AsyncLockTokenSource?>? _recursiveTokenSource;

    private ValueTask<AsyncLockToken> LockAsyncCore(AsyncLockToken parentToken, CancellationToken cancellationToken)
    {
        var tokenSource = GetTokenSource();
        tokenSource.SetOwner(this);

        if (!parentToken.IsEmpty)
        {
            tokenSource.ParentToken = parentToken;
        }

        try
        {
            lock (_mutex)
            {
                if (_currentTokenSource == null)
                {
                    _currentTokenSource = tokenSource;
                    tokenSource.Acquire();
                    var nextToken = tokenSource.GetToken();

                    if (_recursiveTokenSource != null)
                    {
                        _recursiveTokenSource.Value = tokenSource;
                    }

                    return new ValueTask<AsyncLockToken>(nextToken);
                }

                if (cancellationToken.CanBeCanceled)
                {
                    cancellationToken.Register(
                        state =>
                        {
                            var token = (AsyncLockToken)state!;

                            if (token.Version == token.Source!.Version
                                && token.Source.GetStatus(token.Version) == ValueTaskSourceStatus.Pending)
                            {
                                token.Source.SetException(new OperationCanceledException());
                            }
                        },
                        tokenSource.GetToken());
                }

                _queue.Enqueue(tokenSource);

                return new ValueTask<AsyncLockToken>(tokenSource, tokenSource.Version);
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







