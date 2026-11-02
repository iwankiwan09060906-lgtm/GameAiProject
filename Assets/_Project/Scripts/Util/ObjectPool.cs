using System;
using System.Collections.Generic;

namespace GameAi
{
    // 최대 개수까지 만들고, 넘으면 가장 오래된 것부터 다시 쓴다
    public sealed class ObjectPool<T> where T : class
    {
        readonly List<T> items = new List<T>();
        readonly Func<T> create;
        readonly int capacity;
        int next;

        public int Count { get { return items.Count; } }
        public int Capacity { get { return capacity; } }

        public ObjectPool(int capacity, Func<T> create)
        {
            this.capacity = capacity;
            this.create = create;
        }

        public T Acquire()
        {
            if (items.Count < capacity)
            {
                T item = create();
                items.Add(item);
                return item;
            }
            T oldest = items[next];
            next = (next + 1) % capacity;
            return oldest;
        }
    }
}
