using UnityEngine;

namespace GameAi
{
    // 실행 동안 유지되는 사용자 저장(설정·최고 기록). 처음 쓸 때 한 번 읽는다
    public static class GameSession
    {
        static SaveData data;
        static SaveSystem system;

        public static SaveLoadStatus LoadStatus { get; private set; }
        public static bool LastSaveFailed { get; private set; }

        public static SaveData Data
        {
            get
            {
                if (data == null)
                {
                    system = SaveSystem.Default;
                    data = system.Load(out SaveLoadStatus status);
                    LoadStatus = status;
                }
                return data;
            }
        }

        public static SettingsData Settings { get { return Data.settings; } }

        public static bool Save()
        {
            SaveData d = Data;
            LastSaveFailed = !system.Save(d);
            return !LastSaveFailed;
        }

        // 손상된 저장을 사용자가 초기화에 동의했다
        public static void ResetCorrupt()
        {
            SaveData d = Data;
            if (LoadStatus != SaveLoadStatus.CorruptNeedsConsent) return;
            system.ResetWithConsent();
            data = new SaveData();
            LoadStatus = SaveLoadStatus.Ok;
        }

        static bool screenApplied;

        // 실행 후 처음 한 번 저장된 화면 설정을 적용한다(-smoke 실행은 명령줄 해상도를 따른다)
        public static void ApplyScreenOnce()
        {
            if (screenApplied) return;
            screenApplied = true;
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-smoke") >= 0 || Application.isEditor) return;
            ApplyScreen();
        }

        // 화면 설정 적용(해상도는 1280×720 / 1920×1080 프리셋)
        public static void ApplyScreen()
        {
            SettingsData s = Settings;
            Screen.SetResolution(s.width, s.height, s.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }
    }
}
