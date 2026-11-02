using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GameAi.Tests
{
    public class PlayerAndVisionTests
    {
        const float Radius = 0.4f;

        static readonly PlayerConfig Config = new PlayerConfig();

        [TestCase(MoveMode.Walk, 3.5f)]
        [TestCase(MoveMode.Run, 5.25f)]
        [TestCase(MoveMode.Crouch, 1.75f)]
        public void 이동_모드별_속도(MoveMode mode, float speed)
        {
            Assert.AreEqual(speed, PlayerMotor.SpeedFor(mode, Config), 1e-4f);
        }

        [Test]
        public void 웅크리기가_달리기보다_우선()
        {
            Assert.AreEqual(MoveMode.Crouch, PlayerMotor.ModeFor(true, true));
            Assert.AreEqual(MoveMode.Run, PlayerMotor.ModeFor(true, false));
            Assert.AreEqual(MoveMode.Walk, PlayerMotor.ModeFor(false, false));
        }

        [Test]
        public void 대각선도_같은_속도()
        {
            Assert.AreEqual(3.5f, PlayerMotor.Velocity(new Vector2(1f, 1f), 3.5f).magnitude, 1e-4f);
            Assert.AreEqual(Vector2.zero, PlayerMotor.Velocity(Vector2.zero, 3.5f));
        }

        [Test]
        public void 벽을_뚫지_못하고_반지름만큼_떨어져_멈춘다()
        {
            var grid = new GridMap(6, 3);
            grid.SetTile(3, 1, TileType.Wall);
            Vector2 end = PlayerMotor.Move(grid, new Vector2(1.5f, 1.5f), new Vector2(5f, 0f), Radius);
            Assert.AreEqual(3f - Radius, end.x, 1e-3f);
            Assert.AreEqual(1.5f, end.y, 1e-3f);
        }

        [Test]
        public void 벽을_따라_미끄러진다()
        {
            var grid = new GridMap(6, 6);
            for (int y = 0; y < 6; y++) grid.SetTile(3, y, TileType.Wall);
            Vector2 end = PlayerMotor.Move(grid, new Vector2(2.5f, 1.5f), new Vector2(1f, 1f), Radius);
            Assert.AreEqual(3f - Radius, end.x, 1e-3f);
            Assert.AreEqual(2.5f, end.y, 1e-3f);
        }

        [Test]
        public void 닫힌_문은_막고_열린_문은_지나간다()
        {
            var grid = new GridMap(6, 3);
            grid.SetTile(3, 1, TileType.Door);
            var start = new Vector2(1.5f, 1.5f);
            Assert.Less(PlayerMotor.Move(grid, start, new Vector2(3f, 0f), Radius).x, 3f);
            grid.SetDoorOpen(3, 1, true);
            Assert.AreEqual(4.5f, PlayerMotor.Move(grid, start, new Vector2(3f, 0f), Radius).x, 1e-3f);
        }

        [Test]
        public void 풀숲은_이동을_막는다()
        {
            var grid = new GridMap(6, 3);
            grid.SetTile(3, 1, TileType.Foliage);
            Assert.Less(PlayerMotor.Move(grid, new Vector2(1.5f, 1.5f), new Vector2(3f, 0f), Radius).x, 3f);
        }

        [Test]
        public void 산_적과_겹치지_않고_시체는_지나간다()
        {
            var enemy = new CombatTarget { Id = 1, Position = new Vector2(3f, 1f), Radius = 0.4f };
            var list = new List<CombatTarget> { enemy };
            Vector2 pushed = PlayerMotor.PushOutOfTargets(new Vector2(2.7f, 1f), Radius, list, 0);
            Assert.AreEqual(0.8f, Vector2.Distance(pushed, enemy.Position), 1e-3f);

            enemy.Alive = false;
            Assert.AreEqual(new Vector2(2.7f, 1f), PlayerMotor.PushOutOfTargets(new Vector2(2.7f, 1f), Radius, list, 0));
        }

        [Test]
        public void 카메라_앞당김은_커서_쪽_30퍼센트_최대_3m()
        {
            Vector2 near = CameraFollow.LookAhead(Vector2.zero, new Vector2(5f, 0f), 0.3f, 3f);
            Assert.AreEqual(1.5f, near.x, 1e-4f);
            Vector2 far = CameraFollow.LookAhead(Vector2.zero, new Vector2(30f, 40f), 0.3f, 3f);
            Assert.AreEqual(3f, far.magnitude, 1e-4f);
        }

        static PlayerVision Vision(GridMap grid)
        {
            return new PlayerVision(grid, 90f, 12f, 90);
        }

        [Test]
        public void 시야는_90도_12m다()
        {
            var grid = new GridMap(40, 40);
            PlayerVision vision = Vision(grid);
            var center = new Vector2(20.5f, 20.5f);
            vision.Compute(center, Vector2.right);

            Assert.IsTrue(vision.IsVisible(30, 20));
            Assert.IsFalse(vision.IsVisible(34, 20), "12m 밖");
            Assert.IsFalse(vision.IsVisible(14, 20), "등 뒤");
            Assert.IsFalse(vision.IsVisible(20, 28), "옆");
            Assert.IsTrue(vision.IsVisible(new Vector2(25f, 24f)));
        }

        [Test]
        public void 벽은_보이지만_벽_뒤는_보이지_않는다()
        {
            var grid = new GridMap(20, 9);
            for (int y = 0; y < 9; y++) grid.SetTile(8, y, TileType.Wall);
            PlayerVision vision = Vision(grid);
            vision.Compute(new Vector2(2.5f, 4.5f), Vector2.right);

            Assert.IsTrue(vision.IsVisible(8, 4));
            Assert.IsFalse(vision.IsVisible(9, 4));
            Assert.IsFalse(vision.IsVisible(12, 4));
        }

        [Test]
        public void 낮은_엄폐물과_창문_너머는_보인다()
        {
            var grid = new GridMap(20, 9);
            grid.SetTile(6, 4, TileType.CoverLow);
            grid.SetTile(8, 4, TileType.Window);
            PlayerVision vision = Vision(grid);
            vision.Compute(new Vector2(2.5f, 4.5f), Vector2.right);
            Assert.IsTrue(vision.IsVisible(10, 4));
        }

        [Test]
        public void 문을_열면_그_너머가_보인다()
        {
            var grid = new GridMap(20, 9);
            for (int y = 0; y < 9; y++) grid.SetTile(8, y, TileType.Wall);
            grid.SetTile(8, 4, TileType.Door);
            PlayerVision vision = Vision(grid);

            vision.Compute(new Vector2(2.5f, 4.5f), Vector2.right);
            Assert.IsFalse(vision.IsVisible(10, 4));

            grid.SetDoorOpen(8, 4, true);
            vision.Compute(new Vector2(2.5f, 4.5f), Vector2.right);
            Assert.IsTrue(vision.IsVisible(10, 4));
        }

        [Test]
        public void 탐색_기억은_보이던_칸만_남고_재시작하면_없다()
        {
            var grid = new GridMap(30, 9);
            PlayerVision vision = Vision(grid);
            var fog = new FogOfWar(grid);
            Assert.AreEqual(FogState.Unexplored, fog.GetState(5, 4));

            vision.Compute(new Vector2(2.5f, 4.5f), Vector2.right);
            fog.Update(vision);
            Assert.AreEqual(FogState.Visible, fog.GetState(5, 4));
            Assert.AreEqual(FogState.Unexplored, fog.GetState(25, 4));

            vision.Compute(new Vector2(2.5f, 4.5f), Vector2.left);
            fog.Update(vision);
            Assert.AreEqual(FogState.Memory, fog.GetState(5, 4));

            Assert.AreEqual(FogState.Unexplored, new FogOfWar(grid).GetState(5, 4));
        }

        [Test]
        public void 안개_픽셀은_상태별_색을_쓴다()
        {
            var grid = new GridMap(30, 9);
            PlayerVision vision = Vision(grid);
            var fog = new FogOfWar(grid);
            vision.Compute(new Vector2(2.5f, 4.5f), Vector2.right);
            fog.Update(vision);

            var pixels = new Color32[grid.Width * grid.Height];
            var unexplored = new Color32(0, 0, 0, 255);
            var memory = new Color32(25, 25, 28, 166);
            fog.WritePixels(pixels, unexplored, memory);
            Assert.AreEqual(0, pixels[grid.Index(5, 4)].a);
            Assert.AreEqual(255, pixels[grid.Index(25, 4)].a);

            vision.Compute(new Vector2(2.5f, 4.5f), Vector2.left);
            fog.Update(vision);
            fog.WritePixels(pixels, unexplored, memory);
            Assert.AreEqual(166, pixels[grid.Index(5, 4)].a);
        }
    }
}
