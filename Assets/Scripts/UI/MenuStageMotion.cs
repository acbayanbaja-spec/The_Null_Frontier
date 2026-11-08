using UnityEngine;

namespace NullFrontier.UI
{
    /// <summary>
    /// The motion the camera dolly, rain and traffic do not cover: the gate tilts on its
    /// mount, every emissive panel in the stage breathes and drifts around the hue wheel,
    /// and the glows pulse each on their own phase. One LateUpdate drives the whole avenue
    /// - a component per sign would mean fifty scripted behaviours on the menu's first
    /// frame, and phases come from world position so nothing beats in lockstep.
    /// </summary>
    public class MenuStageMotion : MonoBehaviour
    {
        [SerializeField] private Transform gateRing;
        [SerializeField] private Vector2 ringTiltDegrees = new Vector2(4f, 7f);
        [SerializeField] private float ringSeconds = 21f;

        [SerializeField] private Renderer[] emitters;
        [SerializeField] private float glowAmplitude = 0.32f;
        [SerializeField] private float glowSeconds = 4.2f;
        [SerializeField] private float hueDriftDegrees = 18f;
        [SerializeField] private float hueSeconds = 19f;

        [SerializeField] private SpriteRenderer[] glows;
        [SerializeField] private float alphaAmplitude = 0.34f;
        [SerializeField] private float alphaSeconds = 5.5f;

        [Header("Planet")]
        [SerializeField] private Transform earth;
        [SerializeField] private Transform clouds;
        [SerializeField] private Transform cirrus;
        [SerializeField] private float earthDegreesPerSecond = 2.6f;
        [SerializeField] private float cloudDegreesPerSecond = 3.8f;
        [SerializeField] private float cirrusDegreesPerSecond = 5.1f;

        [Header("Portal")]
        [SerializeField] private Transform[] portalArcs;
        [SerializeField] private float arcDegreesPerSecond = 9f;
        [SerializeField] private Light[] gateLights;
        [SerializeField] private float gateLightPulse = 0.16f;
        [SerializeField] private float gateLightSeconds = 3.1f;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseId = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock _block;
        private Color[] _emissive;
        private Color[] _glowBase;
        private float[] _phase;
        private float[] _arcStart;
        private float[] _gateLightBase;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            BuildEmitters();
            BuildGlows();
            BuildArcs();
            BuildGateLightBases();
        }

        // The authored intensities are cached once: writing the pulsing value back into
        // light.intensity every frame would compound the pulse onto itself.
        private void BuildGateLightBases()
        {
            if (gateLights == null) return;

            _gateLightBase = new float[gateLights.Length];
            for (int i = 0; i < gateLights.Length; i++)
            {
                if (gateLights[i] != null) _gateLightBase[i] = gateLights[i].intensity;
            }
        }

        // The authoring pass gives each arc its own start angle. Writing localRotation from
        // zero every frame would throw that away and stack all three on one seam at t = 0.
        private void BuildArcs()
        {
            if (portalArcs == null) return;

            _arcStart = new float[portalArcs.Length];
            for (int i = 0; i < portalArcs.Length; i++)
            {
                if (portalArcs[i] != null)
                    _arcStart[i] = portalArcs[i].localEulerAngles.z;
            }
        }

        private void BuildEmitters()
        {
            if (emitters == null) return;

            _emissive = new Color[emitters.Length];
            _phase = new float[emitters.Length];
            for (int i = 0; i < emitters.Length; i++)
            {
                var renderer = emitters[i];
                if (renderer == null) continue;

                // Staggered by index as well as position: the three portal arcs share one
                // centre, and a position-only phase would pulse them in lockstep.
                _phase[i] = PhaseOf(renderer.transform.position) + i * 1.31f;
                var shared = renderer.sharedMaterial;
                // The menu's glowing strips are Unlit, which carries the same colour in
                // _BaseColor; Lit emitters elsewhere still pulse through _EmissionColor.
                bool lit = shared != null && shared.HasProperty(EmissionId);
                bool unlit = shared != null && shared.HasProperty(BaseId);
                if (!lit && !unlit)
                {
                    // A renderer with no colour property cannot be pulsed, and leaving it
                    // in the list would write a black colour over its own look.
                    _emissive[i] = new Color(0f, 0f, 0f, 0f);
                    continue;
                }

                Color baseEmission = shared.GetColor(lit ? EmissionId : BaseId);
                baseEmission.a = 1f;
                _emissive[i] = baseEmission;
            }
        }

        // The authored colour is cached once: writing back onto renderer.color every
        // frame would compound the pulse onto itself and fade the glow out within seconds.
        private void BuildGlows()
        {
            if (glows == null) return;

            _glowBase = new Color[glows.Length];
            for (int i = 0; i < glows.Length; i++)
            {
                if (glows[i] != null) _glowBase[i] = glows[i].color;
            }
        }

        private void LateUpdate()
        {
            float t = Time.unscaledTime;
            DriveRing(t);
            DriveEmitters(t);
            DriveGlows(t);
            DrivePlanet(t);
            DriveArcs(t);
            DriveGateLights(t);
        }

