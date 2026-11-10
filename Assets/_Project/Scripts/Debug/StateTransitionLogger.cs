using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GameAi
{
    // 적 상태 전이 로그(8.1, 13.3). 모든 전이를 reason과 함께 남기고, 씬이 끝날 때 파일로 쓴다
    public sealed class StateTransitionLogger : MonoBehaviour
    {
        readonly List<string> lines = new List<string>();

        public IReadOnlyList<string> Lines { get { return lines; } }
        public string FilePath { get; set; }

        void OnEnable() { EventBus.Subscribe<EnemyStateChangedEvent>(OnState); }
        void OnDisable() { EventBus.Unsubscribe<EnemyStateChangedEvent>(OnState); }

        void OnState(EnemyStateChangedEvent e)
        {
            string line = e.time.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " enemy=" + e.enemyId + " " + e.from + "->" + e.to + " reason=" + e.reason;
            // Debug.Log는 스택 추적 때문에 비싸 프레임 끊김을 만든다. 메모리에 모았다가 씬이 끝날 때 파일로 쓴다
            lines.Add(line);
        }

        void OnDestroy()
        {
            if (string.IsNullOrEmpty(FilePath) || lines.Count == 0) return;
            try
            {
                File.WriteAllLines(FilePath, lines);
            }
            catch (IOException e)
            {
                Debug.LogWarning("[FSM] 상태 전이 로그를 쓰지 못했습니다: " + e.Message);
            }
        }
    }
}
