using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameAi.Tests
{
    public class ConfigTests
    {
        [Test]
        public void 설정_파일을_읽으면_문서_값과_같다()
        {
            var game = ConfigLoader.Load<GameConfig>("game.json");
            Assert.AreEqual(8.4375f, game.camera_ortho_size, 1e-4f);
            Assert.AreEqual(1.2f, game.interact_range_m, 1e-4f);
            Assert.AreEqual("#FFC873", game.lighting.lamp_color);
            Assert.AreEqual(90f, game.player.fov_deg, 1e-4f);

            var weapons = ConfigLoader.Load<WeaponConfig>("weapons.json");
            Assert.AreEqual(3, weapons.weapons.Length);
            Assert.AreEqual(30, weapons.weapons[2].damage);
            Assert.IsTrue(weapons.weapons[2].auto);
            Assert.AreEqual(120f, weapons.melee.back_arc_deg, 1e-4f);

            var enemies = ConfigLoader.Load<EnemyConfig>("enemies.json");
            Assert.AreEqual(60, enemies.Find("rifleman").hp);
            Assert.IsNull(enemies.Find("없는 종류"));
        }

        [Test]
        public void 모든_설정_키가_채워져_있다()
        {
            foreach (string file in new[] { "game.json", "weapons.json", "enemies.json", "ai.json", "missions.json" })
            {
                string json = ConfigLoader.ReadText(file);
                Assert.IsNotNull(json, file);
            }
            Assert.IsEmpty(ConfigLoader.FindMissingKeys(ConfigLoader.ReadText("game.json"), typeof(GameConfig)));
            Assert.IsEmpty(ConfigLoader.FindMissingKeys(ConfigLoader.ReadText("weapons.json"), typeof(WeaponConfig)));
            Assert.IsEmpty(ConfigLoader.FindMissingKeys(ConfigLoader.ReadText("enemies.json"), typeof(EnemyConfig)));
        }

        [Test]
        public void 누락된_값은_기본값과_경고()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("interact_range_m"));
            string json = ConfigLoader.ReadText("game.json").Replace("\"interact_range_m\": 1.2,", "");
            var config = ConfigLoader.Parse<GameConfig>(json, "테스트");
            Assert.AreEqual(1.2f, config.interact_range_m, 1e-4f);
            Assert.AreEqual(90f, config.player.fov_deg, 1e-4f);
        }

        [Test]
        public void 중첩_필드_누락도_찾는다()
        {
            List<string> missing = ConfigLoader.FindMissingKeys("{\"lighting\":{}}", typeof(GameConfig));
            Assert.Contains("lighting.lamp_color", missing);
        }

        [Test]
        public void 깨진_JSON은_오류_로그와_기본값()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("JSON 형식 오류"));
            var config = ConfigLoader.Parse<GameConfig>("{ 깨진 내용", "테스트");
            Assert.AreEqual(1.2f, config.interact_range_m, 1e-4f);
        }

        [Test]
        public void 없는_파일은_경고와_기본값()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("파일이 없어"));
            var config = ConfigLoader.Load<GameConfig>("없는파일.json");
            Assert.AreEqual(8.4375f, config.camera_ortho_size, 1e-4f);
        }

        [Test]
        public void 빈_카탈로그는_코드_생성_그림으로_돌아간다()
        {
            var catalog = ScriptableObject.CreateInstance<AssetCatalog>();
            foreach (string name in new[] { "tile_wall", "tile_floor_outdoor", "door_closed", "crosshair", "lamp", "casing", "panel_dark" })
                Assert.IsNotNull(catalog.GetSprite(name), name);
            Assert.AreEqual(1, catalog.GetVariants("cover_low").Length);
            Assert.AreEqual(1, catalog.GetVariants("hit_blood").Length);
            Assert.AreEqual(1, catalog.GetFrames("player_walk").Length);
            Assert.IsNull(catalog.GetClip("sfx_door"));
            Assert.IsNull(catalog.GetSprite("held_pistol"));
            Object.DestroyImmediate(catalog);
        }

        [TestCase("tile_wall", 32, 32)]
        [TestCase("door_closed", 32, 8)]
        [TestCase("door_open", 8, 32)]
        [TestCase("casing", 2, 2)]
        [TestCase("lamp", 8, 8)]
        [TestCase("stance_idle", 32, 64)]
        [TestCase("weapon_pistol", 64, 32)]
        [TestCase("icon_detect", 16, 16)]
        public void 코드_생성_그림_크기는_외부_파일과_같다(string name, int width, int height)
        {
            Sprite sprite = ProceduralSprites.Get(name);
            Assert.AreEqual(width, (int)sprite.rect.width);
            Assert.AreEqual(height, (int)sprite.rect.height);
        }

        [Test]
        public void 이름이_같으면_먼저_찾은_것을_쓴다()
        {
            var map = new Dictionary<string, string>();
            Assert.IsTrue(AssetNames.TryAddUnique(map, "blood_1", "첫째"));
            Assert.IsFalse(AssetNames.TryAddUnique(map, "blood_1", "둘째"));
            Assert.AreEqual("첫째", map["blood_1"]);
        }

        [Test]
        public void 이름_규칙에_없는_파일을_구분한다()
        {
            Assert.IsTrue(AssetNames.IsKnown("cover_low_sofa_up_1", false));
            Assert.IsTrue(AssetNames.IsKnown("sfx_door", false));
            Assert.IsTrue(AssetNames.IsKnown("accomplice", true));
            Assert.IsFalse(AssetNames.IsKnown("accomplice", false));
            Assert.IsFalse(AssetNames.IsKnown("tiletest", false));
        }

        [Test]
        public void 번호는_숫자_순서로_정렬한다()
        {
            var names = new List<string> { "hit_blood_10", "hit_blood_2", "hit_blood_1" };
            names.Sort(AssetNames.NaturalCompare);
            CollectionAssert.AreEqual(new[] { "hit_blood_1", "hit_blood_2", "hit_blood_10" }, names);
        }
    }
}
