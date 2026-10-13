using System;
using UnityEngine;

namespace IslandTycoon
{
    public enum Sfx { Click, Coin, Fail, WaveStart, WaveDone, Upgrade, Unlock, Whoosh }

    /// <summary>
    /// Sound effects and a relaxed ukulele-style music loop, synthesised at start-up,
    /// so the game needs no audio files. Respects the Music / Sound settings.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        const int Rate = 22050;
        AudioSource _music, _sfx;
        AudioClip[] _clips;
        float _lastCoin;

        public static AudioManager Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.volume = 0.32f;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.volume = 0.7f;

            _clips = new AudioClip[Enum.GetValues(typeof(Sfx)).Length];
            _clips[(int)Sfx.Click] = Make("click", 0.06f, (t, i) => Tone(t, 1400, 0) * Env(t, 0.003f, 0.05f) * 0.5f);
            _clips[(int)Sfx.Coin] = Make("coin", 0.28f, (t, i) => (t < 0.07f ? Tone(t, 1320, 1) : Tone(t, 1760, 1)) * Env(t, 0.002f, 0.26f) * 0.35f);
            _clips[(int)Sfx.Fail] = Make("fail", 0.45f, (t, i) => Tone(t, Mathf.Lerp(260, 150, t / 0.45f), 2) * Env(t, 0.01f, 0.43f) * 0.3f);
            _clips[(int)Sfx.WaveStart] = Make("start", 0.7f, (t, i) => (Pluck(t, 523) + Pluck(t - 0.12f, 659) + Pluck(t - 0.24f, 784)) * 0.35f);
            _clips[(int)Sfx.WaveDone] = Make("done", 1.2f, (t, i) =>
                (Pluck(t, 523) + Pluck(t - 0.15f, 659) + Pluck(t - 0.3f, 784) + Pluck(t - 0.45f, 1047) * 1.2f) * 0.32f);
            _clips[(int)Sfx.Upgrade] = Make("upgrade", 0.5f, (t, i) => Tone(t, Mathf.Lerp(500, 1300, t / 0.5f), 1) * Env(t, 0.01f, 0.48f) * 0.3f);
            _clips[(int)Sfx.Unlock] = Make("unlock", 1.4f, (t, i) =>
                (Pluck(t, 392) + Pluck(t - 0.12f, 523) + Pluck(t - 0.24f, 659) + Pluck(t - 0.42f, 784) + Pluck(t - 0.42f, 1047)) * 0.28f);
            _clips[(int)Sfx.Whoosh] = Make("whoosh", 0.35f, (t, i) => Noise(i) * Env(t, 0.12f, 0.22f) * 0.18f);

            _music.clip = MakeMusic();
        }

        public void Apply(SettingsState s)
        {
            if (_music == null) return;
            if (s.music && !_music.isPlaying) _music.Play();
            if (!s.music && _music.isPlaying) _music.Stop();
            _sfx.mute = !s.sound;
        }

        public void Play(Sfx s)
        {
            if (_sfx == null || _sfx.mute) return;
            if (s == Sfx.Coin)
            {
                if (Time.unscaledTime - _lastCoin < 0.06f) return;
                _lastCoin = Time.unscaledTime;
            }
            _sfx.PlayOneShot(_clips[(int)s]);
        }

        // ------------------------------------------------------------------ synthesis

        static AudioClip Make(string name, float seconds, Func<float, int, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, i), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Tone(float t, float hz, int shape)
        {
            float ph = t * hz;
            float s = Mathf.Sin(ph * 2f * Mathf.PI);
            if (shape == 1) s = s * 0.7f + Mathf.Sin(ph * 4f * Mathf.PI) * 0.3f;
            if (shape == 2) s = (ph - Mathf.Floor(ph)) * 2f - 1f;
            return s;
        }

        static float Env(float t, float attack, float release)
        {
            if (t < 0) return 0;
            if (t < attack) return t / attack;
            return Mathf.Max(0, 1f - (t - attack) / release);
        }

        static uint _seed = 1234567;
        static float Noise(int i)
        {
            _seed = _seed * 1664525u + 1013904223u;
            return ((_seed >> 9) / 8388608f) * 2f - 1f;
        }

        /// <summary>Soft plucked string: sine with harmonics and exponential decay.</summary>
        static float Pluck(float t, float hz)
        {
            if (t < 0) return 0;
            float d = Mathf.Exp(-t * 5.5f);
            float s = Mathf.Sin(t * hz * 2f * Mathf.PI) + 0.45f * Mathf.Sin(t * hz * 4f * Mathf.PI) * Mathf.Exp(-t * 4f)
                      + 0.2f * Mathf.Sin(t * hz * 6f * Mathf.PI) * Mathf.Exp(-t * 8f);
            return s * d * Mathf.Min(1f, t * 400f);
        }

        /// <summary>16 bars of C - Am - F - G strumming with a bass line, 96 BPM.</summary>
        static AudioClip MakeMusic()
        {
            float beat = 60f / 96f;
            int bars = 8;
            float len = bars * 4 * beat;
            int n = Mathf.CeilToInt(len * Rate);
            var data = new float[n];
            float[][] chords =
            {
                new[] { 261.6f, 329.6f, 392.0f, 523.3f },
                new[] { 220.0f, 261.6f, 329.6f, 440.0f },
                new[] { 174.6f, 220.0f, 261.6f, 349.2f },
                new[] { 196.0f, 246.9f, 293.7f, 392.0f },
            };
            float[] bass = { 65.4f, 55f, 43.7f, 49f };
            // island strum pattern: down, down-up, up-down-up (eighths)
            int[] pattern = { 1, 0, 1, 1, 0, 1, 1, 1 };
            int[] melody = { 4, -1, 2, -1, 3, 2, 1, -1 };

            for (int bar = 0; bar < bars; bar++)
            {
                int ci = bar % 4;
                var ch = chords[ci];
                float barStart = bar * 4 * beat;
                for (int e = 0; e < 8; e++)
                {
                    if (pattern[e] == 0) continue;
                    float start = barStart + e * beat * 0.5f;
                    bool up = e % 2 == 1;
                    for (int k = 0; k < ch.Length; k++)
                    {
                        float st = start + (up ? (ch.Length - 1 - k) : k) * 0.012f;
                        AddNote(data, st, ch[k], up ? 0.06f : 0.09f, 1.1f);
                    }
                }
                AddNote(data, barStart, bass[ci], 0.32f, 1.6f);
                AddNote(data, barStart + 2 * beat, bass[ci] * 1.5f, 0.22f, 1.2f);

                // light marimba-like melody on every other bar
                if (bar % 2 == 1)
                    for (int e = 0; e < 8; e++)
                    {
                        if (melody[e] < 0) continue;
                        float hz = ch[Mathf.Min(melody[e], ch.Length - 1) % ch.Length] * 2f;
                        AddNote(data, barStart + e * beat * 0.5f, hz, 0.07f, 0.5f);
                    }
            }

            float peak = 0.001f;
            foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;

            var clip = AudioClip.Create("music", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void AddNote(float[] data, float start, float hz, float vol, float dur)
        {
            int s0 = Mathf.Max(0, Mathf.RoundToInt(start * Rate));
            int s1 = Mathf.Min(data.Length, s0 + Mathf.RoundToInt(dur * Rate));
            for (int i = s0; i < s1; i++)
            {
                float t = (i - s0) / (float)Rate;
                data[i] += Pluck(t, hz) * vol;
            }
        }
    }
}
