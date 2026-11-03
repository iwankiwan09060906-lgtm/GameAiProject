using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 플레이어의 사격·재장전·전환 입력을 처리한다. 탄약 상태는 WeaponInventory가 갖고,
    // 이 클래스는 입력을 거기에 전달하고 사격 결과를 이벤트로 알린다
    public sealed class WeaponController
    {
        readonly GridMap grid;
        readonly IReadOnlyList<CombatTarget> targets;
        readonly CharacterView view;
        readonly AssetCatalog catalog;
        readonly int ownerId;

        public WeaponInventory Inventory { get; }

        public WeaponController(GridMap grid, IReadOnlyList<CombatTarget> targets, CharacterView view,
            AssetCatalog catalog, WeaponConfig config, int ownerId)
        {
            this.grid = grid;
            this.targets = targets;
            this.view = view;
            this.catalog = catalog;
            this.ownerId = ownerId;
            Inventory = new WeaponInventory(config.weapons, config.switch_delay_s);

            // 시작 무기 그림을 쥐여 주고 HUD가 탄약을 알 수 있게 알린다
            ShowCurrentWeapon();
            PublishAmmo();
        }

        public void Tick(float dt, in PlayerInputState input, Vector2 position, Vector2 aimDir)
        {
            // 재장전이 끝난 프레임에만 탄약 변경을 알린다
            if (Inventory.Tick(dt)) PublishAmmo();

            // 1~3 키로 무기 전환. 실제로 바뀐 경우에만 그림과 탄약 표시를 갱신한다
            if (input.WeaponSelect > 0 && Inventory.Switch(input.WeaponSelect - 1))
            {
                ShowCurrentWeapon();
                PublishAmmo();
            }

            if (input.ReloadPressed) Inventory.TryReload();

            // 권총류는 누른 순간 한 발, 소총은 누르고 있는 동안 연사
            bool wantsFire = Inventory.Current.Def.auto ? input.FireHeld : input.FirePressed;
            if (wantsFire && Inventory.TryFire())
                Fire(position, aimDir);
        }

        // 한 발을 쏜다: 히트스캔 → 사격 이벤트 → 맞았으면 피해 이벤트
        void Fire(Vector2 position, Vector2 aimDir)
        {
            WeaponDef weapon = Inventory.Current.Def;
            HitResult hit = Hitscan.Cast(grid, position, aimDir, weapon.range_m, targets, Team.Player);

            view.PlayShoot();

            // 총구 위치. 총구가 벽 안으로 들어간 경우(벽에 바짝 붙어 쏠 때)에는 몸 중심에서 쏜 것으로 친다
            Vector2 muzzle = view.MuzzlePosition(position);
            if (!LineOfSight.IsClear(grid, position, muzzle, RayMask.Bullet)) muzzle = position;

            EventBus.Publish(new ShotFiredEvent
            {
                shooterId = ownerId,
                muzzlePos = muzzle,
                dir = aimDir,
                weaponId = weapon.id,
                endPos = hit.End,
            });
            PublishAmmo();

            if (hit.HitTarget)
            {
                EventBus.Publish(new DamageEvent
                {
                    targetId = hit.TargetId,
                    amount = weapon.damage,
                    attackerId = ownerId,
                    hitPoint = hit.End,
                    kind = DamageKind.Bullet,
                    time = Time.time,
                });
            }
        }

        // 손에 든 총 그림을 현재 무기(held_{id})로 바꾼다
        void ShowCurrentWeapon()
        {
            view.SetHeld(catalog.GetSprite("held_" + Inventory.Current.Def.id));
        }

        void PublishAmmo()
        {
            WeaponSlot slot = Inventory.Current;
            EventBus.Publish(new AmmoChangedEvent { weaponId = slot.Def.id, mag = slot.Mag, reserve = slot.Reserve });
        }
    }
}
