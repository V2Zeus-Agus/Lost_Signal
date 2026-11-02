using System.Collections.Generic;

public static class NotificationQueue
{
    private static readonly Queue<string> pending = new Queue<string>();

    public static int Count => pending.Count;

    public static void Enqueue(string message)
    {
        if (!string.IsNullOrEmpty(message))
            pending.Enqueue(message);
    }

    public static bool TryDequeue(out string message) => pending.TryDequeue(out message);

    public static void Clear() => pending.Clear();
}
