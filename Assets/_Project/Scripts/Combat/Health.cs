using UnityEngine;

namespace GameAi
{
    // 체력과 방탄 계산(7.2). 순수 C#이라 테스트하기 쉽다.
    // 피해는 방탄이 먼저 1:1로 받고, 남은 피해가 체력으로 넘어간다
    public sealed class HealthModel
    {
        public int Hp { get; private set; }
        public int Armor { get; private set; }
        public bool IsDead { get; private set; }

        public HealthModel(int hp, int armor)
        {
            Hp = hp;
            Armor = armor;
        }

        // 피해를 적용하고, 방탄이 받아낸 양을 돌려준다(방탄 피격음을 고를 때 쓴다)
        public int Apply(int amount)
        {
            if (IsDead || amount <= 0) return 0;
            int toArmor = Mathf.Min(Armor, amount);
            Armor -= toArmor;
            Hp = Mathf.Max(0, Hp - (amount - toArmor));
            if (Hp == 0) IsDead = true;
            return toArmor;
        }
    }

    // HealthModel을 게임 오브젝트에 붙이는 컴포넌트. DamageEvent를 받아 체력을 깎고,
    // 사망을 확정하는 시점에 DeathEvent를 한 번만 보낸다(5.3)
    public sealed class Health : MonoBehaviour
    {
        HealthModel model;
        CombatTarget target;
        CharacterView view;

        // 피격 시 몸이 빨갛게 변하는 시간(초)
        float flashSeconds;
        bool isPlayer;

        // 사망 시 놓을 시체 그림 이름
        string corpseSprite;

        // DeathEvent를 이미 보냈는지. 한 번만 보내려고 확인한다
        bool deathSent;

        public int Hp { get { return model.Hp; } }
        public int Armor { get { return model.Armor; } }
        public bool IsDead { get { return model.IsDead; } }
        // 부하 시험에서만 켠다(피해 무시)
        public bool Invulnerable { get; set; }

        public void Init(CombatTarget owner, int hp, int armor, CharacterView characterView, float flashS, bool player, string corpse)
        {
            target = owner;
            model = new HealthModel(hp, armor);
            view = characterView;
            flashSeconds = flashS;
            isPlayer = player;
            corpseSprite = corpse;
        }

        // 구독은 OnEnable, 해제는 OnDisable(5.1)
        void OnEnable()
        {
            EventBus.Subscribe<DamageEvent>(OnDamage);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<DamageEvent>(OnDamage);
        }

        // 모든 Health가 모든 DamageEvent를 받으므로, 자기 대상이 아니면 무시한다
        void OnDamage(DamageEvent evt)
        {
            if (model == null || evt.targetId != target.Id || model.IsDead || Invulnerable) return;
            model.Apply(evt.amount);
            view.Flash(flashSeconds);
            // 적이 총에 맞으면 비명(근접 제압은 소리 없음, 7.5)
            if (!isPlayer && evt.kind == DamageKind.Bullet) WorldEvents.Raise("SND_SCREAM", target.Position, target.Id);
            EventBus.Publish(new HealthChangedEvent { entityId = target.Id, hp = model.Hp, armor = model.Armor });
        }

        // 프레임 순서의 '사망 확정'(7단계)에서 부른다.
        // 피해를 받은 프레임이 아니라 이 시점에 사망을 알려서, 같은 프레임의 다른 사건과 순서가 어긋나지 않는다
        public void Tick()
        {
            if (!model.IsDead || deathSent) return;
            deathSent = true;
            target.Alive = false;
            EventBus.Publish(new DeathEvent
            {
                entityId = target.Id,
                isPlayer = isPlayer,
                position = target.Position,
                corpseSprite = corpseSprite,
                rotationDeg = view.AimDeg,
                time = Time.time,
            });

            // 살아 있던 몸 그림은 숨기고, 시체 그림은 CombatFx가 따로 놓는다
            view.gameObject.SetActive(false);
        }
    }
}
