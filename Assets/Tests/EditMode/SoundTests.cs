using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GameAi.Tests
{
    // 소리 전파(7.6)와 소음 발생(발소리·깡통·이벤트 ID)
    public class SoundTests
    {
        static GridMap Open(int w = 21, int h = 5)
        {
            return new GridMap(w, h);
        }

        static readonly Vector2 Source = new Vector2(0.5f, 2.5f);

        [Test]
        public void 거리에_따라_선형으로_줄어든다()
        {
            GridMap grid = Open();
            Assert.AreEqual(1f, SoundPropagation.Intensity(grid, Source, 10f, Source, 0.5f), 1e-4f);
            Assert.AreEqual(0.5f, SoundPropagation.Intensity(grid, Source, 10f, Source + Vector2.right * 5f, 0.5f), 1e-4f);
            Assert.AreEqual(0f, SoundPropagation.Intensity(grid, Source, 10f, Source + Vector2.right * 11f, 0.5f));
        }

        [Test]
        public void 벽_하나는_절반_두개는_사분의일()
        {
            GridMap grid = Open();
            grid.SetTile(3, 2, TileType.Wall);
            Vector2 listener = Source + Vector2.right * 5f;
            Assert.AreEqual(0.25f, SoundPropagation.Intensity(grid, Source, 10f, listener, 0.5f), 1e-4f);
            grid.SetTile(4, 2, TileType.Floor);
            grid.SetTile(5, 2, TileType.Wall);
            // 떨어진 벽 두 개
            Assert.AreEqual(2, SoundPropagation.CountWalls(grid, Source, listener + Vector2.right));
        }

        [Test]
        public void 이어진_벽칸은_벽_하나로_센다()
        {
            GridMap grid = Open();
            grid.SetTile(3, 2, TileType.Wall);
            grid.SetTile(4, 2, TileType.Wall);
            Assert.AreEqual(1, SoundPropagation.CountWalls(grid, Source, Source + Vector2.right * 6f));
        }

        [Test]
        public void 창문과_닫힌_문은_감쇠_열린_문은_그대로()
        {
            GridMap grid = Open();
            Vector2 listener = Source + Vector2.right * 5f;
            grid.SetTile(3, 2, TileType.Window);
            Assert.AreEqual(1, SoundPropagation.CountWalls(grid, Source, listener));
            grid.SetTile(3, 2, TileType.Door);
            Assert.AreEqual(1, SoundPropagation.CountWalls(grid, Source, listener));
            grid.SetDoorOpen(3, 2, true);
            Assert.AreEqual(0, SoundPropagation.CountWalls(grid, Source, listener));
        }

        [Test]
        public void 엄폐물과_풀숲은_소리를_막지_않는다()
        {
            GridMap grid = Open();
            grid.SetTile(2, 2, TileType.CoverHigh);
            grid.SetTile(3, 2, TileType.CoverLow);
            grid.SetTile(4, 2, TileType.Foliage);
            Assert.AreEqual(0, SoundPropagation.CountWalls(grid, Source, Source + Vector2.right * 6f));
        }

        [Test]
        public void 반쪽_칸_맵에서도_m_단위로_계산한다()
        {
            var grid = new GridMap(40, 6, 0.5f);
            grid.SetTile(10, 3, TileType.Wall);
            var s = new Vector2(1.25f, 1.75f);
            Assert.AreEqual(0.25f, SoundPropagation.Intensity(grid, s, 10f, s + Vector2.right * 5f, 0.5f), 1e-4f);
        }

        [Test]
        public void 임계값_0점2()
        {
            GridMap grid = Open();
            float r = 10f;
            // 거리 8m → 0.2 (반응), 거리 8.5m → 0.15 (무반응)
            Assert.GreaterOrEqual(SoundPropagation.Intensity(grid, Source, r, Source + Vector2.right * 8f, 0.5f), 0.2f - 1e-4f);
            Assert.Less(SoundPropagation.Intensity(grid, Source, r, Source + Vector2.right * 8.5f, 0.5f), 0.2f);
        }

        [Test]
        public void 바닥_배율()
        {
            var mult = new FloorMult();
            Assert.AreEqual(1.2f, mult.For(FloorType.Tile));
            Assert.AreEqual(1f, mult.For(FloorType.Wood));
            Assert.AreEqual(0.6f, mult.For(FloorType.Carpet));
            Assert.AreEqual(0.8f, mult.For(FloorType.Outdoor));
        }

        [Test]
        public void 발소리는_이동_중에만_간격마다_나고_카펫에서_작다()
        {
            var grid = new GridMap(4, 4);
            grid.SetFloor(1, 1, FloorType.Carpet);
            var noise = new PlayerNoise(grid, new PlayerConfig(), new FloorMult());
            var events = new List<WorldEvent>();
            System.Action<WorldEvent> handler = e => events.Add(e);
            EventBus.Subscribe(handler);
            try
            {
                Assert.IsFalse(noise.Tick(0.1f, false, MoveMode.Walk, new Vector2(1.5f, 1.5f)));
                Assert.IsTrue(noise.Tick(0.1f, true, MoveMode.Walk, new Vector2(1.5f, 1.5f)));
                Assert.IsFalse(noise.Tick(0.3f, true, MoveMode.Walk, new Vector2(1.5f, 1.5f)));
                Assert.IsTrue(noise.Tick(0.31f, true, MoveMode.Walk, new Vector2(1.5f, 1.5f)));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual("SND_FOOTSTEP", events[0].code);
            Assert.AreEqual(6f * 0.6f, events[0].radius, 1e-4f);
        }

        [Test]
        public void 모드별_발소리_반경()
        {
            var config = new PlayerConfig();
            Assert.AreEqual(6f, PlayerNoise.RadiusFor(MoveMode.Walk, config));
            Assert.AreEqual(9f, PlayerNoise.RadiusFor(MoveMode.Run, config));
            Assert.AreEqual(3f, PlayerNoise.RadiusFor(MoveMode.Crouch, config));
        }

        [Test]
        public void 깡통은_웅크리면_소리가_없고_간격을_지킨다()
        {
            var grid = new GridMap(4, 4);
            var cans = new CanSystem(grid, new List<Vector2Int> { new Vector2Int(2, 1) }, 2f);
            Vector2 off = new Vector2(0.5f, 1.5f), on = new Vector2(2.5f, 1.5f);
            cans.Tick(off, MoveMode.Walk, 0f);
            Assert.AreEqual(-1, cans.Tick(on, MoveMode.Crouch, 0.1f));
            cans.Tick(off, MoveMode.Walk, 0.2f);
            Assert.AreEqual(0, cans.Tick(on, MoveMode.Walk, 0.3f));
            cans.Tick(off, MoveMode.Walk, 0.4f);
            Assert.AreEqual(-1, cans.Tick(on, MoveMode.Walk, 1.0f));
            cans.Tick(off, MoveMode.Walk, 2.2f);
            Assert.AreEqual(0, cans.Tick(on, MoveMode.Run, 2.5f));
        }

        [Test]
        public void 소리_이벤트는_고유_ID와_소음_이벤트를_함께_보낸다()
        {
            WorldEvents.Reset(new EventTypeTable(new EventTypeConfig()));
            int noiseCount = 0;
            System.Action<NoiseEvent> handler = e => noiseCount++;
            EventBus.Subscribe(handler);
            int a, b, c;
            try
            {
                a = WorldEvents.Raise("SND_DOOR", Vector2.zero, 0);
                b = WorldEvents.Raise("SND_DOOR", Vector2.zero, 0);
                c = WorldEvents.Raise("CLU_BLOOD", Vector2.zero, 0);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
            Assert.AreEqual(1, a);
            Assert.AreEqual(2, b);
            Assert.AreEqual(3, c);
            Assert.AreEqual(2, noiseCount);
        }
    }
}
