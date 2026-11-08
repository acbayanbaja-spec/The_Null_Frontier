using UnityEngine;

namespace NullFrontier.Core
{
    /// <summary>
    /// All game sound is synthesised at runtime: no audio files are shipped, so
    /// nothing here depends on an external asset. The clips are short procedural
    /// waveforms (noise bursts, swept sines) built once in Awake.
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        [Header("Mix")]
        [SerializeField] private int sampleRate = 22050;
        [SerializeField] private float masterVolume = 0.75f;
        [SerializeField] private float ambientVolume = 0.28f;
        [SerializeField] private float footstepVolume = 0.32f;
        [SerializeField] private float shotVolume = 0.6f;
        [SerializeField] private float maxHearDistance = 42f;
        [SerializeField] private int voices = 6;

        private AudioClip _footstep;
        private AudioClip _shot;
        private AudioClip _hit;
        private AudioClip _ui;
        private AudioClip _ambient;
        private AudioSource[] _pool;
        private AudioSource _pad;
        private int _nextVoice;
        private float _effectsScale = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _footstep = ProceduralWaves.Build("Footstep", sampleRate, 0.10f, ProceduralWaves.WriteFootstep);
            _shot = ProceduralWaves.Build("Shot", sampleRate, 0.16f, ProceduralWaves.WriteShot);
            _hit = ProceduralWaves.Build("Hit", sampleRate, 0.14f, ProceduralWaves.WriteHit);
            _ui = ProceduralWaves.Build("UiClick", sampleRate, 0.06f, ProceduralWaves.WriteUiClick);
            _ambient = ProceduralWaves.BuildLoop("Ambient", sampleRate, 5.0f, ProceduralWaves.WriteAmbientDrone);

