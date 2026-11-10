using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GameAi
{
    // save.json(9.10)
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public SettingsData settings = new SettingsData();
        public List<MissionRecord> records = new List<MissionRecord>();

        public MissionRecord Find(string map, string difficulty)
        {
            foreach (MissionRecord r in records)
                if (r.map == map && r.difficulty == difficulty) return r;
            return null;
        }

        // 더 좋은 기록이면 바꾸고 true
        public bool Submit(MissionRecord record)
        {
            MissionRecord existing = Find(record.map, record.difficulty);
            if (!RecordRules.IsBetter(record, existing)) return false;
            if (existing != null) records.Remove(existing);
            records.Add(record);
            return true;
        }
    }

    [Serializable]
    public class SettingsData
    {
        public float master = 1f;
        public float sfx = 1f;
        public float bgm = 0.7f;
        public bool fullscreen = true;
        public int width = 1920;
        public int height = 1080;
    }

    public enum SaveLoadStatus { Ok, Missing, RestoredFromBackup, CorruptNeedsConsent }

    // 저장·복구(7.13): 임시 파일에 쓴 뒤 교체, 이전 파일은 .bak. 실패하면 1회 재시도.
    // 손상되면 .corrupt로 보존 → .bak 복원 → 없으면 동의 후 초기화
    public sealed class SaveSystem
    {
        readonly string path;

        public string FilePath { get { return path; } }
        public string LastError { get; private set; }
        // 쓰기 대신 실패를 흉내 내는 횟수(테스트용)
        public int FailNextWrites { get; set; }

        public SaveSystem(string directory)
        {
            path = Path.Combine(directory, "save.json");
        }

        public static SaveSystem Default { get { return new SaveSystem(Application.persistentDataPath); } }

        public SaveData Load(out SaveLoadStatus status)
        {
            status = SaveLoadStatus.Ok;
            if (!File.Exists(path))
            {
                status = SaveLoadStatus.Missing;
                return new SaveData();
            }
            if (TryParse(File.ReadAllText(path), out SaveData data)) return data;

            // 손상: 원본을 .corrupt로 보존하고 .bak에서 복원
            string corrupt = path + ".corrupt";
            File.Copy(path, corrupt, true);
            string bak = path + ".bak";
            if (File.Exists(bak) && TryParse(File.ReadAllText(bak), out SaveData restored))
            {
                File.Copy(bak, path, true);
                status = SaveLoadStatus.RestoredFromBackup;
                Debug.LogWarning("[Save] 저장 파일이 손상되어 백업에서 복원했습니다.");
                return restored;
            }
            status = SaveLoadStatus.CorruptNeedsConsent;
            Debug.LogWarning("[Save] 저장 파일이 손상되었고 백업도 없습니다. 동의를 받은 뒤 초기화합니다.");
            return new SaveData();
        }

        // 손상 상태에서 사용자가 동의하면 새로 만든다
        public bool ResetWithConsent()
        {
            return Save(new SaveData());
        }

        static bool TryParse(string json, out SaveData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{")) return false;
            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) return false;
                if (data.settings == null) data.settings = new SettingsData();
                if (data.records == null) data.records = new List<MissionRecord>();
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        // 실패하면 1회 재시도. 그래도 실패하면 false(기록 없이 진행)
        public bool Save(SaveData data)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (TryWrite(data)) return true;
                Debug.LogWarning("[Save] 저장 실패" + (attempt == 0 ? ", 다시 시도합니다: " : ": ") + LastError);
            }
            return false;
        }

        bool TryWrite(SaveData data)
        {
            try
            {
                if (FailNextWrites > 0)
                {
                    FailNextWrites--;
                    throw new IOException("테스트용 쓰기 실패");
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Copy(path, path + ".bak", true);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                LastError = e.Message;
                return false;
            }
        }
    }
}
