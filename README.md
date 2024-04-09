# AsyncExtensions

This is an experimental repo for solving efficient recursive async locking in .NET.

## Example: Simple async locking

```cs
var mutex = new AsyncLock();

using (await mutex.LockAsync()) 
{
    // ...
}
```

## Example: Recursive async locking

> Let me know if you have a solution for flowing back the execution context
> to the awaiter, without an additional method call.

```cs
var mutex = new AsyncLock(new AsyncLockOptions { AllowReentrancy = true });

await Outer();

async ValueTask Outer() 
{
    using ((await mutex.LockAsync()).WithReentrancy()) 
    {
        await Inner();
    }
}

async ValueTask Inner()
{
    using (await mutex.LockAsync())
    {
        // ...
    }
}
```

## Example: Recursive async locking with shorthand syntax

Here you don't call `LockAsync` at all, the lock itself wraps your method that wants to acquire it, allowing reentrancy if configured.

```cs
var mutex = new AsyncLock(new AsyncLockOptions { AllowReentrancy = true });

await mutex.RunAsync(Outer);

async ValueTask Outer() 
{
    await mutex.RunAsync(Inner);
}

async ValueTask Inner()
{
    await mutex.RunAsync(
        () => 
        {
            // ...
        });
}
```

## Example: Recursive async locking using a token

This method is faster and allocates zero memory on the happy path (when the lock is not already owned by someone else).

```cs
var mutex = new AsyncLock();

await Outer();

async ValueTask Outer() 
{
    using (var token = await mutex.LockAsync()) 
    {
        await Inner(token);
    }
}

async ValueTask Inner(AsyncLockToken token = default)
{
    using (await mutex.LockAsync(token))
    {
        // ...
    }
}
```