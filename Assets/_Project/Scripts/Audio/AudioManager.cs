using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 소리 재생(11.2). 외부 파일이 없으면 코드 생성 톤. 일시정지하면 모두 멈췄다가 이어서 재생한다.
    // 플레이어에게서 먼 소리는 작게, 25m 밖은 재생하지 않는다
    public sealed class AudioManager : MonoBehaviour
    {
        const float HearRange = 25f;
        const int Voices = 16;

        readonly List<AudioSource> voices = new List<AudioSource>();
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        AssetCatalog catalog;
        AudioSource bgm;
        AudioSource ambience;
        AudioSource wind;
        float fadeSeconds = 0.5f;
        float ambienceTarget;
        float windTarget;
        int next;
        System.Func<Vector2> listener = () => Vector2.zero;

        public static AudioManager Create(Transform parent, AssetCatalog catalog, float ambienceFade)
        {
            var go = new GameObject("AudioManager");
            go.transform.SetParent(parent, false);
            var manager = go.AddComponent<AudioManager>();
            manager.catalog = catalog;
            manager.fadeSeconds = Mathf.Max(0.01f, ambienceFade);
            for (int i = 0; i < Voices; i++) manager.voices.Add(go.AddComponent<AudioSource>());
            manager.bgm = go.AddComponent<AudioSource>();
            manager.bgm.loop = true;
            manager.ambience = go.AddComponent<AudioSource>();
            manager.ambience.loop = true;
            manager.wind = go.AddComponent<AudioSource>();
            manager.wind.loop = false;
            foreach (AudioSource s in go.GetComponents<AudioSource>()) { s.playOnAwake = false; s.spatialBlend = 0f; }
            manager.ApplyVolumes();
            return manager;
        }

        public void SetListener(System.Func<Vector2> position) { listener = position; }

        AudioClip Clip(string id)
        {
            if (clips.TryGetValue(id, out AudioClip clip)) return clip;
            clip = catalog != null ? catalog.GetClip(id) : null;
            if (clip == null) clip = ProceduralAudio.Get(id);
            clips[id] = clip;
            return clip;
        }

        public void ApplyVolumes()
        {
            SettingsData s = GameSession.Settings;
            AudioListener.volume = Mathf.Clamp01(s.master);
            if (bgm != null) bgm.volume = Mathf.Clamp01(s.bgm);
        }

        public void PlayBgm(string id)
        {
            AudioClip clip = Clip(id);
            if (bgm.clip == clip && bgm.isPlaying) return;
            bgm.clip = clip;
            bgm.volume = GameSession.Settings.bgm;
            bgm.Play();
            ambience.clip = Clip("amb_outdoor_night");
            ambience.volume = 0f;
            ambience.Play();
            wind.clip = Clip("amb_wind_gust");
        }

        // 2D 효과음. 위치를 주면 플레이어와의 거리로 줄인다
        public void Play(string id, Vector2? position = null, float gain = 1f)
        {
            float volume = gain * GameSession.Settings.sfx;
            if (position.HasValue)
            {
                float d = Vector2.Distance(position.Value, listener());
                if (d > HearRange) return;
                volume *= 1f - d / HearRange * 0.8f;
            }
            if (volume <= 0.001f) return;
            AudioSource source = voices[next];
            next = (next + 1) % voices.Count;
            source.PlayOneShot(Clip(id), volume);
        }

        // 야외 환경음·돌풍 목표 음량(0~1). 배경음 음량을 따른다
        public void SetAmbience(bool outdoor, bool gust)
        {
            ambienceTarget = outdoor ? 1f : 0f;
            windTarget = outdoor && gust ? 1f : 0f;
            if (windTarget > 0f && !wind.isPlaying) wind.Play();
        }

        void OnEnable()
        {
            EventBus.Subscribe<PauseEvent>(OnPause);
            EventBus.Subscribe<ShotFiredEvent>(OnShot);
            EventBus.Subscribe<WorldEvent>(OnWorldEvent);
            EventBus.Subscribe<DoorChangedEvent>(OnDoor);
            EventBus.Subscribe<DamageEvent>(OnDamage);
            EventBus.Subscribe<HealthChangedEvent>(OnHealth);
            EventBus.Subscribe<DetectionEvent>(OnDetection);
            EventBus.Subscribe<BarkEvent>(OnBark);
            EventBus.Subscribe<EnemyActionEvent>(OnEnemyAction);
            EventBus.Subscribe<MissionStateEvent>(OnMission);
            EventBus.Subscribe<LightChangedEvent>(OnLight);
            EventBus.Subscribe<UiClickEvent>(OnClick);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<PauseEvent>(OnPause);
            EventBus.Unsubscribe<ShotFiredEvent>(OnShot);
            EventBus.Unsubscribe<WorldEvent>(OnWorldEvent);
            EventBus.Unsubscribe<DoorChangedEvent>(OnDoor);
            EventBus.Unsubscribe<DamageEvent>(OnDamage);
            EventBus.Unsubscribe<HealthChangedEvent>(OnHealth);
            EventBus.Unsubscribe<DetectionEvent>(OnDetection);
            EventBus.Unsubscribe<BarkEvent>(OnBark);
            EventBus.Unsubscribe<EnemyActionEvent>(OnEnemyAction);
            EventBus.Unsubscribe<MissionStateEvent>(OnMission);
            EventBus.Unsubscribe<LightChangedEvent>(OnLight);
            EventBus.Unsubscribe<UiClickEvent>(OnClick);
            AudioListener.pause = false;
        }

        void OnPause(PauseEvent e) { AudioListener.pause = e.isPaused; }

        void OnShot(ShotFiredEvent e)
        {
            string id = e.weaponId == "silenced_pistol" ? "sfx_shot_silenced" : e.weaponId == "rifle" || e.weaponId == "rifleman" ? "sfx_shot_rifle" : "sfx_shot_pistol";
            Play(id, e.shooterId == EntityIds.Player ? (Vector2?)null : e.muzzlePos);
            Play("sfx_casing", e.muzzlePos, 0.5f);
        }

        void OnWorldEvent(WorldEvent e)
        {
            switch (e.code)
            {
                case "SND_FOOTSTEP": Play("sfx_footstep", null, 0.5f); break;
                case "SND_RELOAD": Play("sfx_reload"); break;
                case "SND_CAN": Play("sfx_can", e.position); break;
                case "SND_SCREAM": Play("sfx_scream", e.position, 0.8f); break;
                case "SND_DECOY": Play("sfx_smoke", e.position); break;
            }
        }

        void OnDoor(DoorChangedEvent e) { Play("sfx_door", e.actorId == EntityIds.Player ? (Vector2?)null : listener(), 0.8f); }

        void OnDamage(DamageEvent e)
        {
            if (e.kind == DamageKind.Melee) Play("sfx_takedown");
            else if (e.targetId != EntityIds.Player) Play("sfx_hit_enemy", e.hitPoint);
        }

        int lastArmor = int.MaxValue;
        int lastHp = int.MaxValue;

        void OnHealth(HealthChangedEvent e)
        {
            if (e.entityId != EntityIds.Player) return;
            if (e.armor < lastArmor) Play("sfx_hit_armor");
            else if (e.hp < lastHp) Play("sfx_hit_player");
            lastArmor = e.armor;
            lastHp = e.hp;
        }

        void OnDetection(DetectionEvent e) { Play("sfx_alert", null, 0.7f); }

        void OnBark(BarkEvent e) { Play("sfx_bark", null, 0.6f); }

        void OnEnemyAction(EnemyActionEvent e) { }

        void OnMission(MissionStateEvent e)
        {
            if (e.state == "objective") Play("sfx_pickup");
            if (e.state == MissionManager.Debrief) Play(e.result == "success" ? "sfx_success" : "sfx_fail");
        }

        void OnLight(LightChangedEvent e) { Play("sfx_switch"); }

        void OnClick(UiClickEvent e) { Play("sfx_ui_click"); }

        // 환경음 페이드(일시정지 중에도 진행)
        public void Tick()
        {
            float step = Time.unscaledDeltaTime / fadeSeconds;
            float bgmVolume = GameSession.Settings.bgm;
            if (ambience != null) ambience.volume = Mathf.MoveTowards(ambience.volume, ambienceTarget * bgmVolume, step * bgmVolume);
            if (wind != null) wind.volume = Mathf.MoveTowards(wind.volume, windTarget * bgmVolume, step * bgmVolume);
        }
    }
}
