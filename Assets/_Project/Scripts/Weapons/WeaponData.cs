using UnityEngine;

namespace GameAi
{
    public sealed class WeaponSlot
    {
        public readonly WeaponDef Def;
        public int Mag;
        public int Reserve;

        public WeaponSlot(WeaponDef def)
        {
            Def = def;
            Mag = def.mag_size;
            Reserve = def.reserve;
        }
    }

    // 탄창·재장전·전환 상태. 시간은 Tick으로만 흐른다
    public sealed class WeaponInventory
    {
        readonly WeaponSlot[] slots;
        readonly float switchDelay;
        float fireCooldown;
        float reloadLeft;
        float switchLeft;

        public int CurrentIndex { get; private set; }
        public WeaponSlot Current { get { return slots[CurrentIndex]; } }
        public int Count { get { return slots.Length; } }
        public bool IsReloading { get { return reloadLeft > 0f; } }
        public bool IsSwitching { get { return switchLeft > 0f; } }

        public WeaponInventory(WeaponDef[] defs, float switchDelay)
        {
            slots = new WeaponSlot[defs.Length];
            for (int i = 0; i < defs.Length; i++)
                slots[i] = new WeaponSlot(defs[i]);
            this.switchDelay = switchDelay;
        }

        public WeaponSlot Get(int index)
        {
            return slots[index];
        }

        // 재장전이 끝난 프레임에 true
        public bool Tick(float dt)
        {
            fireCooldown = Mathf.Max(0f, fireCooldown - dt);
            switchLeft = Mathf.Max(0f, switchLeft - dt);
            if (reloadLeft <= 0f) return false;

            reloadLeft -= dt;
            if (reloadLeft > 0f) return false;

            reloadLeft = 0f;
            WeaponSlot slot = Current;
            int amount = Mathf.Min(slot.Def.mag_size - slot.Mag, slot.Reserve);
            slot.Mag += amount;
            slot.Reserve -= amount;
            return true;
        }

        public bool TryFire()
        {
            if (fireCooldown > 0f || IsSwitching || IsReloading) return false;
            WeaponSlot slot = Current;
            if (slot.Mag <= 0) return false;
            slot.Mag--;
            fireCooldown = slot.Def.fire_interval_s;
            return true;
        }

        public bool TryReload()
        {
            if (IsReloading || IsSwitching) return false;
            WeaponSlot slot = Current;
            if (slot.Mag >= slot.Def.mag_size || slot.Reserve <= 0) return false;
            reloadLeft = slot.Def.reload_s;
            return true;
        }

        // 재장전 중 전환하면 재장전을 취소한다. 탄은 끝날 때 옮기므로 잔탄이 그대로 남는다
        public bool Switch(int index)
        {
            if (index < 0 || index >= slots.Length || index == CurrentIndex) return false;
            reloadLeft = 0f;
            CurrentIndex = index;
            switchLeft = switchDelay;
            return true;
        }
    }
}
