using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NullFrontier.Combat
{
    /// <summary>
    /// The shot itself, made visible.
    ///
    /// Damage here is hitscan: a raycast that resolves in the same frame, so with nothing
    /// drawn the player watches an enemy die and sees no reason why. Every round now
    /// leaves a streak from the muzzle to wherever it landed and a spark at the end of it.
    ///
    /// Pooled rather than instantiated because an assault rifle runs at nine rounds a
    /// second, and a phone does not care how tidy the garbage collector is.
    /// </summary>
    public static class CombatEffects
    {
        public static void Shot(Vector3 from, Vector3 to, Color tint, float width = 0.045f)
        {
            if (CombatEffectsView.Instance != null)
                CombatEffectsView.Instance.AddShot(from, to, tint, width);
        }

        public static void Impact(Vector3 point, Vector3 normal, Color tint)
        {
            if (CombatEffectsView.Instance != null)
                CombatEffectsView.Instance.AddImpact(point, normal, tint);
        }

        /// <summary>
        /// What a body does when a round takes a piece of it: fluid leaves along the
        /// direction the shot was travelling. Severity is the fraction of the target's
        /// health this hit removed, so a pistol spatter and a shotgun blast differ.
        /// </summary>
        public static void Wound(Vector3 point, Vector3 away, float severity, Color tint)
        {
            if (CombatEffectsView.Instance != null)
                CombatEffectsView.Instance.AddWound(point, away, severity, tint);
        }

        /// <summary>The last hit. Machines come apart; living things do not.</summary>
        public static void Death(Vector3 point, Color tint, bool leaveSplat)
        {
            if (CombatEffectsView.Instance != null)
                CombatEffectsView.Instance.AddDeath(point, tint, leaveSplat);
        }
    }

    [DisallowMultipleComponent]
    internal sealed class CombatEffectsView : MonoBehaviour
    {
        private const int MaxTracers = 40;
        private const int MaxSparks = 40;
        private const int MaxDroplets = 140;
        private const int MaxSplats = 16;

        private static CombatEffectsView _instance;
        private static bool _unavailable;

        internal static CombatEffectsView Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (_unavailable) return null;

                var host = new GameObject("CombatEffects");
                DontDestroyOnLoad(host);
                host.hideFlags = HideFlags.DontSave;
                _instance = host.AddComponent<CombatEffectsView>();
                return _instance;
            }
        }

        private readonly Queue<CombatTracer> _freeTracers = new Queue<CombatTracer>();
        private readonly Queue<CombatSpark> _freeSparks = new Queue<CombatSpark>();
        private readonly Queue<CombatDroplet> _freeDroplets = new Queue<CombatDroplet>();
        private readonly Queue<CombatSplat> _freeSplats = new Queue<CombatSplat>();
        private readonly List<CombatTracer> _liveTracers = new List<CombatTracer>();
        private readonly List<CombatSpark> _liveSparks = new List<CombatSpark>();
        private readonly List<CombatDroplet> _liveDroplets = new List<CombatDroplet>();
        private readonly List<CombatSplat> _liveSplats = new List<CombatSplat>();

        private Material _glow;
        private Material _fluid;
        private Transform _root;
        private Camera _viewCamera;

        /// <summary>
        /// Blood is drawn as flat discs, so it has to be turned to the lens every frame -
        /// a droplet that keeps the orientation it was spawned with spends half its life
        /// edge-on and vanishes. Resolved once rather than through Camera.main per droplet.
        /// </summary>
        private Camera ViewCamera =>
            _viewCamera != null ? _viewCamera : (_viewCamera = Camera.main);

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                _unavailable = true;
                return;
            }

            _instance = this;
            _root = transform;
            _glow = BuildGlowMaterial();
            _fluid = BuildFluidMaterial();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            for (int i = _liveTracers.Count - 1; i >= 0; i--)
            {
                var tracer = _liveTracers[i];
                if (tracer.Advance(Time.deltaTime)) continue;
                _liveTracers.RemoveAt(i);
                _freeTracers.Enqueue(tracer);
            }

            for (int i = _liveSparks.Count - 1; i >= 0; i--)
            {
                var spark = _liveSparks[i];
                if (spark.Advance(Time.deltaTime)) continue;
                _liveSparks.RemoveAt(i);
                _freeSparks.Enqueue(spark);
            }

            for (int i = _liveDroplets.Count - 1; i >= 0; i--)
            {
                var droplet = _liveDroplets[i];
                if (droplet.Advance(Time.deltaTime)) continue;
                _liveDroplets.RemoveAt(i);
                _freeDroplets.Enqueue(droplet);
            }

            for (int i = _liveSplats.Count - 1; i >= 0; i--)
            {
                var splat = _liveSplats[i];
                if (splat.Advance(Time.deltaTime)) continue;
                _liveSplats.RemoveAt(i);
                _freeSplats.Enqueue(splat);
            }
        }

        internal void AddShot(Vector3 from, Vector3 to, Color tint, float width)
        {
            if (_glow == null) return;

            var tracer = TakeTracer();
            if (tracer != null)
            {
                tracer.Play(from, to, tint, width, _glow);
                _liveTracers.Add(tracer);
            }

            // A round starts somewhere the player can see. The flash faces back along the
            // shot line so the disc turns to the camera instead of sitting edge-on.
            var flash = TakeSpark();
            if (flash != null)
            {
                Vector3 line = to - from;
                flash.Play(from, line.sqrMagnitude > 0.0001f ? -line.normalized : Vector3.back,
                    tint, _glow);
                _liveSparks.Add(flash);
            }
        }

        internal void AddImpact(Vector3 point, Vector3 normal, Color tint)
        {
            if (_glow == null) return;

            var spark = TakeSpark();
            if (spark == null) return;
            spark.Play(point, normal, tint, _glow);
            _liveSparks.Add(spark);
        }

        internal void AddWound(Vector3 point, Vector3 away, float severity, Color tint)
        {
            if (_fluid == null) return;

            int count = Mathf.Clamp(2 + Mathf.RoundToInt(5f * Mathf.Clamp01(severity)), 2, 7);
            Vector3 push = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.up;

            for (int i = 0; i < count; i++)
            {
                var droplet = TakeDroplet();
                if (droplet == null) return;

                Vector3 velocity = push * Random.Range(1.6f, 4.2f)
                    + Random.onUnitSphere * 1.1f
                    + Vector3.up * Random.Range(0.9f, 2.6f);
                droplet.Play(point, velocity, Random.Range(0.34f, 0.62f),
                    Random.Range(0.05f, 0.13f), tint, _fluid, ViewCamera);
                _liveDroplets.Add(droplet);
            }
        }

        internal void AddDeath(Vector3 point, Color tint, bool leaveSplat)
        {
            if (_fluid == null) return;

            for (int i = 0; i < 16; i++)
            {
                var droplet = TakeDroplet();
                if (droplet == null) break;

                Vector3 velocity = Random.onUnitSphere * Random.Range(1.4f, 3.6f)
                    + Vector3.up * Random.Range(1.6f, 3.8f);
                droplet.Play(point + Random.insideUnitSphere * 0.25f, velocity,
                    Random.Range(0.5f, 0.95f), Random.Range(0.07f, 0.19f), tint, _fluid,
                    ViewCamera);
                _liveDroplets.Add(droplet);
            }

            if (!leaveSplat) return;

            var splat = TakeSplat();
            if (splat == null) return;
            if (splat.Play(point, tint, _fluid)) _liveSplats.Add(splat);
            else _freeSplats.Enqueue(splat);
        }

        private CombatTracer TakeTracer()
        {
            if (_freeTracers.Count > 0) return _freeTracers.Dequeue();
            if (_liveTracers.Count + _freeTracers.Count >= MaxTracers) return null;

            var go = new GameObject("Tracer");
            go.transform.SetParent(_root, false);
            go.hideFlags = HideFlags.DontSave;
            return go.AddComponent<CombatTracer>();
        }

        private CombatSpark TakeSpark()
        {
            if (_freeSparks.Count > 0) return _freeSparks.Dequeue();
            if (_liveSparks.Count + _freeSparks.Count >= MaxSparks) return null;

            var go = new GameObject("Spark");
            go.transform.SetParent(_root, false);
            go.hideFlags = HideFlags.DontSave;
            return go.AddComponent<CombatSpark>();
        }

        private CombatDroplet TakeDroplet()
        {
            if (_freeDroplets.Count > 0) return _freeDroplets.Dequeue();
            if (_liveDroplets.Count + _freeDroplets.Count >= MaxDroplets) return null;

            var go = new GameObject("Droplet");
            go.transform.SetParent(_root, false);
            go.hideFlags = HideFlags.DontSave;
            return go.AddComponent<CombatDroplet>();
        }

        private CombatSplat TakeSplat()
        {
            if (_freeSplats.Count > 0) return _freeSplats.Dequeue();
            if (_liveSplats.Count + _freeSplats.Count >= MaxSplats) return null;

            var go = new GameObject("Splat");
            go.transform.SetParent(_root, false);
            go.hideFlags = HideFlags.DontSave;
            return go.AddComponent<CombatSplat>();
        }

        /// <summary>
        /// URP Lit is already in the build - every kit material uses it - so this shader
        /// resolves on device. The old code reached for "Standard", which Unity strips
        /// from a URP player, and handed back a material with no shader in it.
        /// </summary>
        private static Material BuildGlowMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[NullFrontier] URP Lit is unavailable, so shot effects are disabled.");
                return null;
            }

            var material = new Material(shader) { name = "NF Shot Glow" };
            material.color = Color.white;
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.white);
            material.hideFlags = HideFlags.DontSave;
            return material;
        }

        /// <summary>
        /// Fluid, not light. Blood has to read as wet tissue rather than as glowing
        /// plasma, so the emission gain here is a highlight and the base colour carries
        /// the material - which is also what lets one material serve coolant and oil by
        /// tinting _BaseColor per droplet.
        /// </summary>
        private static Material BuildFluidMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            var material = new Material(shader) { name = "NF Combat Fluid" };
            material.color = Color.white;
            material.SetColor("_BaseColor", new Color(0.42f, 0.03f, 0.05f));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.86f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.10f, 0.005f, 0.01f));
            material.hideFlags = HideFlags.DontSave;
            return material;
        }
    }

    internal sealed class CombatTracer : MonoBehaviour
    {
        private const float Duration = 0.075f;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private LineRenderer _line;
        private MaterialPropertyBlock _block;
        private Color _tint;
        private float _age;

        private void Awake()
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.positionCount = 2;
            _line.useWorldSpace = true;
            _line.numCapVertices = 4;
            _line.numCornerVertices = 4;
            _line.shadowCastingMode = ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.lightProbeUsage = LightProbeUsage.Off;
            _block = new MaterialPropertyBlock();
        }

        internal void Play(Vector3 from, Vector3 to, Color tint, float width, Material shared)
        {
            _age = 0f;
            _tint = tint;
            _line.widthMultiplier = width;
            _line.sharedMaterial = shared;
            _line.SetPosition(0, from);
            _line.SetPosition(1, to);
            WriteBlock(1f);
            gameObject.SetActive(true);
        }

        internal bool Advance(float deltaTime)
        {
            _age += deltaTime;
            float t = 1f - _age / Duration;
            if (t <= 0f)
            {
                gameObject.SetActive(false);
                return false;
            }

            // The streak shortens toward its far end rather than fading out, which reads
            // as a round leaving the frame instead of a line going dim.
            Vector3 near = _line.GetPosition(0);
            Vector3 far = _line.GetPosition(1);
            _line.SetPosition(0, Vector3.Lerp(near, far, 1f - t));
            WriteBlock(t);
            return true;
        }

        private void WriteBlock(float strength)
        {
            _line.GetPropertyBlock(_block);
            Color lit = _tint * (3.5f * strength);
            _block.SetColor(EmissionId, lit);
            _line.SetPropertyBlock(_block);
        }
    }

    internal sealed class CombatSpark : MonoBehaviour
    {
        private const float Duration = 0.16f;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private Color _tint;
        private float _age;

        private void Awake()
        {
            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildDisc();

            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _block = new MaterialPropertyBlock();
        }

        internal void Play(Vector3 point, Vector3 normal, Color tint, Material shared)
        {
            _age = 0f;
            _tint = tint;
            transform.SetPositionAndRotation(point + normal * 0.03f,
                Quaternion.LookRotation(normal == Vector3.zero ? Vector3.back : normal));
            _renderer.sharedMaterial = shared;
            transform.localScale = Vector3.one * 0.1f;
            WriteBlock(1f);
            gameObject.SetActive(true);
        }

        internal bool Advance(float deltaTime)
        {
            _age += deltaTime;
            float t = 1f - _age / Duration;
            if (t <= 0f)
            {
                gameObject.SetActive(false);
                return false;
            }

            transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 0.42f, 1f - t);
            WriteBlock(t);
            return true;
        }

        private void WriteBlock(float strength)
        {
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(EmissionId, _tint * (5f * strength));
            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>Unit circle in the XY plane facing +Z, so the same winding that makes
        /// the disc visible also makes LookRotation(surface normal) aim it out of the wall.
        /// Shared with the fluid effects: one mesh, three uses.</summary>
        internal static Mesh _disc;

        internal static Mesh BuildDisc()
        {
            if (_disc != null) return _disc;

            const int segments = 12;
            var verts = new List<Vector3> { Vector3.zero };
            var norms = new List<Vector3> { Vector3.forward };
            var tris = new List<int>();

            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
                norms.Add(Vector3.forward);
            }

            for (int i = 1; i <= segments; i++)
            {
                tris.Add(0);
                tris.Add(i + 1);
                tris.Add(i);
            }

            _disc = new Mesh { name = "NF Spark Disc" };
            _disc.SetVertices(verts);
            _disc.SetNormals(norms);
            _disc.SetTriangles(tris, 0);
            _disc.RecalculateBounds();
            _disc.UploadMeshData(true);
            _disc.hideFlags = HideFlags.DontSave;
            return _disc;
        }
    }

    /// <summary>
    /// A piece of a body thrown by a round. Small, short-lived and pooled: a firefight
    /// with an assault rifle asks for sixty of these a second and a phone should not
    /// notice.
    /// </summary>
    internal sealed class CombatDroplet : MonoBehaviour
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private Vector3 _velocity;
        private Camera _view;
        private Color _tint;
        private float _life;
        private float _age;
        private float _size;

        private void Awake()
        {
            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = CombatSpark.BuildDisc();

            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _block = new MaterialPropertyBlock();
        }

        internal void Play(Vector3 point, Vector3 velocity, float life, float size,
            Color tint, Material shared, Camera view)
        {
            _age = 0f;
            _life = life;
            _size = size;
            _tint = tint;
            _velocity = velocity;
            _view = view;

            transform.SetPositionAndRotation(point, Quaternion.LookRotation(velocity));
            transform.localScale = Vector3.one * size;
            _renderer.sharedMaterial = shared;
            Write(1f);
            gameObject.SetActive(true);
        }

        internal bool Advance(float deltaTime)
        {
            _age += deltaTime;
            float t = 1f - _age / _life;
            if (t <= 0f)
            {
                gameObject.SetActive(false);
                return false;
            }

            _velocity += Physics.gravity * (0.55f * deltaTime);
            Vector3 where = transform.position + _velocity * deltaTime;

            // The street is the floor of this world; a droplet that passes through it
            // falls out of the game instead of landing.
            if (where.y <= 0.03f)
            {
                where.y = 0.03f;
                _velocity = Vector3.zero;
            }

            transform.position = where;
            if (_view != null)
            {
                Vector3 toLens = transform.position - _view.transform.position;
                if (toLens.sqrMagnitude > 0.0001f)
                {
                    // A flat disc must face the lens or it spends half its life edge-on and
                    // disappears. The roll about that axis is what keeps the spray from
                    // looking like a wall of identical coins.
                    transform.rotation = Quaternion.LookRotation(toLens)
                        * Quaternion.Euler(0f, 0f, _age * 320f);
                }
            }
            transform.localScale = Vector3.one * (_size * Mathf.Lerp(0.55f, 1f, t));
            Write(t);
            return true;
        }

        private void Write(float strength)
        {
            _block.SetColor(BaseColorId, _tint);
            _block.SetColor(EmissionId, _tint * (0.3f * strength));
            _renderer.SetPropertyBlock(_block);
        }
    }

    /// <summary>
    /// What is left on the ground after a body stops moving. It does not fade - a mobile
    /// forward pass has no cheap transparent surface to fade with - it dries by shrinking,
    /// which is what a real splat does and costs the same one draw call.
    /// </summary>
    internal sealed class CombatSplat : MonoBehaviour
    {
        private const float Life = 7f;
        private const float DrySeconds = 2.4f;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private Color _tint;
        private Vector3 _wet;
        private float _age;

        private void Awake()
        {
            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = CombatSpark.BuildDisc();

            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _block = new MaterialPropertyBlock();
        }

        /// <summary>False when there is no surface under the body to mark.</summary>
        internal bool Play(Vector3 body, Color tint, Material shared)
        {
            Vector3 start = body + Vector3.up * 0.4f;
            if (!Physics.Raycast(start, Vector3.down, out RaycastHit hit, 4f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;

            _age = 0f;
            _tint = tint;
            float spread = Random.Range(0.42f, 0.78f);
            _wet = new Vector3(spread, spread * Random.Range(0.6f, 0.9f), 1f);

            transform.SetPositionAndRotation(hit.point + hit.normal * 0.014f,
                Quaternion.LookRotation(hit.normal)
                * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            transform.localScale = _wet;
            _renderer.sharedMaterial = shared;
            Write(1f);
            gameObject.SetActive(true);
            return true;
        }

        internal bool Advance(float deltaTime)
        {
            _age += deltaTime;
            float t = 1f - _age / Life;
            if (t <= 0f)
            {
                gameObject.SetActive(false);
                return false;
            }

            // Wet for the first seconds, then it dries by shrinking to four tenths.
            float dry = Mathf.Clamp01((_age - (Life - DrySeconds)) / DrySeconds);
            transform.localScale = _wet * Mathf.Lerp(1f, 0.4f, dry);
            Write(t);
            return true;
        }

        private void Write(float strength)
        {
            _block.SetColor(BaseColorId, _tint);
            _block.SetColor(EmissionId, _tint * (0.22f * strength));
            _renderer.SetPropertyBlock(_block);
        }
    }
}
