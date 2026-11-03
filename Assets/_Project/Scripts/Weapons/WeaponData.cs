using UnityEngine;

namespace GameAi
{
    // 무기 한 자루의 현재 탄약 상태. 수치(피해·사거리 등)는 Def, 변하는 값(탄창·예비)은 여기에 둔다
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

    // 3종 무기의 탄창·재장전·전환 상태(7.3). 시간은 Tick(dt)로만 흐르는 순수 C# 클래스라 테스트하기 쉽다
    public sealed class WeaponInventory
    {
        readonly WeaponSlot[] slots;
        readonly float switchDelay;

        // 다음 발사까지 남은 시간
        float fireCooldown;

        // 재장전이 끝날 때까지 남은 시간(0이면 재장전 중이 아님)
        float reloadLeft;

        // 전환 지연이 끝날 때까지 남은 시간
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

        // 시간을 흘린다. 재장전이 이번 프레임에 끝났으면 true
        public bool Tick(float dt)
        {
            fireCooldown = Mathf.Max(0f, fireCooldown - dt);
            switchLeft = Mathf.Max(0f, switchLeft - dt);
            if (reloadLeft <= 0f) return false;

            reloadLeft -= dt;
            if (reloadLeft > 0f) return false;

            // 재장전 완료: 탄창의 부족분만큼 예비에서 옮긴다. 예비가 모자라면 있는 만큼만
            reloadLeft = 0f;
            WeaponSlot slot = Current;
            int amount = Mathf.Min(slot.Def.mag_size - slot.Mag, slot.Reserve);
            slot.Mag += amount;
            slot.Reserve -= amount;
            return true;
        }

        // 발사를 시도한다. 간격·전환·재장전·빈 탄창 중 하나라도 걸리면 false. 성공하면 탄창이 1 줄어든다
        public bool TryFire()
        {
            if (fireCooldown > 0f || IsSwitching || IsReloading) return false;
            WeaponSlot slot = Current;
            if (slot.Mag <= 0) return false;
            slot.Mag--;
            fireCooldown = slot.Def.fire_interval_s;
            return true;
        }

        // 재장전을 시작한다. 탄창이 가득 찼거나 예비가 없으면 false
        public bool TryReload()
        {
            if (IsReloading || IsSwitching) return false;
            WeaponSlot slot = Current;
            if (slot.Mag >= slot.Def.mag_size || slot.Reserve <= 0) return false;
            reloadLeft = slot.Def.reload_s;
            return true;
        }

        // 무기를 바꾼다. 재장전 중이면 취소한다. 탄은 재장전이 끝날 때 옮기므로 잔탄이 그대로 남는다
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