            var host = new GameObject("AudioVoices");
            host.transform.SetParent(transform, false);
            _pool = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                var voice = host.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1f;
                voice.rolloffMode = AudioRolloffMode.Linear;
                voice.minDistance = 1f;
                voice.maxDistance = maxHearDistance;
                _pool[i] = voice;
            }

            var pad = gameObject.AddComponent<AudioSource>();
            pad.clip = _ambient;
            pad.loop = true;
            pad.playOnAwake = false;
            pad.spatialBlend = 0f;
            pad.volume = ambientVolume * masterVolume;
            pad.Play();
            _pad = pad;

            var listener = Object.FindFirstObjectByType<AudioListener>();
            if (listener == null)
            {
                var cam = Object.FindFirstObjectByType<Camera>();
                if (cam != null) cam.gameObject.AddComponent<AudioListener>();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Release(_footstep); Release(_shot); Release(_hit);
            Release(_ui); Release(_ambient);
        }

        public void PlayFootstep(Vector3 worldPosition, float speed)
            => PlayAt(_footstep, worldPosition,
                footstepVolume * Mathf.Clamp01(speed / 6f) * masterVolume,
                Random.Range(0.85f, 1.2f));

        public void PlayShot(Vector3 worldPosition)
            => PlayAt(_shot, worldPosition, shotVolume * masterVolume, Random.Range(0.92f, 1.08f));

        public void PlayHit(Vector3 worldPosition)
            => PlayAt(_hit, worldPosition, 0.55f * masterVolume, Random.Range(0.9f, 1.15f));

        public void PlayUiClick()
            => PlayAt(_ui, transform.position, 0.5f * masterVolume, 1f);

        /// <summary>Objective tick - pitched above the click so feedback reads as progress.</summary>
        public void PlayObjectivePing()
            => PlayAt(_ui, transform.position, 0.62f * masterVolume, 1.55f);

        /// <summary>Contract chime: two voices a fifth apart land as one reward sting.</summary>
        public void PlayMissionComplete()
        {
            PlayAt(_ui, transform.position, 0.7f * masterVolume, 1f);
            PlayAt(_ui, transform.position, 0.7f * masterVolume, 1.5f);
        }

        // --------------------------------------------------------------- mix settings

        /// <summary>Overall level, 0..1. Every voice and the ambience pad scale with it.</summary>
        public float MasterLevel => masterVolume;
        public float AmbientLevel => ambientVolume;
        public float EffectsLevel => _effectsScale;

        public void SetMasterVolume(float value)
        {
            masterVolume = Mathf.Clamp01(value);
            if (_pad != null) _pad.volume = ambientVolume * masterVolume;
        }

        /// <summary>City ambience level, 0..1.</summary>
        public void SetAmbientVolume(float value)
        {
            ambientVolume = Mathf.Clamp01(value);
            if (_pad != null) _pad.volume = ambientVolume * masterVolume;
        }

        /// <summary>Footsteps, shots and impacts level, 0..1.</summary>
        public void SetEffectsVolume(float value) => _effectsScale = Mathf.Clamp01(value);

        private void PlayAt(AudioClip clip, Vector3 worldPosition, float volume, float pitch)
        {
            if (clip == null || _pool == null || volume <= 0f) return;
            var voice = _pool[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _pool.Length;
            voice.transform.position = worldPosition;
            voice.pitch = pitch;
            voice.PlayOneShot(clip, volume * _effectsScale);
        }

        private static void Release(AudioClip clip)
        {
            if (clip != null) Destroy(clip);
        }
    }

    /// <summary>
    /// Tiny waveform generator. Every writer fills a normalised, peak-limited
    /// buffer so the mix never clips regardless of how many voices overlap.
    /// </summary>
    internal static class ProceduralWaves
    {
        internal delegate void Writer(float[] samples, int sampleRate);

        public static AudioClip Build(string name, int sampleRate, float seconds, Writer writer)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(sampleRate * seconds));
            var samples = new float[count];
            writer(samples, sampleRate);
            Normalise(samples, 0.9f);

            var clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Same as Build, but the writer must keep the first and last sample
        /// continuous so the AudioSource loop closes without a click.
        /// </summary>
        public static AudioClip BuildLoop(string name, int sampleRate, float seconds, Writer writer)
            => Build(name, sampleRate, seconds, writer);

        public static void WriteFootstep(float[] s, int rate)
        {
            var random = new System.Random(0x50EF);
            float previous = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / s.Length;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                previous = previous * 0.62f + noise * 0.38f;      // one-pole lowpass
                float envelope = Mathf.Exp(-9f * t) * (1f - t);
                s[i] = previous * envelope;
            }
            AddTone(s, rate, 78f, 0.35f, 6f);
        }

        public static void WriteShot(float[] s, int rate)
        {
            var random = new System.Random(0x5707);
            float previous = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / rate;
                float progress = (float)i / s.Length;

                float frequency = Mathf.Lerp(920f, 90f, progress);
                float tone = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-6f * progress);

                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                previous = previous * 0.45f + noise * 0.55f;
                float crack = previous * Mathf.Exp(-26f * progress);

                s[i] = tone * 0.7f + crack * 0.5f;
            }
        }

        public static void WriteHit(float[] s, int rate)
        {
            var random = new System.Random(0x4817);
            float previous = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / rate;
                float progress = (float)i / s.Length;
                float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(190f, 60f, progress) * t)
                             * Mathf.Exp(-8f * progress);
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                previous = previous * 0.75f + noise * 0.25f;
                s[i] = thud * 0.85f + previous * 0.25f * Mathf.Exp(-16f * progress);
            }
        }

        public static void WriteUiClick(float[] s, int rate)
        {
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / rate;
                float progress = (float)i / s.Length;
                s[i] = Mathf.Sin(2f * Mathf.PI * 1180f * t) * Mathf.Exp(-34f * progress)
                     + Mathf.Sin(2f * Mathf.PI * 1760f * t) * Mathf.Exp(-52f * progress) * 0.4f;
            }
        }

        /// <summary>
        /// Low city hum: two detuned sub tones plus a slow filtered wash. All
        /// tones use whole periods across the buffer so the loop closes silently.
        /// </summary>
        public static void WriteAmbientDrone(float[] s, int rate)
        {
            var random = new System.Random(0xA11B);
            var wash = new float[s.Length];
            float previous = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                previous = previous * 0.94f + noise * 0.06f;
                wash[i] = previous;
            }

            for (int i = 0; i < s.Length; i++)
            {
                float phase = (float)i / s.Length;
                float swell = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * phase);
                float sub = Mathf.Sin(2f * Mathf.PI * 2f * phase * 44f) * 0.30f
                          + Mathf.Sin(2f * Mathf.PI * 3f * phase * 44f) * 0.18f
                          + Mathf.Sin(2f * Mathf.PI * phase * 110f) * 0.10f;
                s[i] = sub * swell + wash[i] * 0.35f * (1f - 0.4f * Mathf.Cos(2f * Mathf.PI * phase));
            }
        }

        private static void AddTone(float[] s, int rate, float frequency, float gain, float decay)
        {
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / rate;
                float progress = (float)i / s.Length;
                s[i] += Mathf.Sin(2f * Mathf.PI * frequency * t) * gain * Mathf.Exp(-decay * progress);
            }
        }

        private static void Normalise(float[] s, float targetPeak)
        {
            float peak = 0f;
            for (int i = 0; i < s.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(s[i]));
            if (peak <= 0.0001f) return;
            float scale = targetPeak / peak;
            for (int i = 0; i < s.Length; i++) s[i] *= scale;
        }
    }
}
