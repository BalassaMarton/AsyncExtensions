namespace AsyncExtensions;

internal static class ThrowHelper
{
    public static InvalidOperationException InvalidToken()
    {
        return new InvalidOperationException("The provided AsyncLockToken is in an invalid state");
    }

    public static InvalidOperationException RecursiveLockNotOwned()
    {
        return new InvalidOperationException("Attempted to recursively acquire a lock without owning it");
    }
    
    public static InvalidOperationException ReleasedLockNotOwned()
    {
        return new InvalidOperationException("Attempted to release a lock without owning it");
    }
}
