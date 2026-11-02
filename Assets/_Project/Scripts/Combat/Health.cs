using UnityEngine;

namespace GameAi
{
    // 방탄이 먼저 1:1로 받고 남은 피해가 체력으로 넘어간다
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

        // 방탄이 받은 양을 돌려준다
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

    public sealed class Health : MonoBehaviour
    {
        HealthModel model;
        CombatTarget target;
        CharacterView view;
        float flashSeconds;
        bool isPlayer;
        string corpseSprite;
        bool deathSent;

        public int Hp { get { return model.Hp; } }
        public int Armor { get { return model.Armor; } }
        public bool IsDead { get { return model.IsDead; } }

        public void Init(CombatTarget owner, int hp, int armor, CharacterView characterView, float flashS, bool player, string corpse)
        {
            target = owner;
            model = new HealthModel(hp, armor);
            view = characterView;
            flashSeconds = flashS;
            isPlayer = player;
            corpseSprite = corpse;
        }

        void OnEnable()
        {
            EventBus.Subscribe<DamageEvent>(OnDamage);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<DamageEvent>(OnDamage);
        }

        void OnDamage(DamageEvent evt)
        {
            if (model == null || evt.targetId != target.Id || model.IsDead) return;
            model.Apply(evt.amount);
            view.Flash(flashSeconds);
            EventBus.Publish(new HealthChangedEvent { entityId = target.Id, hp = model.Hp, armor = model.Armor });
        }

        // 프레임 순서의 '사망 확정' 단계에서 부른다. DeathEvent는 한 번만 보낸다
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
            view.gameObject.SetActive(false);
        }
    }
}
