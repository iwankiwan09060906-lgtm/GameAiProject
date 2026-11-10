using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 소리 파일이 없을 때 쓰는 코드 생성 톤(11.2). 같은 ID는 한 번만 만든다
    public static class ProceduralAudio
    {
        const int Rate = 22050;
        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        public static AudioClip Get(string id)
        {
            if (Cache.TryGetValue(id, out AudioClip clip)) return clip;
            clip = Make(id);
            Cache[id] = clip;
            return clip;
        }

        static AudioClip Make(string id)
        {
            // 연출 난수 없이 ID별 고정 시드(같은 소리)
            var rng = new RandomSource(id.GetHashCode());
            switch (id)
            {
                case "sfx_shot_pistol": return Noise(id, 0.1f, 0.9f, 0f, rng);
                case "sfx_shot_silenced": return Noise(id, 0.08f, 0.35f, 0f, rng);
                case "sfx_shot_rifle": return Noise(id, 0.12f, 1f, 70f, rng);
                case "sfx_reload": return Clicks(id, 0.4f, 2);
                case "sfx_empty": return Clicks(id, 0.05f, 1);
                case "sfx_casing": return Tone(id, 0.1f, 3200f, 3000f, 0.25f);
                case "sfx_footstep": return Noise(id, 0.08f, 0.3f, 90f, rng);
                case "sfx_door": return Tone(id, 0.2f, 140f, 90f, 0.5f);
                case "sfx_hit_player": return Tone(id, 0.15f, 520f, 180f, 0.6f);
                case "sfx_hit_armor": return Tone(id, 0.12f, 1400f, 1200f, 0.4f);
                case "sfx_hit_enemy": return Tone(id, 0.1f, 300f, 200f, 0.5f);
                case "sfx_takedown": return Noise(id, 0.2f, 0.6f, 60f, rng);
                case "sfx_alert": return Beeps(id, 1f, 880f, 2);
                case "sfx_bark": return Mumble(id, 0.15f, rng);
                case "sfx_decoy": return Clicks(id, 0.3f, 3);
                case "sfx_pickup": return Tone(id, 0.3f, 440f, 880f, 0.5f);
                case "sfx_nvg_on": return Tone(id, 0.2f, 600f, 1400f, 0.4f);
                case "sfx_nvg_off": return Tone(id, 0.2f, 1400f, 600f, 0.4f);
                case "sfx_dialogue_next": return Clicks(id, 0.05f, 1);
                case "sfx_success": return Arpeggio(id, 1f, new[] { 523f, 659f, 784f, 1046f });
                case "sfx_fail": return Arpeggio(id, 1f, new[] { 392f, 330f, 262f, 196f });
                case "sfx_ui_click": return Clicks(id, 0.05f, 1);
                case "sfx_can": return Can(id, rng);
                case "sfx_scream": return ScreamTone(id, rng);
                case "sfx_switch": return Clicks(id, 0.05f, 1);
                case "sfx_smoke": return SmokeBurst(id, rng);
                case "amb_wind_gust": return Wind(id, 4f, rng);
                case "amb_outdoor_night": return Crickets(id, 11f, rng);
                case "bgm_menu": return Pad(id, 20f, 55f);
                case "bgm_game": return Pad(id, 30f, 49f);
                default: return Tone(id, 0.1f, 440f, 440f, 0.3f);
            }
        }

        static AudioClip Build(string id, float seconds, System.Func<int, float, float> sample)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds * Rate));
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sample(i, (float)i / Rate), -1f, 1f);
            AudioClip clip = AudioClip.Create(id, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Envelope(float t, float seconds)
        {
            float attack = Mathf.Min(0.005f, seconds * 0.1f);
            return t < attack ? t / attack : Mathf.Clamp01(1f - (t - attack) / (seconds - attack));
        }

        static AudioClip Noise(string id, float s, float gain, float lowHz, RandomSource rng)
        {
            return Build(id, s, (i, t) => Envelope(t, s) * gain * ((rng.Value() * 2f - 1f) * 0.7f + (lowHz > 0f ? Mathf.Sin(2f * Mathf.PI * lowHz * t) * 0.5f : 0f)));
        }

        static AudioClip Tone(string id, float s, float from, float to, float gain)
        {
            float phase = 0f;
            return Build(id, s, (i, t) =>
            {
                phase += 2f * Mathf.PI * Mathf.Lerp(from, to, t / s) / Rate;
                return Mathf.Sin(phase) * gain * Envelope(t, s);
            });
        }

        static AudioClip Clicks(string id, float s, int count)
        {
            return Build(id, s, (i, t) =>
            {
                float slot = s / count;
                float local = t % slot;
                return local < 0.008f ? (1f - local / 0.008f) * (i % 2 == 0 ? 0.6f : -0.6f) : 0f;
            });
        }

        static AudioClip Beeps(string id, float s, float hz, int count)
        {
            return Build(id, s, (i, t) =>
            {
                float slot = s / count;
                return (t % slot) < slot * 0.4f ? Mathf.Sin(2f * Mathf.PI * hz * t) * 0.4f : 0f;
            });
        }

        static AudioClip Mumble(string id, float s, RandomSource rng)
        {
            return Build(id, s, (i, t) => Envelope(t, s) * (Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.3f + (rng.Value() - 0.5f) * 0.2f));
        }

        static AudioClip Arpeggio(string id, float s, float[] notes)
        {
            return Build(id, s, (i, t) =>
            {
                int k = Mathf.Min(notes.Length - 1, (int)(t / (s / notes.Length)));
                return Mathf.Sin(2f * Mathf.PI * notes[k] * t) * 0.3f * Envelope(t, s);
            });
        }

        static AudioClip Can(string id, RandomSource rng)
        {
            const float s = 0.25f;
            return Build(id, s, (i, t) => Envelope(t, s) * (Mathf.Sin(2f * Mathf.PI * 2600f * t) * Mathf.Exp(-t * 40f) * 0.6f + (rng.Value() - 0.5f) * 0.25f * Mathf.Exp(-t * 8f)));
        }

        static AudioClip ScreamTone(string id, RandomSource rng)
        {
            const float s = 0.3f;
            float phase = 0f;
            return Build(id, s, (i, t) =>
            {
                phase += 2f * Mathf.PI * Mathf.Lerp(700f, 300f, t / s) / Rate;
                return Envelope(t, s) * (Mathf.Sin(phase) * 0.4f + (rng.Value() - 0.5f) * 0.2f);
            });
        }

        // 연막탄: 짧은 '펑' + 2초 동안 줄어드는 '쉬익'
        static AudioClip SmokeBurst(string id, RandomSource rng)
        {
            const float s = 2.2f;
            float low = 0f;
            return Build(id, s, (i, t) =>
            {
                float pop = t < 0.08f ? Mathf.Sin(2f * Mathf.PI * 90f * t) * (1f - t / 0.08f) * 0.8f : 0f;
                float hiss = (rng.Value() * 2f - 1f);
                low = low * 0.6f + hiss * 0.4f;
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((s - t) / 1.6f);
                return pop + (hiss - low) * 0.35f * env;
            });
        }

        static AudioClip Wind(string id, float s, RandomSource rng)
        {
            float last = 0f;
            return Build(id, s, (i, t) =>
            {
                last = last * 0.985f + (rng.Value() - 0.5f) * 0.05f;
                float fade = Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((s - t) / 0.5f);
                return last * 6f * fade;
            });
        }

        static AudioClip Crickets(string id, float s, RandomSource rng)
        {
            float last = 0f;
            return Build(id, s, (i, t) =>
            {
                last = (rng.Value() - 0.5f) * 0.04f - last * 0.5f;
                float chirp = (t * 6f % 1f) < 0.08f ? Mathf.Sin(2f * Mathf.PI * 4200f * t) * 0.08f : 0f;
                return last + chirp;
            });
        }

        static AudioClip Pad(string id, float s, float root)
        {
            return Build(id, s, (i, t) =>
            {
                float lfo = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / s);
                return (Mathf.Sin(2f * Mathf.PI * root * t) + Mathf.Sin(2f * Mathf.PI * root * 1.5f * t) * 0.5f) * 0.08f * (0.6f + 0.4f * lfo);
            });
        }
    }
}
