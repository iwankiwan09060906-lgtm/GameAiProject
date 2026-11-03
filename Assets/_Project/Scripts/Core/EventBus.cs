using System;
using System.Collections.Generic;

namespace GameAi
{
    // 시스템 간 통신의 유일한 통로(5.1). 보내는 쪽은 받는 쪽을 몰라도 된다.
    // 규칙: 구독은 OnEnable, 해제는 OnDisable에서 반드시 한다.
    public static class EventBus
    {
        // 이벤트 종류(T)마다 따로 만들어지는 구독자 목록.
        // 제네릭 정적 클래스라서 종류별로 목록이 하나씩 생기고, 딕셔너리 조회가 필요 없다.
        static class Channel<T> where T : struct
        {
            static readonly List<Action<T>> Handlers = new List<Action<T>>();

            // 지금 Publish가 몇 겹으로 실행 중인지. 핸들러 안에서 다시 Publish할 수도 있어 숫자로 센다
            static int depth;

            // 발행 중에 해제된 구독자가 있어 목록을 정리해야 하는지
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
                    // 발행 중에는 목록 길이를 바꾸면 순회가 꼬이므로 칸만 비워 두고 나중에 치운다
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

                // 발행 중에 새로 구독한 핸들러는 이번 발행에서 받지 않는다(길이를 미리 고정)
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

        // 테스트에서 구독 해제 누락을 확인하는 데 쓴다
        public static int SubscriberCount<T>() where T : struct
        {
            return Channel<T>.Count;
        }
    }
}
