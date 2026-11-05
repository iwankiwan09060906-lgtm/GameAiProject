// 역할: 적의 감지/이동/공격/대사 수치를 한 에셋에 모아 Inspector에서 조절하게 한다.
// 연결: EnemyAgent가 이 값을 읽고 AiMath, GridWorld2D, SpeechGate에 필요한 수치를 넘긴다.
// 읽는 순서: 아래 튜닝값 → ViewDistance의 조도 표 → ValidateValues의 안전 범위 보정.
// 거리와 속도의 '월드 단위'는 Unity 좌표 단위다. 팀에서 월드 1단위를 1m로 정한 경우에만 1을 1m로 해석한다.
// 격자 한 칸의 크기는 별도 설정인 GridWorld2D.CellSize가 정한다.
using UnityEngine;

namespace NullTrace.AI
{
    /// <summary>
    /// 적 동작을 조절하는 ScriptableObject 설정 에셋. GameObject에 붙이는 행동 컴포넌트는 아니다.
    /// 같은 에셋을 여러 적에게 연결하면 설정도 공유된다. 기본값은 시연용이며 확정 난이도 값이 아니다.
    /// </summary>
    [CreateAssetMenu(menuName = "Null Trace/Enemy Profile")]
    public sealed class EnemyProfile : ScriptableObject
    {
        [Header("시야 / 조도별 거리")]
        // 전체 시야각(도). 100이면 정면 좌우 50도까지; 클수록 측면을 잘 보고 360이면 전 방향을 검사한다.
        public float ViewAngle = 100f;
        // 어두운 대상을 볼 수 있는 최대 거리(월드 단위). 늘리면 어둠 속에서도 더 멀리 발견할 수 있다.
        public float DarkViewDistance = 3f;
        // 희미한 조도의 최대 거리. 보정 시 Dark보다 짧아지지 않는다.
        public float DimViewDistance = 6f;
        // 밝은 조도의 최대 거리. 보정 시 Dim보다 짧아지지 않는다. 조도는 인지 증가 속도가 아닌 거리 표를 바꾼다.
        public float BrightViewDistance = 10f;
        // 보이는 동안 초당 늘어나는 인지도(0..1 기준). 1이면 인지도 0에서 약 1초, 2이면 약 0.5초 뒤 교전한다.
        public float DetectionGain = 1f;
        // 보이지 않을 때 초당 줄어드는 직접 식별 누적값(0..1). 의심도(0..100) 감쇠와는 별개다.
        public float DetectionDecay = 0.4f;
        // 이전 에셋/호출 호환용. 현재 Utility의 확인 진입은 의심도 10점이므로 이 필드는 행동에 사용하지 않는다.
        public float AlertThreshold = 0.25f;
        [Header("청각")]
        // 거리와 벽 감쇠가 끝난 소리 세기(0..1)의 최소 기준. 작게 하면 작은 소리도 단서로 받아들인다.
        public float HearingThreshold = 0.1f;
        // 벽 Collider 하나를 지날 때 남는 소리 비율. 0.5는 절반, 0은 완전 차단, 1은 벽에 의한 감소 없음이다.
        public float WallSoundMultiplier = 0.5f;
        [Header("상태 / 수색")]
        // 이전 에셋 호환용 미사용 필드. 현재 확인 시간은 InspectDuration이 정한다.
        public float AlertDuration = 1f;
        // 이전 에셋 호환용 미사용 필드. 현재 공격은 시야를 잃으면 즉시 취소한다.
        public float LostSightDelay = 0.6f;
        // 이전 에셋 호환용 미사용 필드. 전체 대응 마감은 EpisodeDuration이며 새 단서로 연장하지 않는다.
        public float SearchDuration = 8f;
        // 마지막 단서 주변 네 조사점까지의 거리(월드 단위). 클수록 넓게 훑지만 맵 밖/벽 안 후보가 생길 수 있다.
        public float SearchRadius = 2f;
        // 이전 에셋 호환용 미사용 필드. 현재 지점별 조사 시간은 SearchPointDuration이다.
        public float SearchPause = 0.7f;
        // 순찰점 실제 도착 후 다음 지점으로 출발하기 전 대기(초). 이동 실패는 방문 완료로 세지 않는다.
        public float PatrolPause = 0.8f;
        [Header("이동 / 공격")]
        // 순찰/수색/복귀 이동 속도(월드 단위/초). 실제 한 Tick 이동량은 속도×경과 초이며 벽 검사를 먼저 통과해야 한다.
        public float MoveSpeed = 2f;
        // 교전에서 사거리 밖의 보이는 대상을 향해 접근하는 속도(월드 단위/초).
        public float CombatMoveSpeed = 3f;
        // 이 거리 안이면 도착으로 인정한다. 크게 하면 덜 가까이 가도 되지만 실제로는 CellSize의 20% 이하로 제한한다.
        public float ArrivalDistance = 0.1f;
        // 새 목적지 등의 경로 요청 사이 최소 간격(초). 크게 하면 A* 호출은 줄고 이동 목표 변경 반응은 늦어진다.
        public float RepathInterval = 0.5f;
        // 경로 실패 후 재시도까지의 최소 대기(초). 클수록 막힌 길의 반복 계산이 줄지만 길이 열린 뒤 반응도 늦을 수 있다.
        public float PathRetryDelay = 1f;
        // 공격 요청을 허용하는 최대 거리(월드 단위). 늘려도 시야·벽·생존 조건을 우회할 수는 없다.
        public float AttackRange = 4f;
        // 공격 요청 사이 최소 시간(초). 작을수록 자주 요청한다. 실제 피해량/탄환 생성은 팀 전투 코드가 담당한다.
        public float AttackInterval = 0.8f;
        [Header("대사")]
        // 문장이 서로 달라도 반드시 기다리는 최소 간격(초). 크게 하면 말풍선이 덜 자주 나온다.
        public float SpeechInterval = 2f;
        // 같은 문장을 다시 허용하는 간격(초). 크게 하면 반복 대사가 줄며 SpeechInterval보다 작게 보정되지 않는다.
        public float SameSpeechInterval = 8f;

