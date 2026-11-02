using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 플레이어의 사격·재장전·전환 입력을 처리한다
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
            ShowCurrentWeapon();
            PublishAmmo();
        }

        public void Tick(float dt, in PlayerInputState input, Vector2 position, Vector2 aimDir)
        {
            if (Inventory.Tick(dt)) PublishAmmo();

            if (input.WeaponSelect > 0 && Inventory.Switch(input.WeaponSelect - 1))
            {
                ShowCurrentWeapon();
                PublishAmmo();
            }

            if (input.ReloadPressed) Inventory.TryReload();

            bool wantsFire = Inventory.Current.Def.auto ? input.FireHeld : input.FirePressed;
            if (wantsFire && Inventory.TryFire())
                Fire(position, aimDir);
        }

        void Fire(Vector2 position, Vector2 aimDir)
        {
            WeaponDef weapon = Inventory.Current.Def;
            HitResult hit = Hitscan.Cast(grid, position, aimDir, weapon.range_m, targets, Team.Player);

            view.PlayShoot();
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
