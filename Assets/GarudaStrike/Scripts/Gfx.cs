using System.Collections.Generic;
using UnityEngine;

namespace GarudaStrike
{
    /// <summary>Pembuat model 3D dari primitif + material otomatis (kompatibel Built-in & URP).</summary>
    public static class Gfx
    {
        static Shader shader;
        static readonly Dictionary<int, Material> cache = new Dictionary<int, Material>();

        static Shader Sh()
        {
            if (shader == null)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shader = p.GetComponent<Renderer>().sharedMaterial.shader;
                Object.Destroy(p);
            }
            return shader;
        }

        public static Color Dk(Color c, float f) { return new Color(c.r * f, c.g * f, c.b * f, 1f); }

        public static Material Mat(Color c, float smooth = 0.15f, float metal = 0f, float emit = 0f)
        {
            int key = Mathf.RoundToInt(c.r * 255f);
            key = key * 257 + Mathf.RoundToInt(c.g * 255f);
            key = key * 257 + Mathf.RoundToInt(c.b * 255f);
            key = key * 131 + Mathf.RoundToInt(smooth * 100f);
            key = key * 131 + Mathf.RoundToInt(metal * 100f);
            key = key * 131 + Mathf.RoundToInt(emit * 10f);
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            c.a = 1f;
            m = new Material(Sh());
            m.name = "gs_mat";
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (emit > 0f)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * emit);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            cache[key] = m;
            return m;
        }

        public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c, Vector3 euler,
            bool col = true, float smooth = 0.15f, float metal = 0f, float emit = 0f)
        {
            var g = GameObject.CreatePrimitive(t);
            var tr = g.transform;
            tr.SetParent(parent, false);
            tr.localPosition = pos;
            tr.localEulerAngles = euler;
            tr.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = Mat(c, smooth, metal, emit);
            if (!col)
            {
                var cd = g.GetComponent<Collider>();
                if (cd != null) Object.DestroyImmediate(cd);
            }
            return g;
        }

        public static GameObject Cube(Transform p, Vector3 pos, Vector3 s, Color c, bool col = true, float smooth = 0.15f, float metal = 0f, float emit = 0f)
        { return Prim(PrimitiveType.Cube, p, pos, s, c, Vector3.zero, col, smooth, metal, emit); }

        public static GameObject Sph(Transform p, Vector3 pos, Vector3 s, Color c, bool col = true, float smooth = 0.15f, float metal = 0f, float emit = 0f)
        { return Prim(PrimitiveType.Sphere, p, pos, s, c, Vector3.zero, col, smooth, metal, emit); }

        /// <summary>Silinder tegak. pos = titik tengah.</summary>
        public static GameObject Cyl(Transform p, Vector3 pos, float r, float h, Color c, bool col = true, float smooth = 0.15f, float metal = 0f, float emit = 0f)
        { return Prim(PrimitiveType.Cylinder, p, pos, new Vector3(r * 2f, h * 0.5f, r * 2f), c, Vector3.zero, col, smooth, metal, emit); }

        /// <summary>Silinder rebah searah sumbu Z (laras senjata).</summary>
        public static GameObject Tube(Transform p, Vector3 pos, float r, float len, Color c, bool col = false, float smooth = 0.4f, float metal = 0.6f, float emit = 0f)
        { return Prim(PrimitiveType.Cylinder, p, pos, new Vector3(r * 2f, len * 0.5f, r * 2f), c, new Vector3(90f, 0f, 0f), col, smooth, metal, emit); }

        public static Light PointLight(Transform p, Vector3 pos, Color c, float range, float intensity)
        {
            var g = new GameObject("Light");
            g.transform.SetParent(p, false);
            g.transform.localPosition = pos;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        public static void NoShadow(GameObject g)
        {
            var r = g.GetComponent<Renderer>();
            if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    public class Debris : MonoBehaviour
    {
        public Vector3 vel;
        public float life = 0.5f;
        float t;
        Vector3 s0;
        void Start() { s0 = transform.localScale; }
        void Update()
        {
            t += Time.deltaTime;
            vel += Vector3.down * 14f * Time.deltaTime;
            Vector3 np = transform.position + vel * Time.deltaTime;
            if (np.y < 0.02f) { np.y = 0.02f; vel = Vector3.zero; }
            transform.position = np;
            transform.localScale = s0 * Mathf.Clamp01(1f - t / life);
            if (t >= life) Destroy(gameObject);
        }
    }

    public class Timed : MonoBehaviour
    {
        public float life = 0.2f, grow = 1f;
        float t;
        Vector3 s0;
        void Start() { s0 = transform.localScale; }
        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / life);
            transform.localScale = s0 * Mathf.Lerp(1f, grow, k);
            if (t >= life) Destroy(gameObject);
        }
    }

    public static class Fx
    {
        public static Transform Root;
        static readonly Queue<GameObject> holes = new Queue<GameObject>();

        public static void Reset() { holes.Clear(); }

        public static void Sparks(Vector3 pos, Vector3 n, Color c, int count, float speed = 4f, float emit = 1.5f)
        {
            for (int i = 0; i < count; i++)
            {
                var g = Gfx.Cube(Root, pos, Vector3.one * Random.Range(0.025f, 0.06f), c, false, 0.1f, 0f, emit);
                var d = g.AddComponent<Debris>();
                d.vel = (n + Random.insideUnitSphere * 0.9f).normalized * Random.Range(1f, speed);
                d.life = Random.Range(0.25f, 0.55f);
            }
        }

        public static void Tracer(Vector3 a, Vector3 b, Color c, float width = 0.02f, float life = 0.05f)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.6f) return;
            var g = Gfx.Prim(PrimitiveType.Cube, Root, a + d * 0.5f, new Vector3(width, width, len), c, Vector3.zero, false, 0.1f, 0f, 3f);
            g.transform.rotation = Quaternion.LookRotation(d);
            Gfx.NoShadow(g);
            Object.Destroy(g, life);
        }

        public static void Hole(Vector3 p, Vector3 n)
        {
            var g = Gfx.Prim(PrimitiveType.Cube, Root, p + n * 0.012f, new Vector3(0.09f, 0.09f, 0.01f), new Color(0.04f, 0.04f, 0.04f), Vector3.zero, false);
            g.transform.rotation = Quaternion.LookRotation(n);
            Gfx.NoShadow(g);
            holes.Enqueue(g);
            if (holes.Count > 70)
            {
                var o = holes.Dequeue();
                if (o != null) Object.Destroy(o);
            }
        }

        public static void Burst(Vector3 pos, Color c, float size, float life, float lightRange = 0f)
        {
            var g = Gfx.Sph(Root, pos, Vector3.one * size * 0.25f, c, false, 0.1f, 0f, 4f);
            Gfx.NoShadow(g);
            var t = g.AddComponent<Timed>();
            t.life = life;
            t.grow = 4f;
            if (lightRange > 0f)
            {
                var l = g.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = c;
                l.range = lightRange;
                l.intensity = 4f;
                l.shadows = LightShadows.None;
            }
        }

        public static void Explosion(Vector3 pos, float radius)
        {
            Burst(pos, new Color(1f, 0.55f, 0.1f), radius * 0.9f, 0.35f, radius * 2f);
            Burst(pos + Vector3.up * 0.3f, new Color(1f, 0.92f, 0.5f), radius * 0.5f, 0.2f);
            Sparks(pos, Vector3.up, new Color(1f, 0.6f, 0.2f), 28, 10f);
            Sparks(pos, Vector3.up, new Color(0.18f, 0.18f, 0.18f), 14, 6f, 0f);
            Sfx.Play("explosion", pos, 1.3f);
        }
    }

    /// <summary>Efek suara & musik yang disintesis lewat kode (tanpa file audio).</summary>
    public static class Sfx
    {
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static bool built;
        static AudioSource musicSrc;
        const float TAU = 6.2831853f;

        static float N() { return Random.value * 2f - 1f; }
        static float Env(float t, float k) { return Mathf.Exp(-t * k); }
        static float Sin(float f, float t) { return Mathf.Sin(TAU * f * t); }
        static float Click(float t, float t0)
        {
            if (t < t0) return 0f;
            float u = t - t0;
            return N() * Env(u, 90f) * 0.6f + Sin(240f, u) * Env(u, 60f) * 0.35f;
        }

        static AudioClip Gen(string n, float dur, System.Func<float, float> f)
        {
            int sr = 44100;
            int len = Mathf.CeilToInt(sr * dur);
            var d = new float[len];
            for (int i = 0; i < len; i++) d[i] = Mathf.Clamp(f(i / (float)sr), -1f, 1f);
            var c = AudioClip.Create(n, len, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip Shot(string n, float dur, float nk, float f0, float fk, float lpf, float gain)
        {
            float s = 0f;
            return Gen(n, dur, t =>
            {
                s += (N() - s) * lpf;
                return (s * Env(t, nk) * 1.8f + Sin(f0 * (1f - t * 0.5f), t) * Env(t, fk)) * gain;
            });
        }

        public static void Build()
        {
            if (built) return;
            built = true;
            clips["pistol"] = Shot("pistol", 0.3f, 34f, 170f, 26f, 0.5f, 0.9f);
            clips["rifle"] = Shot("rifle", 0.25f, 30f, 120f, 26f, 0.4f, 0.95f);
            clips["shotgun"] = Shot("shotgun", 0.7f, 10f, 75f, 9f, 0.25f, 1f);
            clips["sniper"] = Shot("sniper", 1.1f, 7f, 55f, 5f, 0.2f, 1f);
            clips["enemy"] = Shot("enemy", 0.25f, 34f, 140f, 28f, 0.45f, 0.8f);
            clips["empty"] = Gen("empty", 0.08f, t => N() * Env(t, 150f) * 0.5f);
            clips["reload"] = Gen("reload", 1.0f, t => Click(t, 0.05f) + Click(t, 0.45f) * 1.3f + Click(t, 0.8f));
            clips["hit"] = Gen("hit", 0.09f, t => Sin(1300f, t) * Env(t, 60f) * 0.6f);
            clips["headshot"] = Gen("headshot", 0.18f, t => (Sin(1900f, t) + Sin(1200f, t)) * Env(t, 35f) * 0.4f);
            clips["kill"] = Gen("kill", 0.3f, t => Sin(700f * (1f + t * 2f), t) * Env(t, 10f) * 0.5f);
            clips["hurt"] = Gen("hurt", 0.35f, t => (Sin(85f, t) * Env(t, 10f) + N() * Env(t, 25f) * 0.3f) * 0.9f);
            float ls = 0f;
            clips["step"] = Gen("step", 0.16f, t => { ls += (N() - ls) * 0.2f; return ls * Env(t, 28f) * 1.2f; });
            clips["pickup"] = Gen("pickup", 0.3f, t => Sin(600f * (1f + 2f * t), t) * Env(t, 8f) * 0.5f);
            float es = 0f;
            clips["explosion"] = Gen("explosion", 1.6f, t => { es += (N() - es) * 0.12f; return (es * Env(t, 2.6f) * 3f + Sin(48f * (1f - t * 0.3f), t) * Env(t, 3f)) * 0.9f; });
            float ks = 0f;
            clips["knife"] = Gen("knife", 0.3f, t => { ks += (N() - ks) * 0.5f; return ks * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.3f)) * 0.5f; });
            clips["melee"] = Gen("melee", 0.15f, t => (Sin(120f, t) * Env(t, 30f) * 0.8f + N() * Env(t, 50f) * 0.3f));
            clips["click"] = Gen("click", 0.06f, t => Sin(1000f, t) * Env(t, 70f) * 0.4f);
            clips["horn"] = Gen("horn", 1.2f, t => (Sin(196f, t) + Sin(294f, t) * 0.8f + Sin(392f, t) * 0.5f) * Mathf.Min(1f, t * 8f) * Env(t, 2.2f) * 0.25f);

            float[] roots = { 110f, 87.31f, 130.81f, 98f };
            float cd = 2.6667f;
            clips["music"] = Gen("music", cd * 4f, t =>
            {
                int ci = Mathf.Min(3, (int)(t / cd));
                float lt = t - ci * cd;
                float r = roots[ci];
                float env = Mathf.Clamp01(lt / 0.5f) * Mathf.Clamp01((cd - lt) / 0.5f);
                float pad = (Sin(r * 2f, t) + 0.6f * Sin(r * 3f, t) + 0.4f * Sin(r * 4f, t)) * env * 0.12f;
                float beat = lt % 0.6667f;
                float bass = Sin(r, t) * Env(beat, 4f) * 0.35f * Mathf.Clamp01((cd - lt) / 0.05f);
                int step = (int)(lt / 0.3333f);
                int m = step % 4;
                float an = r * 4f * (m == 0 ? 1f : (m == 1 ? 1.5f : (m == 2 ? 2f : 1.5f)));
                float ab = lt % 0.3333f;
                float arp = Sin(an, t) * Env(ab, 9f) * 0.07f * Mathf.Clamp01((cd - lt) / 0.1f);
                return pad + bass + arp;
            });
        }

        public static AudioClip Get(string k)
        {
            Build();
            AudioClip c;
            clips.TryGetValue(k, out c);
            return c;
        }

        public static void Play(string key, Vector3 pos, float vol = 1f, float pitch = 1f)
        {
            var c = Get(key);
            if (c == null) return;
            var g = new GameObject("sfx");
            g.transform.position = pos;
            var a = g.AddComponent<AudioSource>();
            a.clip = c;
            a.volume = vol;
            a.pitch = pitch;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 4f;
            a.maxDistance = 80f;
            a.Play();
            Object.Destroy(g, c.length / Mathf.Max(0.1f, pitch) + 0.2f);
        }

        public static void Play2D(string key, float vol = 1f, float pitch = 1f)
        {
            var c = Get(key);
            if (c == null) return;
            var g = new GameObject("sfx2d");
            var a = g.AddComponent<AudioSource>();
            a.clip = c;
            a.volume = vol;
            a.pitch = pitch;
            a.spatialBlend = 0f;
            a.Play();
            Object.Destroy(g, c.length / Mathf.Max(0.1f, pitch) + 0.2f);
        }

        public static void Music(bool on, float volume)
        {
            Build();
            if (musicSrc == null)
            {
                var g = new GameObject("Music");
                Object.DontDestroyOnLoad(g);
                musicSrc = g.AddComponent<AudioSource>();
                musicSrc.clip = clips["music"];
                musicSrc.loop = true;
                musicSrc.spatialBlend = 0f;
            }
            musicSrc.volume = volume;
            if (on && !musicSrc.isPlaying) musicSrc.Play();
            if (!on && musicSrc.isPlaying) musicSrc.Stop();
        }
    }
}
