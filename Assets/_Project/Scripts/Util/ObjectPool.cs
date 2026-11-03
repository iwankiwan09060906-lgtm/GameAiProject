using System;
using System.Collections.Generic;

namespace GameAi
{
    // 정해진 개수까지만 만들고, 넘으면 가장 오래된 것부터 다시 쓰는 풀(FN-COM-04).
    // 혈흔·탄피가 아무리 쌓여도 메모리가 늘지 않게 한다. 반납 절차가 없고 "빌려서 덮어쓰는" 방식이다
    public sealed class ObjectPool<T> where T : class
    {
        readonly List<T> items = new List<T>();
        readonly Func<T> create;
        readonly int capacity;

        // 다음에 재사용할(가장 오래된) 칸 번호
        int next;

        public int Count { get { return items.Count; } }
        public int Capacity { get { return capacity; } }

        public ObjectPool(int capacity, Func<T> create)
        {
            this.capacity = capacity;
            this.create = create;
        }

        // 하나를 얻는다. 아직 가득 차지 않았으면 새로 만들고, 가득 찼으면 가장 오래된 것을 돌려준다
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
