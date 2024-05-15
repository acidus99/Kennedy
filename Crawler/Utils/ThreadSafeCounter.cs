namespace Kennedy.Crawler.Utils;

/// <summary>
/// Simple, thread safe counter
/// </summary>
public class ThreadSafeCounter
{
    private int Counter;

    public ThreadSafeCounter(int initialValue = 0)
    {
        Counter = initialValue;
    }

    public int Increment()
    {
        int tmp = Interlocked.Increment(ref Counter);
        return tmp;
    }

    public int Decrement()
    {
        int tmp = Interlocked.Decrement(ref Counter);
        return tmp;
    }

    public int Count
        => Counter;
}