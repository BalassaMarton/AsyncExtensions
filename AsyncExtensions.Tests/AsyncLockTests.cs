namespace AsyncExtensions.Tests;

public partial class AsyncLockTests
{
    [Fact]
    public void LockAsync_returns_synchronously_when_the_lock_is_not_already_taken()
    {
        var vt = _lock.LockAsync();

        vt.IsCompletedSuccessfully.Should().BeTrue();

        vt.Result.Dispose();
    }

    [Fact]
    public async Task LockAsync_returns_a_Task_when_the_lock_is_already_taken()
    {
        using var first = await _lock.LockAsync();
        var second = _lock.LockAsync();

        second.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task LockAsync_queues_callers()
    {
        var messages = new List<int>();

        var count = 10;
        var tasks = new List<Task>();

        for (var i = 0; i < count; i++)
            tasks.Add(CreateTask(i));

        await Task.WhenAll(tasks);

        messages.Should().Equal(Enumerable.Range(0, count).ToArray());

        async Task CreateTask(int key)
        {
            using (await _lock.LockAsync())
            {
                messages.Add(key);
            }
        }
    }

    [Fact]
    public async Task Recursive_LockAsync()
    {
        using var token = await _lock.LockAsync();
        using var token2 = await _lock.LockAsync(token);
        using var token3 = await _lock.LockAsync(token2);
        var token4 = _lock.LockAsync();
        token4.IsCompleted.Should().BeFalse();
        //
    }

    [Theory]
    [InlineData(new[]{0, 1, 2})]
    [InlineData(new[]{1, 2, 0})]
    [InlineData(new[]{2, 1, 0})]
    public async Task Lock_with_parent_chain(int[] ordering)
    {
        var locks = Enumerable.Range(0, ordering.Length).Select(i => new AsyncLock()).ToList();
        AsyncLock.Chain(locks);
        var invocations = new List<int>();
        var semaphore = new TaskCompletionSource();
        var allTasks = Task.WhenAll(ordering.Select(i => AddTask(i, locks[i])));
        semaphore.SetResult();
        await allTasks;

        invocations.Should().Equal(ordering);
        
        async Task AddTask(int id, AsyncLock asyncLock)
        {
            using (await asyncLock.LockAsync())
            {
                await semaphore.Task;
                invocations.Add(id);
            }
        }
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(128)]
    public async Task Contention_test(int concurrency)
    {
        var maxDelay = 50;
        var random = new Random(0);
        var randomDelays = Enumerable.Range(0, concurrency).Select(_ => random.Next(maxDelay)).ToArray();

        var tasks = new List<Task>();

        foreach (var i in randomDelays)
            tasks.Add(CreateTask(i));

        await Task.WhenAll(tasks);

        async Task CreateTask(int delay)
        {
            await Task.Delay(delay);

            using (await _lock.LockAsync())
            {
                await Task.Delay(delay);
            }
        }
    }

    private readonly AsyncLock _lock = new();
}
