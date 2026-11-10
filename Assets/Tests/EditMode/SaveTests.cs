using System.IO;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameAi.Tests
{
    // 7.13 저장·복구
    public class SaveTests
    {
        string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "gameai_save_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        [Test]
        public void 저장하고_다시_읽는다()
        {
            var system = new SaveSystem(dir);
            var data = new SaveData();
            data.settings.bgm = 0.3f;
            data.Submit(new MissionRecord { map = "map_01", difficulty = "normal", clear_time_s = 183.412f, detections = 1, kills = 3 });
            Assert.IsTrue(system.Save(data));
            SaveData loaded = system.Load(out SaveLoadStatus status);
            Assert.AreEqual(SaveLoadStatus.Ok, status);
            Assert.AreEqual(0.3f, loaded.settings.bgm, 1e-4f);
            Assert.AreEqual(183.412f, loaded.Find("map_01", "normal").clear_time_s, 1e-3f);
        }

        [Test]
        public void 두번째_저장은_이전_파일을_bak으로_남긴다()
        {
            var system = new SaveSystem(dir);
            system.Save(new SaveData());
            system.Save(new SaveData());
            Assert.IsTrue(File.Exists(system.FilePath + ".bak"));
            Assert.IsFalse(File.Exists(system.FilePath + ".tmp"));
        }

        [Test]
        public void 한번_실패하면_재시도해서_성공()
        {
            var system = new SaveSystem(dir) { FailNextWrites = 1 };
            Assert.IsTrue(system.Save(new SaveData()));
        }

        [Test]
        public void 두번_실패하면_기록_없이_진행()
        {
            var system = new SaveSystem(dir) { FailNextWrites = 2 };
            Assert.IsFalse(system.Save(new SaveData()));
            Assert.IsFalse(File.Exists(system.FilePath));
        }

        [Test]
        public void 손상되면_corrupt로_보존하고_bak에서_복원()
        {
            var system = new SaveSystem(dir);
            var good = new SaveData();
            good.settings.sfx = 0.4f;
            system.Save(good);
            system.Save(good);
            File.WriteAllText(system.FilePath, "{이건 망가진 파일");
            SaveData loaded = system.Load(out SaveLoadStatus status);
            Assert.AreEqual(SaveLoadStatus.RestoredFromBackup, status);
            Assert.AreEqual(0.4f, loaded.settings.sfx, 1e-4f);
            Assert.IsTrue(File.Exists(system.FilePath + ".corrupt"));
        }

        [Test]
        public void 백업도_없으면_동의를_받은_뒤_초기화()
        {
            var system = new SaveSystem(dir);
            File.WriteAllText(system.FilePath, "망가짐");
            system.Load(out SaveLoadStatus status);
            Assert.AreEqual(SaveLoadStatus.CorruptNeedsConsent, status);
            Assert.IsTrue(File.Exists(system.FilePath + ".corrupt"));
            Assert.IsTrue(system.ResetWithConsent());
            system.Load(out status);
            Assert.AreEqual(SaveLoadStatus.Ok, status);
        }

        [Test]
        public void 더_좋은_기록만_바꾼다()
        {
            var data = new SaveData();
            Assert.IsTrue(data.Submit(new MissionRecord { map = "m", difficulty = "normal", clear_time_s = 100f }));
            Assert.IsFalse(data.Submit(new MissionRecord { map = "m", difficulty = "normal", clear_time_s = 120f }));
            Assert.IsTrue(data.Submit(new MissionRecord { map = "m", difficulty = "hard", clear_time_s = 120f }), "난이도별 따로");
            Assert.IsTrue(data.Submit(new MissionRecord { map = "m", difficulty = "normal", clear_time_s = 90f }));
            Assert.AreEqual(2, data.records.Count);
        }
    }
}
