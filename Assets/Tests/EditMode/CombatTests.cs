using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GameAi.Tests
{
    // 무기 탄창·재장전·전환(7.3), 히트스캔 명중 판정(가장 가까운 하나, 막는 칸, 사거리),
    // 근접 제압 조건(7.5), 오브젝트 풀 상한을 확인한다
    public class CombatTests
    {
        static WeaponDef Pistol()
        {
            return new WeaponDef { id = "pistol", damage = 20, range_m = 15, fire_interval_s = 0.3f, mag_size = 12, reserve = 36, reload_s = 1.5f };
        }

        static WeaponDef Rifle()
        {
            return new WeaponDef { id = "rifle", damage = 30, range_m = 30, fire_interval_s = 0.12f, mag_size = 30, reserve = 90, reload_s = 2.5f, auto = true };
        }

        static WeaponInventory Inventory()
        {
            return new WeaponInventory(new[] { Pistol(), Rifle() }, 0.3f);
        }

        [Test]
        public void 발사_간격이_지나야_다시_쏜다()
        {
            WeaponInventory inv = Inventory();
            Assert.IsTrue(inv.TryFire());
            Assert.IsFalse(inv.TryFire());
            inv.Tick(0.29f);
            Assert.IsFalse(inv.TryFire());
            inv.Tick(0.02f);
            Assert.IsTrue(inv.TryFire());
            Assert.AreEqual(10, inv.Current.Mag);
        }

        [Test]
        public void 빈_탄창은_쏘지_못한다()
        {
            WeaponInventory inv = Inventory();
            inv.Current.Mag = 0;
            Assert.IsFalse(inv.TryFire());
        }

        [Test]
        public void 재장전은_부족분만큼_예비에서_채운다()
        {
            WeaponInventory inv = Inventory();
            inv.Current.Mag = 5;
            Assert.IsTrue(inv.TryReload());
            Assert.IsFalse(inv.TryFire());
            Assert.IsFalse(inv.Tick(1.4f));
            Assert.IsTrue(inv.Tick(0.2f));
            Assert.AreEqual(12, inv.Current.Mag);
            Assert.AreEqual(29, inv.Current.Reserve);
        }

        [Test]
        public void 예비가_모자라면_있는_만큼만_채운다()
        {
            WeaponInventory inv = Inventory();
            inv.Current.Mag = 0;
            inv.Current.Reserve = 4;
            inv.TryReload();
            inv.Tick(2f);
            Assert.AreEqual(4, inv.Current.Mag);
            Assert.AreEqual(0, inv.Current.Reserve);
        }

        [Test]
        public void 탄창이_가득하거나_예비가_없으면_재장전하지_않는다()
        {
            WeaponInventory inv = Inventory();
            Assert.IsFalse(inv.TryReload());
            inv.Current.Mag = 1;
            inv.Current.Reserve = 0;
            Assert.IsFalse(inv.TryReload());
        }

        [Test]
        public void 재장전_중_전환하면_취소되고_잔탄_유지()
        {
            WeaponInventory inv = Inventory();
            inv.Current.Mag = 3;
            inv.TryReload();
            inv.Tick(1f);
            Assert.IsTrue(inv.Switch(1));
            Assert.IsFalse(inv.IsReloading);

            inv.Tick(2f);
            WeaponSlot pistol = inv.Get(0);
            Assert.AreEqual(3, pistol.Mag);
            Assert.AreEqual(36, pistol.Reserve);
        }

        [Test]
        public void 전환_지연_동안은_쏘지_못한다()
        {
            WeaponInventory inv = Inventory();
            inv.Switch(1);
            Assert.IsFalse(inv.TryFire());
            inv.Tick(0.31f);
            Assert.IsTrue(inv.TryFire());
        }

        [Test]
        public void 같은_무기로는_전환하지_않는다()
        {
            WeaponInventory inv = Inventory();
            Assert.IsFalse(inv.Switch(0));
            Assert.IsFalse(inv.Switch(5));
            Assert.IsFalse(inv.IsSwitching);
        }

        static CombatTarget Enemy(int id, float x, float y = 1.5f)
        {
            return new CombatTarget { Id = id, Team = Team.Enemy, Position = new Vector2(x, y), Radius = 0.4f };
        }

        static HitResult Shoot(GridMap grid, params CombatTarget[] targets)
        {
            return Hitscan.Cast(grid, new Vector2(0.5f, 1.5f), Vector2.right, 15f, targets, Team.Player);
        }

        static GridMap Hall(TileType middle = TileType.Floor, bool doorOpen = false)
        {
            var grid = new GridMap(9, 3);
            grid.SetTile(3, 1, middle);
            grid.SetDoorOpen(3, 1, doorOpen);
            return grid;
        }

        [Test]
        public void 가장_가까운_적_하나만_맞는다()
        {
            HitResult hit = Shoot(Hall(), Enemy(2, 6.5f), Enemy(1, 4.5f));
            Assert.IsTrue(hit.HitTarget);
            Assert.AreEqual(1, hit.TargetId);
            Assert.AreEqual(4.1f, hit.End.x, 1e-3f);
        }

        [TestCase(TileType.Wall)]
        [TestCase(TileType.Window)]
        [TestCase(TileType.CoverLow)]
        [TestCase(TileType.CoverHigh)]
        [TestCase(TileType.Door)]
        public void 막는_칸_뒤의_적은_맞지_않는다(TileType type)
        {
            HitResult hit = Shoot(Hall(type), Enemy(1, 5.5f));
            Assert.IsFalse(hit.HitTarget);
            Assert.AreEqual(3f, hit.End.x, 1e-3f);
        }

        [Test]
        public void 풀숲과_열린_문은_총알이_지나간다()
        {
            Assert.IsTrue(Shoot(Hall(TileType.Foliage), Enemy(1, 5.5f)).HitTarget);
            Assert.IsTrue(Shoot(Hall(TileType.Door, true), Enemy(1, 5.5f)).HitTarget);
        }

        [Test]
        public void 사거리_밖은_맞지_않는다()
        {
            var grid = new GridMap(30, 3);
            Assert.IsFalse(Shoot(grid, Enemy(1, 20f)).HitTarget);
        }

        [Test]
        public void 뒤쪽_적과_옆으로_빗나간_적은_맞지_않는다()
        {
            HitResult behind = Hitscan.Cast(Hall(), new Vector2(4.5f, 1.5f), Vector2.right, 15f, new[] { Enemy(1, 2.5f) }, Team.Player);
            Assert.IsFalse(behind.HitTarget);
            Assert.IsFalse(Shoot(Hall(), Enemy(1, 4.5f, 2.5f)).HitTarget);
        }

        [Test]
        public void 죽은_대상과_같은_편은_무시한다()
        {
            CombatTarget dead = Enemy(1, 3.5f);
            dead.Alive = false;
            Assert.IsFalse(Shoot(Hall(), dead).HitTarget);

            HitResult enemyShot = Hitscan.Cast(Hall(), new Vector2(0.5f, 1.5f), Vector2.right, 15f,
                new[] { Enemy(1, 3.5f), new CombatTarget { Id = 0, Team = Team.Player, Position = new Vector2(5.5f, 1.5f), Radius = 0.4f } }, Team.Enemy);
            Assert.AreEqual(0, enemyShot.TargetId);
        }

        static readonly MeleeConfig Melee = new MeleeConfig { range_m = 1.5f, back_arc_deg = 120f };

        static bool Takedown(Vector2 player, Vector2 facing, EnemyState state = EnemyState.Patrol, float gauge = 0f)
        {
            return MeleeRules.CanTakedown(new GridMap(10, 10), player, new Vector2(5.5f, 5.5f), facing, state, gauge, 100f, Melee);
        }

        [Test]
        public void 뒤에서는_제압되고_앞에서는_안_된다()
        {
            Assert.IsTrue(Takedown(new Vector2(4.5f, 5.5f), Vector2.right));
            Assert.IsFalse(Takedown(new Vector2(6.5f, 5.5f), Vector2.right));
        }

        [Test]
        public void 뒤쪽_부채꼴은_120도다()
        {
            Vector2 enemy = new Vector2(5.5f, 5.5f);
            Assert.IsTrue(Takedown(enemy + MathUtil.FromAngleDeg(180f - 59f), Vector2.right));
            Assert.IsFalse(Takedown(enemy + MathUtil.FromAngleDeg(180f - 61f), Vector2.right));
        }

        [Test]
        public void 거리가_멀면_안_된다()
        {
            Assert.IsTrue(Takedown(new Vector2(4.1f, 5.5f), Vector2.right));
            Assert.IsFalse(Takedown(new Vector2(3.9f, 5.5f), Vector2.right));
        }

        [TestCase(EnemyState.Patrol, 0f, true)]
        [TestCase(EnemyState.Investigate, 50f, true)]
        [TestCase(EnemyState.Investigate, 100f, false)]
        [TestCase(EnemyState.Chase, 0f, false)]
        [TestCase(EnemyState.Engage, 0f, false)]
        [TestCase(EnemyState.Search, 0f, false)]
        public void 발각되지_않은_적만_제압된다(EnemyState state, float gauge, bool expected)
        {
            Assert.AreEqual(expected, Takedown(new Vector2(4.5f, 5.5f), Vector2.right, state, gauge));
        }

        [Test]
        public void 벽_너머로는_제압하지_못한다()
        {
            var grid = new GridMap(10, 10);
            grid.SetTile(5, 5, TileType.Wall);
            Assert.IsFalse(MeleeRules.CanTakedown(grid, new Vector2(4.5f, 5.5f), new Vector2(6.1f, 5.5f), Vector2.right,
                EnemyState.Patrol, 0f, 100f, new MeleeConfig { range_m = 2f, back_arc_deg = 120f }));
        }

        [Test]
        public void 풀은_정해진_개수까지만_만들고_오래된_것을_재사용한다()
        {
            int created = 0;
            var pool = new ObjectPool<object>(3, () => { created++; return new object(); });
            object first = pool.Acquire();
            object second = pool.Acquire();
            pool.Acquire();
            Assert.AreSame(first, pool.Acquire());
            Assert.AreSame(second, pool.Acquire());
            for (int i = 0; i < 1000; i++) pool.Acquire();
            Assert.AreEqual(3, created);
            Assert.AreEqual(3, pool.Count);
        }
    }
}
