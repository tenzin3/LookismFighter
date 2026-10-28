using UnityEngine;

namespace LookismFighter
{
    /// <summary>
    /// Sound effects and background music, all generated in code (no audio files needed).
    /// To use your own music, put an audio file named "music" in Assets/Resources/Audio/ — it replaces the generated beat.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Range(0f, 1f)] public float musicVolume = 0.35f;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;

        const int Rate = 22050;
        AudioSource music, sfx;
        AudioClip whoosh, whooshHeavy, hitLight, hitHeavy, block, ko, bell, thud;

        void Awake()
        {
            Instance = this;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.volume = musicVolume;

            whoosh = Make("whoosh", 0.16f, Whoosh(900f, 2600f, 0.5f));
            whooshHeavy = Make("whooshHeavy", 0.24f, Whoosh(500f, 1800f, 0.7f));
            hitLight = Make("hitLight", 0.18f, Hit(0.6f));
            hitHeavy = Make("hitHeavy", 0.32f, Hit(1f));
            block = Make("block", 0.09f, Block);
            ko = Make("ko", 1.4f, Ko);
            bell = Make("bell", 1.2f, Bell);
            thud = Make("thud", 0.35f, Thud);

            var custom = Resources.Load<AudioClip>("Audio/music");
            music.clip = custom != null ? custom : MakeMusic();
            music.Play();
        }

        void Update() { music.volume = musicVolume; music.pitch = Time.timeScale < 0.99f && Time.timeScale > 0.01f ? 0.8f : 1f; }

        // ---------------------------------------------------------------- public API
        public void Swing(bool heavy) => Play(heavy ? whooshHeavy : whoosh, 0.6f, Random.Range(0.92f, 1.08f));
        public void Hit(bool heavy) => Play(heavy ? hitHeavy : hitLight, 1f, Random.Range(0.94f, 1.06f));
        public void Blocked() => Play(block, 0.8f, Random.Range(0.95f, 1.1f));
        public void KO() => Play(ko, 1f, 1f);
        public void RoundBell() => Play(bell, 0.6f, 1f);
        public void Fall() => Play(thud, 0.9f, 1f);

        void Play(AudioClip c, float vol, float pitch)
        {
            if (c == null) return;
            sfx.pitch = pitch;
            sfx.PlayOneShot(c, vol * sfxVolume);
        }

        // ---------------------------------------------------------------- synthesis helpers
        delegate float Synth(float t, float dur, System.Random rng);

        static AudioClip Make(string name, float seconds, Synth f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, seconds, rng), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        // simple one-pole low-pass state shared per synth call
        static float lp;

        static Synth Whoosh(float f0, float f1, float amp) => (t, d, r) =>
        {
            float k = t / d;
            float cutoff = Mathf.Lerp(f0, f1, Mathf.Sin(k * Mathf.PI));
            float a = Mathf.Clamp01(cutoff / Rate * 6.28f);
            lp += a * (Noise(r) - lp);
            return lp * amp * Mathf.Sin(k * Mathf.PI) * 2.2f;
        };

        static Synth Hit(float power) => (t, d, r) =>
        {
            float env = Mathf.Exp(-t * (18f - power * 6f));
            float thump = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(150f, 45f, t / d) * t) * Mathf.Exp(-t * 9f);
            float crack = Noise(r) * Mathf.Exp(-t * 45f);
            return (thump * 0.9f + crack * 0.6f * power + Noise(r) * env * 0.25f) * (0.7f + 0.3f * power);
        };

        static float Block(float t, float d, System.Random r)
        {
            float env = Mathf.Exp(-t * 55f);
            return (Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 1400f * t)) * 0.25f + Noise(r) * 0.5f) * env;
        }

        static float Ko(float t, float d, System.Random r)
        {
            float boom = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 28f, t / d) * t) * Mathf.Exp(-t * 2.2f);
            return boom * 0.95f + Noise(r) * Mathf.Exp(-t * 6f) * 0.35f;
        }

        static float Bell(float t, float d, System.Random r)
        {
            float env = Mathf.Exp(-t * 3.5f);
            return (Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 1987f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 3310f * t) * 0.1f) * env;
        }

        static float Thud(float t, float d, System.Random r)
        {
            return Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(80f, 35f, t / d) * t) * Mathf.Exp(-t * 10f) + Noise(r) * Mathf.Exp(-t * 25f) * 0.3f;
        }

        // ---------------------------------------------------------------- generated fight music
        /// <summary>16-bar loop at 132 BPM: kick, snare, hats, a minor bass line and a synth riff.</summary>
        static AudioClip MakeMusic()
        {
            const float bpm = 132f;
            float beat = 60f / bpm, sixteenth = beat / 4f;
            int bars = 16, steps = bars * 16;
            float seconds = steps * sixteenth;
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var rng = new System.Random(7);

            // A minor - F - C - G, two bars each, repeated
            float[] roots = { 55f, 55f, 43.65f, 43.65f, 65.41f, 65.41f, 49f, 49f };
            int[] riff = { 0, 12, 7, 12, 3, 12, 7, 10, 0, 12, 7, 12, 15, 12, 10, 7 }; // semitones above root (x2 octave)

            for (int s = 0; s < steps; s++)
            {
                int start = Mathf.FloorToInt(s * sixteenth * Rate);
                int bar = s / 16, step = s % 16;
                float root = roots[bar % roots.Length];

                // kick on every beat, extra kick on the "and" of 3
                if (step % 4 == 0 || step == 10) AddVoice(data, start, 0.28f, (t) => Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(120f, 40f, Mathf.Clamp01(t / 0.12f)) * t) * Mathf.Exp(-t * 16f) * 0.9f);
                // snare on 2 and 4
                if (step == 4 || step == 12) AddVoice(data, start, 0.2f, (t) => ((float)(rng.NextDouble() * 2 - 1) * 0.55f + Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.3f) * Mathf.Exp(-t * 18f));
                // closed hats on 8ths, open-ish on offbeats
                if (step % 2 == 0) AddVoice(data, start, 0.05f, (t) => (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * (step % 4 == 2 ? 40f : 90f)) * 0.18f);
                // bass: root on 8ths with octave jumps
                if (step % 2 == 0)
                {
                    float f = root * (step % 8 == 6 ? 2f : 1f);
                    AddVoice(data, start, sixteenth * 1.8f, (t) => Saw(f * t) * 0.32f * Mathf.Exp(-t * 6f));
                }
                // synth riff in the second half of the loop
                if (bar >= 8)
                {
                    float f = root * 4f * Mathf.Pow(2f, riff[step] / 12f);
                    AddVoice(data, start, sixteenth * 0.9f, (t) => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t)) * 0.07f * Mathf.Exp(-t * 10f));
                }
            }

            // soft limiter
            for (int i = 0; i < n; i++) data[i] = (float)System.Math.Tanh(data[i] * 1.2f) * 0.85f;
            var clip = AudioClip.Create("GeneratedFightMusic", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase + 0.5f));

        static void AddVoice(float[] data, int start, float seconds, System.Func<float, float> f)
        {
            int len = Mathf.CeilToInt(seconds * Rate);
            for (int i = 0; i < len && start + i < data.Length; i++) data[start + i] += f(i / (float)Rate);
        }
    }
}
