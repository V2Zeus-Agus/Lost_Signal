using System;
using System.Collections.Generic;
using UnityEngine;

public class GameObjectPool<T> where T : Component
{
    private readonly Func<Vector3, Quaternion, T> createItem;
    private readonly Queue<T> available = new Queue<T>();
    private readonly int maxSize;
    private int totalCreated;

    public int ActiveCount => totalCreated - available.Count;

    public GameObjectPool(Func<Vector3, Quaternion, T> createItem, int maxSize = int.MaxValue)
    {
        this.createItem = createItem;
        this.maxSize = maxSize;
    }

    public void Prewarm(int count, Vector3 position)
    {
        for (int i = 0; i < count && totalCreated < maxSize; i++)
        {
            T item = createItem(position, Quaternion.identity);
            totalCreated++;
            item.gameObject.SetActive(false);
            available.Enqueue(item);
        }
    }



    public bool TryGet(Vector3 position, Quaternion rotation, out T item)
    {
        if (available.Count > 0)
        {
            item = available.Dequeue();
            item.transform.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);
            return true;
        }

        if (totalCreated >= maxSize)
        {
            item = null;
            return false;
        }

        item = createItem(position, rotation);
        totalCreated++;
        item.gameObject.SetActive(true);
        return true;
    }

    public void Release(T item)
    {
        if (!item.gameObject.activeSelf) return;

        item.gameObject.SetActive(false);
        available.Enqueue(item);
    }
}