        [Header("Utility / 추적 기록 (초, 월드 단위)")]
        // 첫 대응 시 고정한다. 새 소리나 교전 전환으로 이 예산을 다시 시작하지 않는다.
        public float EpisodeDuration = 20f;
        public float PursuitRadius = 8f;
        // 일반 후보끼리 비교하는 주기. 생존·시야·단서 만료·강제 복귀는 이 시간을 기다리지 않는다.
        public float DecisionInterval = 0.2f;
        // 유효한 현재 목표를 유지하는 최소 초와 전환에 필요한 점수 차이(0..100 점수 기준).
        public float MinimumGoalHold = 0.5f;
        public float GoalSwitchAdvantage = 10f;
        // 복귀 뒤 약한 단서를 다시 조사하기 전 기다리는 최소 초. 다음 점/초소 도착과 묶지 않는다.
        // 현재 직접 식별한 적은 이 대기를 우회하지만, 이동 중 복귀와 추적 마감/영역 제약은 그대로 지킨다.
        public float RecoveryDuration = 2f;
        // 확인은 제자리 방향 확인, 교전 준비는 조사할 단서 없는 감시, 수색은 한 방문 지점의 조사 초다.
        public float InspectDuration = 1f;
        public float PrepareDuration = 2f;
        public float SearchPointDuration = 2f;
        // 목격은 마지막 직접 관측부터, 소리는 원본 발생부터 센다. 무전 수신으로 다시 시작하지 않는다.
        public float SightMemorySeconds = 8f;
        public float SoundMemorySeconds = 15f;
        // 새 유효 정보 없이 기다리는 초 / 이후 전체 기여 합계에서 초당 빼는 점수다.
        public float SuspicionQuietSeconds = 3f;
        public float SuspicionDecayPerSecond = 6f;

        /// <summary>
        /// 대상의 조도 단계를 받아 그 단계의 최대 감지 거리를 반환한다.
        /// 실제 조명 렌더링을 계산하는 대신 세 개의 상수 표를 고르는 수업의 단순 조도 모델이다.
        /// </summary>
        public float ViewDistance(LightLevel light)
        {
            switch (light)
            {
                case LightLevel.Dark: return DarkViewDistance;
                case LightLevel.Dim: return DimViewDistance;
                case LightLevel.Bright: return BrightViewDistance;
                // 잘못된 enum 값이 들어와도 가장 보수적인 어두움 거리로 처리한다.
                default: return DarkViewDistance;
            }
        }