        // The rim is a torus, so spinning it about its own axis would move nothing a viewer
        // can see. Tilting it is what reads as a gate that is alive and settling on its mount.
        private void DriveRing(float t)
        {
            if (gateRing == null) return;

            float w = 2f * Mathf.PI * (t / ringSeconds);
            gateRing.localRotation = Quaternion.Euler(
                ringTiltDegrees.x * Mathf.Sin(w),
                ringTiltDegrees.y * Mathf.Sin(w * 0.7f + 1.1f),
                0.4f * Mathf.Sin(w * 1.3f));
        }

        private void DriveEmitters(float t)
        {
            if (_emissive == null) return;

            // A script recompile during Play reloads the domain and drops non-serialized
            // fields; without this the first pulse after the reload dereferences null.
            if (_block == null) _block = new MaterialPropertyBlock();

            for (int i = 0; i < emitters.Length; i++)
            {
                var renderer = emitters[i];
                if (renderer == null || _emissive[i].a <= 0f) continue;

                Color baseColor = _emissive[i];
                Color.RGBToHSV(baseColor, out float h, out float s, out float v);
                h = Mathf.Repeat(h + (hueDriftDegrees / 360f)
                    * Mathf.Sin(2f * Mathf.PI * (t / hueSeconds) + _phase[i]), 1f);

                float breath = 1f + glowAmplitude
                    * Mathf.Sin(2f * Mathf.PI * (t / glowSeconds) + _phase[i] * 1.7f);
                Color shifted = Color.HSVToRGB(h, Mathf.Min(1f, s * 1.12f), v);

                _block.Clear();
                // Written to both slots: Unlit reads the base colour, a Lit emitter reads
                // emission, and the unused one is ignored by its shader.
                _block.SetColor(BaseId, shifted * breath);
                _block.SetColor(EmissionId, shifted * breath);
                renderer.SetPropertyBlock(_block);
            }
        }

        private void DriveGlows(float t)
        {
            if (_glowBase == null) return;

            for (int i = 0; i < glows.Length; i++)
            {
                var glow = glows[i];
                if (glow == null) continue;

                float p = PhaseOf(glow.transform.position);
                float wave = Mathf.Sin(2f * Mathf.PI * (t / alphaSeconds) + p * 2.3f);

                Color c = _glowBase[i];
                // Alpha writes are absolute because sprite tint is not a property block -
                // multiplying glow.color in place would compound the pulse onto itself.
                c.a = Mathf.Clamp01(c.a * (1f + alphaAmplitude * wave));
                c.r = Mathf.Min(1f, c.r * (1f + 0.12f * wave));
                c.g = Mathf.Min(1f, c.g * (1f + 0.12f
                    * Mathf.Sin(2f * Mathf.PI * (t / alphaSeconds) + p * 1.1f)));
                glow.color = c;
            }
        }

        // Both shells ride on the tilted pivot, so their local Y is the planet's own axis.
        // The weather runs a little faster than the ground - the only cue at this distance
        // that the clouds are a separate layer rather than paint on the surface.
        private void DrivePlanet(float t)
        {
            if (earth != null)
                earth.localRotation = Quaternion.Euler(0f, t * earthDegreesPerSecond, 0f);
            if (clouds != null)
                clouds.localRotation = Quaternion.Euler(0f, t * cloudDegreesPerSecond, 0f);
            if (cirrus != null)
                cirrus.localRotation = Quaternion.Euler(0f, t * cirrusDegreesPerSecond, 0f);
        }

        // Alternating direction and rising speed means the three arcs cross, separate and
        // never settle into a pattern a viewer can read as a fixed three-ring graphic.
        private void DriveArcs(float t)
        {
            if (portalArcs == null) return;

            for (int i = 0; i < portalArcs.Length; i++)
            {
                var arc = portalArcs[i];
                if (arc == null) continue;

                float direction = (i % 2 == 0) ? 1f : -1f;
                float start = _arcStart != null ? _arcStart[i] : 0f;
                arc.localRotation = Quaternion.Euler(0f, 0f,
                    start + direction * t * arcDegreesPerSecond * (1f + 0.35f * i));
            }
        }

        // Three points breathing out of phase read as a gate whose energy is not constant;
        // in lockstep they read as a light with a broken exposure.
        private void DriveGateLights(float t)
        {
            if (gateLights == null || _gateLightBase == null) return;

            for (int i = 0; i < gateLights.Length; i++)
            {
                var light = gateLights[i];
                if (light == null) continue;

                float phase = i * 2.1f + PhaseOf(light.transform.position);
                light.intensity = _gateLightBase[i] * (1f + gateLightPulse
                    * Mathf.Sin(2f * Mathf.PI * (t / gateLightSeconds) + phase));
            }
        }

        private static float PhaseOf(Vector3 world)
        {
            return world.x * 0.37f + world.y * 0.53f + world.z * 0.21f;
        }
    }
}
