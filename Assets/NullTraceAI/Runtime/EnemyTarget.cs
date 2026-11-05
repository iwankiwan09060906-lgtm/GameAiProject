// 역할: 적이 감지할 대상의 생존 여부와 조도만 제공하는 작은 입력 컴포넌트다.
// 연결: 팀의 플레이어/전투/조명 코드가 아래 값을 갱신하고 EnemyAgent가 읽는다.
// 읽는 순서: EnemyTypes의 LightLevel → 이 파일 → EnemyAgent.IsUsable/Sense를 확인한다.
// 모델을 바꾸더라도 이 컴포넌트가 붙은 로직 루트는 유지하고, 외형 자식만 교체하면 된다.
using UnityEngine;

namespace NullTrace.AI
{
    /// <summary>
    /// 자신의 Transform을 감지 위치로 제공한다. 이동이나 피해 계산은 하지 않는다.
    /// 한 GameObject에 중복 부착하지 못하게 하여 어느 입력을 읽어야 할지 모호해지는 것을 막는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyTarget : MonoBehaviour
    {
        // 전투 코드에서 사망 시 false로 바꾼다. EnemyAgent는 비활성/파괴된 대상도 감지 대상에서 제외한다.
        // 이 값이 false가 되어도 적이 이전에 기억한 위치까지 자동으로 삭제하는 것은 아니다.
        public bool IsAlive = true;
        // 현재 조도 단계. 스프라이트 색이나 실제 Light2D 밝기를 자동으로 읽는 기능은 없으므로
        // 조명 구역 등 팀 코드가 갱신해야 한다. 밝을수록 EnemyProfile의 더 긴 거리 표를 사용한다.
        public LightLevel Light = LightLevel.Bright;
        // 같은 표적을 직접 보고 무전으로 다시 받아도 한 목격 기록으로 합치기 위한 번호다.
        // 관측 정보 자체는 NPC에게 있다. 이 번호가 존재한다고 표적 위치를 안다는 뜻은 아니다.
        public long ObservationId { get; } = AiEventIds.Next();
        public double ObservationStartedAt { get; private set; }
        internal int ObservationRevision;
        private void Awake() => ObservationStartedAt = Time.timeAsDouble;
    }
}