        /// <summary>
        /// Inspector 또는 다른 코드가 넣은 값을 유한한 안전 범위로 보정한다. 반환값 없이 이 에셋 값을 고친다.
        /// NaN/무한대는 기본값으로 바꾸고, 정상 숫자는 각 허용 구간에 맞춘다.
        /// 예: 재탐색 간격 0은 매 프레임 요청을 유발할 수 있어 최소 양수로 올린다.
        /// </summary>
        public void ValidateValues()
        {
            ViewAngle = Safe(ViewAngle, 100f, 1f, 360f);
            // 앞 단계의 보정값을 다음 단계의 하한으로 사용해 Dark ≤ Dim ≤ Bright 순서를 유지한다.
            DarkViewDistance = Safe(DarkViewDistance, 3f, 0.1f, 1000f);
            DimViewDistance = Safe(DimViewDistance, 6f, DarkViewDistance, 1000f);
            BrightViewDistance = Safe(BrightViewDistance, 10f, DimViewDistance, 1000f);
            // 인지도는 늘거나 줄 수 있어야 한다. 경계 기준도 0/1 사이로 두어 의미 없는 임계값을 피한다.
            DetectionGain = Safe(DetectionGain, 1f, 0.01f, 100f);
            DetectionDecay = Safe(DetectionDecay, 0.4f, 0.01f, 100f);
            AlertThreshold = Safe(AlertThreshold, 0.25f, 0.01f, 0.99f);
            HearingThreshold = Safe(HearingThreshold, 0.1f, 0.001f, 1f);
            WallSoundMultiplier = Safe(WallSoundMultiplier, 0.5f, 0f, 1f);
            // 상태 대기/행동 간격에 양수 하한을 두어 설정 실수로 즉시 반복되는 동작을 줄인다.
            AlertDuration = Safe(AlertDuration, 1f, 0.05f, 60f);
            LostSightDelay = Safe(LostSightDelay, 0.6f, 0.05f, 60f);
            SearchDuration = Safe(SearchDuration, 8f, 0.1f, 300f);
            SearchRadius = Safe(SearchRadius, 2f, 0f, 100f);
            SearchPause = Safe(SearchPause, 0.7f, 0.05f, 60f);
            PatrolPause = Safe(PatrolPause, 0.8f, 0.05f, 60f);
            MoveSpeed = Safe(MoveSpeed, 2f, 0.01f, 100f);
            CombatMoveSpeed = Safe(CombatMoveSpeed, 3f, 0.01f, 100f);
            ArrivalDistance = Safe(ArrivalDistance, 0.1f, 0.01f, 0.5f);
            RepathInterval = Safe(RepathInterval, 0.5f, 0.05f, 30f);
            PathRetryDelay = Safe(PathRetryDelay, 1f, 0.05f, 60f);
            AttackRange = Safe(AttackRange, 4f, 0.1f, 1000f);
            AttackInterval = Safe(AttackInterval, 0.8f, 0.05f, 60f);
            // 같은 문장 제한은 전체 대사 제한보다 짧아지지 않게 마지막에 맞춘다.
            SpeechInterval = Safe(SpeechInterval, 2f, 0.1f, 300f);
            SameSpeechInterval = Safe(SameSpeechInterval, 8f, SpeechInterval, 600f);
            EpisodeDuration = Safe(EpisodeDuration, 20f, 0.1f, 300f);
            PursuitRadius = Safe(PursuitRadius, 8f, 0.1f, 1000f);
            DecisionInterval = Safe(DecisionInterval, 0.2f, 0.02f, 2f);
            MinimumGoalHold = Safe(MinimumGoalHold, 0.5f, 0f, 10f);
            GoalSwitchAdvantage = Safe(GoalSwitchAdvantage, 10f, 0f, 100f);
            RecoveryDuration = Safe(RecoveryDuration, 2f, 2f, 60f);
            InspectDuration = Safe(InspectDuration, 1f, 0.1f, 10f);
            PrepareDuration = Safe(PrepareDuration, 2f, 0.1f, 10f);
            SearchPointDuration = Safe(SearchPointDuration, 2f, 0.1f, 2f);
            SightMemorySeconds = Safe(SightMemorySeconds, 8f, 0.1f, 300f);
            SoundMemorySeconds = Safe(SoundMemorySeconds, 15f, 0.1f, 300f);
            SuspicionQuietSeconds = Safe(SuspicionQuietSeconds, 3f, 0f, 60f);
            SuspicionDecayPerSecond = Safe(SuspicionDecayPerSecond, 6f, 0f, 100f);
        }

        // value가 잘못된 수면 fallback을 고르고, 최종 숫자를 min..max로 제한해 반환하는 공통 처리다.
        private static float Safe(float value, float fallback, float min, float max)
            => Mathf.Clamp(AiMath.IsFinite(value) ? value : fallback, min, max);

        // Unity Inspector에서 값을 편집할 때 실행한다. 런타임 코드에서의 변경은 EnemyAgent가 같은 검사를 호출한다.
        private void OnValidate() => ValidateValues();
    }
}
