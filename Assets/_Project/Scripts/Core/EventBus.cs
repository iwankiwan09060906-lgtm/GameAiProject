using System;
using System.Collections.Generic;

namespace GameAi
{
    // 시스템 간 통신의 유일한 통로. 구독은 OnEnable, 해제는 OnDisable에서 한다.
    public static class EventBus
    {
        static class Channel<T> where T : struct
        {
            static readonly List<Action<T>> Handlers = new List<Action<T>>();
            static int depth;
            static bool dirty;

            public static int Count
            {
                get
                {
                    int n = 0;
                    foreach (var h in Handlers)
                        if (h != null) n++;
                    return n;
                }
            }

            public static void Add(Action<T> handler)
            {
                Handlers.Add(handler);
            }

            public static void Remove(Action<T> handler)
            {
                int i = Handlers.IndexOf(handler);
                if (i < 0) return;
                if (depth > 0)
                {
                    // 발행 중에는 목록을 줄이지 않고 비워 둔다
                    Handlers[i] = null;
                    dirty = true;
                }
                else
                {
                    Handlers.RemoveAt(i);
                }
            }

            public static void Publish(T evt)
            {
                depth++;
                int count = Handlers.Count;
                for (int i = 0; i < count; i++)
                    Handlers[i]?.Invoke(evt);
                depth--;
                if (depth == 0 && dirty)
                {
                    Handlers.RemoveAll(h => h == null);
                    dirty = false;
                }
            }
        }

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            Channel<T>.Add(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            Channel<T>.Remove(handler);
        }

        public static void Publish<T>(T evt) where T : struct
        {
            Channel<T>.Publish(evt);
        }

        public static int SubscriberCount<T>() where T : struct
        {
            return Channel<T>.Count;
        }
    }
}
