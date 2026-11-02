using UnityEngine;

namespace GameAi
{
    public enum NoiseKind { Footstep, Shot, Decoy }
    public enum DamageKind { Bullet, Melee }
    public enum AlertKind { Shout, Radio }

    public struct NoiseEvent
    {
        public Vector2 position;
        public NoiseKind kind;
        public float radius;
        public int sourceId;
        public float time;
    }

    public struct DamageEvent
    {
        public int targetId;
        public int amount;
        public int attackerId;
        public Vector2 hitPoint;
        public DamageKind kind;
        public float time;
    }

    public struct HealthChangedEvent
    {
        public int entityId;
        public int hp;
        public int armor;
    }

    public struct DeathEvent
    {
        public int entityId;
        public bool isPlayer;
        public Vector2 position;
        public string corpseSprite;
        public float rotationDeg;
        public float time;
    }

    public struct ShotFiredEvent
    {
        public int shooterId;
        public Vector2 muzzlePos;
        public Vector2 dir;
        public string weaponId;
        public Vector2 endPos;
    }

    public struct EnemyStateChangedEvent
    {
        public int enemyId;
        public EnemyState from;
        public EnemyState to;
        public string reason;
        public float time;
    }

    public struct DetectionEvent
    {
        public int enemyId;
        public float time;
    }

    public struct AlertEvent
    {
        public AlertKind kind;
        public Vector2 position;
        public float time;
        public int senderId;
    }

    public struct BarkEvent
    {
        public int enemyId;
        public string text;
    }

    public struct DoorChangedEvent
    {
        public int doorId;
        public bool isOpen;
        public int actorId;
        public float time;
    }

    public struct DialogueStateEvent
    {
        public string dialogueId;
        public bool isActive;
    }

    public struct MissionStateEvent
    {
        public string state;
        public float objectiveProgress;
        public float timeLeft;
        public string result;
    }

    public struct AmmoChangedEvent
    {
        public string weaponId;
        public int mag;
        public int reserve;
    }

    public struct PauseEvent
    {
        public bool isPaused;
    }
}
