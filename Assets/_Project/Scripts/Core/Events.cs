using UnityEngine;

namespace GameAi
{
    // 시스템 사이를 오가는 이벤트 구조체 모음(5.2). 구조체라서 보내도 메모리 할당이 없다.

    // 소리의 종류. 소음 반경과 적의 반응이 종류에 따라 달라질 수 있다
    public enum NoiseKind { Footstep, Shot, Decoy, Door, Reload, Scream, Can }

    // 피해의 종류. 총알은 피 튀김 연출을 만들고 근접 제압은 만들지 않는다
    public enum DamageKind { Bullet, Melee }

    // 경보 방식. 경비병은 외침(근처만), 소총병은 무전(전체)
    public enum AlertKind { Shout, Radio }

    // 소리가 났다. HearingSensor와 HUD 소음 미터가 받는다(Phase 4~5)
    public struct NoiseEvent
    {
        public Vector2 position;
        public NoiseKind kind;
        public float radius;
        public int sourceId;
        public float time;
    }

    // 누가 피해를 입었다. Health와 CombatFx(피 튀김)가 받는다
    public struct DamageEvent
    {
        public int targetId;
        public int amount;
        public int attackerId;
        public Vector2 hitPoint;
        public DamageKind kind;
        public float time;
    }

    // 체력이나 방탄이 바뀌었다. HUD가 받는다
    public struct HealthChangedEvent
    {
        public int entityId;
        public int hp;
        public int armor;
    }

    // 누가 죽었다. 한 번만 보낸다. corpseSprite와 rotationDeg는 시체 그림을 놓는 데 쓴다
    public struct DeathEvent
    {
        public int entityId;
        public bool isPlayer;
        public Vector2 position;
        public string corpseSprite;
        public float rotationDeg;
        public float time;
    }

    // 총을 쐈다. endPos는 총알이 멈춘 곳(궤적선의 끝)이다
    public struct ShotFiredEvent
    {
        public int shooterId;
        public Vector2 muzzlePos;
        public Vector2 dir;
        public string weaponId;
        public Vector2 endPos;
    }

    // 적의 상태가 바뀌었다. reason은 로그와 대사 선택에 쓴다(Phase 5)
    public struct EnemyStateChangedEvent
    {
        public int enemyId;
        public EnemyState from;
        public EnemyState to;
        public string reason;
        public float time;
    }

    // 적이 플레이어를 완전히 발견했다(게이지 100). 발각 횟수 집계가 받는다(Phase 5~6)
    public struct DetectionEvent
    {
        public int enemyId;
        public float time;
    }

    // 동료에게 알리는 경보(Phase 9)
    public struct AlertEvent
    {
        public AlertKind kind;
        public Vector2 position;
        public float time;
        public int senderId;
    }

    // 적 머리 위에 띄울 대사(Phase 5)
    public struct BarkEvent
    {
        public int enemyId;
        public string text;
        public string emotion;
        // Qwen / Cache / Fallback
        public string source;
    }

    // 월드에서 일어난 사건 1건(7.21). 소리·단서·시야 이벤트를 모두 이 하나로 보낸다
    public struct WorldEvent
    {
        public int eventId;
        public string code;
        public Vector2 position;
        // 게임 시각(대화·일시정지 중 멈춤)
        public float time;
        // 발생 세기 0~1 (소리는 1)
        public float intensity;
        // 소리 반경(m). 소리가 아니면 0
        public float radius;
        public int sourceId;
        // 관련 물체(시체·탄피·혈흔·조명 등). 없으면 -1
        public int objectId;
        // 소음권총 사격 등 이벤트별 보조 표시
        public bool flag;
    }

    // 적 한 명이 이벤트를 인지했다(npc_perception). AI 판단은 여기서 끝나고 대사는 이것을 읽기만 한다
    public struct PerceptionEvent
    {
        public int enemyId;
        public int eventId;
        public string code;
        // 직접(true) / 무전(false)
        public bool direct;
        public Vector2 position;
        public float strength;
        public float contribution;
        public float time;
        public bool flag;
        public bool isNew;
    }

    // UI 버튼을 눌렀다(클릭음)
    public struct UiClickEvent { }

    // 죽은 적이 무기를 떨궜다(그림 표시용)
    public struct DroppedWeaponEvent
    {
        public Vector2 position;
        public string weaponSprite;
    }

    // 적의 행동 알림(재장전 등). 대사 계기로만 쓴다
    public struct EnemyActionEvent
    {
        public int enemyId;
        public string action;
        public float time;
    }

    // 전역 경계도가 바뀌었다(0 평상, 1 경계, 2 전투)
    public struct GlobalAlertChangedEvent
    {
        public int level;
    }

    // 조명이 꺼지거나 켜졌다(C)
    public struct LightChangedEvent
    {
        public int lightId;
        public bool isOn;
        public int actorId;
        public float time;
    }

    // 문이 열리거나 닫혔다. 시야·밝기·길찾기가 받는다
    public struct DoorChangedEvent
    {
        public int doorId;
        public bool isOpen;
        public int actorId;
        public float time;
    }

    // 대화가 시작되거나 끝났다. GameManager가 받아 게임 진행을 멈춘다
    public struct DialogueStateEvent
    {
        public string dialogueId;
        public bool isActive;
    }

    // 미션 진행 상태가 바뀌었다(Phase 6)
    public struct MissionStateEvent
    {
        public string state;
        public float objectiveProgress;
        public float timeLeft;
        public string result;
    }

    // 탄약이 바뀌었다. HUD가 받는다
    public struct AmmoChangedEvent
    {
        public string weaponId;
        public int mag;
        public int reserve;
    }

    // 일시정지가 켜지거나 꺼졌다
    public struct PauseEvent
    {
        public bool isPaused;
    }
}
