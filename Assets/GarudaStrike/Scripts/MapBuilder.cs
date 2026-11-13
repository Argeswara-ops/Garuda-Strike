using System.Collections.Generic;
using UnityEngine;

namespace GarudaStrike
{
    public class MapInfo
    {
        public string name;
        public Vector3 playerSpawn;
        public float playerYaw;
        public List<Vector3> enemySpawns = new List<Vector3>();
        public List<Vector3> pickupSpawns = new List<Vector3>();
        public Vector3 center;
        public float orbitRadius = 30f, orbitHeight = 14f;
        public Color sky;
    }

    public static class MapBuilder
    {
        static Transform R;
        static System.Random rng;
        static float Rf() { return (float)rng.NextDouble(); }
        static float Rr(float a, float b) { return a + (b - a) * Rf(); }

        static readonly Color[] CrateCols =
        {
            new Color(0.55f, 0.38f, 0.2f), new Color(0.48f, 0.33f, 0.18f), new Color(0.2f, 0.35f, 0.5f),
            new Color(0.3f, 0.45f, 0.25f), new Color(0.6f, 0.5f, 0.3f)
        };

        public static MapInfo Build(int id, Transform root)
        {
            R = root;
            rng = new System.Random(777 + id);
            return id == 0 ? Warehouse() : Desert();
        }

        // ---------- helper ----------
        static GameObject Box(float x, float y, float z, float w, float h, float d, Color c, bool col = true,
            float smooth = 0.1f, float metal = 0f, float emit = 0f, float rotY = 0f)
        {
            return Gfx.Prim(PrimitiveType.Cube, R, new Vector3(x, y + h * 0.5f, z), new Vector3(w, h, d), c,
                new Vector3(0f, rotY, 0f), col, smooth, metal, emit);
        }

        static Transform Group(string n, float x, float y, float z, float rotY)
        {
            var g = new GameObject(n).transform;
            g.SetParent(R, false);
            g.localPosition = new Vector3(x, y, z);
            g.localEulerAngles = new Vector3(0f, rotY, 0f);
            return g;
        }

        static void Ramp(Vector3 bottom, Vector3 top, float width, Color c)
        {
            Vector3 d = top - bottom;
            var g = Gfx.Prim(PrimitiveType.Cube, R, (bottom + top) * 0.5f, new Vector3(width, 0.25f, d.magnitude), c, Vector3.zero, true, 0.1f);
            g.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            g.transform.position -= g.transform.up * 0.125f;
        }

        static void Sun(Vector3 euler, Color c, float intensity)
        {
            var g = new GameObject("Sun");
            g.transform.SetParent(R, false);
            g.transform.rotation = Quaternion.Euler(euler);
            var l = g.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = c;
            l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.85f;
        }

        static void Env(Color ambient, bool fog, Color fogCol, float dens)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.skybox = null;
            RenderSettings.fog = fog;
            if (fog)
            {
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogColor = fogCol;
                RenderSettings.fogDensity = dens;
            }
        }

        static void Crates(float cx, float cz, int nx, int nz, int layers)
        {
            for (int l = 0; l < layers; l++)
                for (int ix = 0; ix < nx; ix++)
                    for (int iz = 0; iz < nz; iz++)
                    {
                        if (l > 0 && Rf() < 0.3f) continue;
                        Color c = CrateCols[rng.Next(CrateCols.Length)];
                        Box(cx + ix * 1.05f + Rr(-0.04f, 0.04f), l * 1f, cz + iz * 1.05f + Rr(-0.04f, 0.04f), 1f, 1f, 1f, c, true, 0.1f);
                        Box(cx + ix * 1.05f, l * 1f + 0.45f, cz + iz * 1.05f, 1.02f, 0.1f, 1.02f, Gfx.Dk(c, 0.7f), false);
                    }
        }

        static void Barrel(float x, float z, Color c)
        {
            Gfx.Cyl(R, new Vector3(x, 0.5f, z), 0.33f, 1f, c, true, 0.4f, 0.4f);
            Gfx.Cyl(R, new Vector3(x, 0.3f, z), 0.345f, 0.06f, Gfx.Dk(c, 0.6f), false, 0.4f, 0.4f);
            Gfx.Cyl(R, new Vector3(x, 0.7f, z), 0.345f, 0.06f, Gfx.Dk(c, 0.6f), false, 0.4f, 0.4f);
        }

        static void Barrier(float x, float z, bool alongX)
        {
            float w = alongX ? 5f : 0.8f, d = alongX ? 0.8f : 5f;
            Box(x, 0f, z, w, 0.95f, d, new Color(0.62f, 0.62f, 0.64f), true, 0.1f);
            Box(x, 0.95f, z, w + 0.02f, 0.12f, d + 0.02f, new Color(0.95f, 0.75f, 0.1f), false);
        }

        // ================= MAP 1: GUDANG =================
        static MapInfo Warehouse()
        {
            var m = new MapInfo();
            m.name = "GUDANG LOGISTIK";
            m.sky = new Color(0.05f, 0.06f, 0.08f);
            m.playerSpawn = new Vector3(0f, 0.05f, -29f);
            m.playerYaw = 0f;
            m.center = Vector3.zero;
            m.orbitRadius = 23f;
            m.orbitHeight = 7.5f;
            Env(new Color(0.40f, 0.41f, 0.46f), true, new Color(0.10f, 0.11f, 0.13f), 0.006f);
            Sun(new Vector3(62f, 25f, 0f), new Color(0.85f, 0.9f, 1f), 0.55f);

            Color concrete = new Color(0.36f, 0.37f, 0.39f), wall = new Color(0.52f, 0.53f, 0.56f), steel = new Color(0.18f, 0.19f, 0.22f);
            Color yellow = new Color(0.95f, 0.78f, 0.1f), band = new Color(0.28f, 0.3f, 0.34f);

            Box(0f, -1f, 0f, 74f, 1f, 74f, concrete, true, 0.35f);
            Box(0f, 0f, 36f, 74f, 10f, 1f, wall);
            Box(0f, 0f, -36f, 74f, 10f, 1f, wall);
            Box(36f, 0f, 0f, 1f, 10f, 74f, wall);
            Box(-36f, 0f, 0f, 1f, 10f, 74f, wall);
            Gfx.NoShadow(Box(0f, 10f, 0f, 74f, 1f, 74f, steel, false));

            // dekorasi dinding
            Box(0f, 0f, 35.45f, 71f, 1.4f, 0.1f, band, false);
            Box(0f, 0f, -35.45f, 71f, 1.4f, 0.1f, band, false);
            Box(35.45f, 0f, 0f, 0.1f, 1.4f, 71f, band, false);
            Box(-35.45f, 0f, 0f, 0.1f, 1.4f, 71f, band, false);
            for (int i = -3; i <= 3; i++)
            {
                Color win = new Color(0.6f, 0.75f, 1f);
                Box(i * 10f, 6.5f, 35.45f, 5f, 1.4f, 0.1f, win, false, 0.5f, 0f, 1.2f);
                Box(i * 10f, 6.5f, -35.45f, 5f, 1.4f, 0.1f, win, false, 0.5f, 0f, 1.2f);
                Box(35.45f, 6.5f, i * 10f, 0.1f, 1.4f, 5f, win, false, 0.5f, 0f, 1.2f);
                Box(-35.45f, 6.5f, i * 10f, 0.1f, 1.4f, 5f, win, false, 0.5f, 0f, 1.2f);
                Box(i * 10f + 5f, 0f, 35f, 1.2f, 10f, 1f, steel, false);
                Box(i * 10f + 5f, 0f, -35f, 1.2f, 10f, 1f, steel, false);
                Gfx.NoShadow(Box(i * 10f, 9f, 0f, 0.5f, 1f, 72f, Gfx.Dk(steel, 1.3f), false));
                Gfx.NoShadow(Box(0f, 9f, i * 10f, 72f, 0.5f, 0.5f, Gfx.Dk(steel, 1.3f), false));
            }

            // marka lantai
            Box(0f, 0f, -14f, 60f, 0.02f, 0.25f, yellow, false);
            Box(0f, 0f, 14f, 60f, 0.02f, 0.25f, yellow, false);
            Box(-12f, 0f, 0f, 0.25f, 0.02f, 60f, yellow, false);
            Box(12f, 0f, 0f, 0.25f, 0.02f, 60f, yellow, false);

            // pilar tengah
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Box(sx * 10f, 0f, sz * 10f, 1.2f, 10f, 1.2f, new Color(0.45f, 0.46f, 0.5f), true, 0.2f);
                    Box(sx * 10f, 0f, sz * 10f, 1.3f, 0.4f, 1.3f, yellow, false);
                }

            // lampu
            for (int ix = -1; ix <= 1; ix++)
                for (int iz = -1; iz <= 1; iz++)
                {
                    float lx = ix * 20f, lz = iz * 20f;
                    Gfx.NoShadow(Box(lx, 8.4f, lz, 2.4f, 0.2f, 0.8f, new Color(1f, 0.95f, 0.8f), false, 0.1f, 0f, 2f));
                    Box(lx, 8.6f, lz, 0.05f, 0.9f, 0.05f, steel, false);
                    Gfx.PointLight(R, new Vector3(lx, 7.8f, lz), new Color(1f, 0.92f, 0.8f), 26f, 1.1f);
                }

            // rak
            Color rackC = new Color(0.9f, 0.45f, 0.1f), shelfC = new Color(0.2f, 0.3f, 0.5f);
            float[] rackZ = { -20f, -8f, 6f, 18f };
            float[] rackX = { -27f, -21f };
            foreach (float rx in rackX)
                foreach (float rz in rackZ)
                {
                    float len = 6f;
                    for (int k = 0; k < 3; k++)
                        foreach (float ox in new float[] { -0.65f, 0.65f })
                            Box(rx + ox, 0f, rz - len * 0.5f + k * len * 0.5f, 0.1f, 4f, 0.1f, rackC, true);
                    for (int lv = 0; lv < 4; lv++)
                    {
                        float yy = 0.3f + lv * 1.2f;
                        Box(rx, yy, rz, 1.4f, 0.08f, len, shelfC, true);
                        if (lv == 3) continue;
                        for (int k = 0; k < 5; k++)
                        {
                            if (Rf() < 0.25f) continue;
                            Box(rx + Rr(-0.1f, 0.1f), yy + 0.08f, rz - len * 0.5f + 0.6f + k * 1.1f, 1f, Rr(0.6f, 1f), 0.9f, CrateCols[rng.Next(CrateCols.Length)], true);
                        }
                    }
                }

            // kontainer
            Container(-11f, 0f, 8f, true, new Color(0.65f, 0.15f, 0.12f));
            Container(-11f, 2.9f, 8f, false, new Color(0.12f, 0.28f, 0.55f));
            Container(14f, 0f, 10f, false, new Color(0.85f, 0.45f, 0.1f));
            Container(14f, 2.9f, 8f, true, new Color(0.15f, 0.45f, 0.25f));
            Container(-6f, 0f, -18f, true, new Color(0.8f, 0.7f, 0.15f));
            Ramp(new Vector3(14f, 0f, 22.5f), new Vector3(14f, 2.9f, 16.2f), 2.2f, new Color(0.35f, 0.35f, 0.38f));

            // catwalk timur
            Color cat = new Color(0.3f, 0.32f, 0.36f);
            Box(29.5f, 3.7f, 0f, 5f, 0.3f, 41f, cat, true, 0.3f, 0.3f);
            Box(27.1f, 4f, 0f, 0.12f, 1.1f, 41f, yellow, true);
            Box(31.9f, 4f, 0f, 0.12f, 1.1f, 41f, yellow, true);
            Box(29.5f, 4f, 20.6f, 5f, 1.1f, 0.12f, yellow, true);
            Box(29.5f, 4f, -20.6f, 5f, 1.1f, 0.12f, yellow, true);
            for (int i = -2; i <= 2; i++) Box(29.5f, 0f, i * 9f, 0.5f, 3.7f, 0.5f, steel, true);
            Ramp(new Vector3(29.5f, 0f, -29.5f), new Vector3(29.5f, 4f, -20.5f), 2.5f, cat);
            Ramp(new Vector3(29.5f, 0f, 29.5f), new Vector3(29.5f, 4f, 20.5f), 2.5f, cat);

            // peti
            Crates(-8f, -23f, 3, 2, 2);
            Crates(10f, -25f, 2, 3, 1);
            Crates(23f, -10f, 2, 2, 2);
            Crates(-2f, 16f, 3, 2, 1);
            Crates(4f, 26f, 2, 2, 2);
            Crates(23f, 26f, 3, 1, 2);

            // drum
            Color red = new Color(0.7f, 0.12f, 0.1f), blue = new Color(0.12f, 0.3f, 0.65f), yel = new Color(0.85f, 0.7f, 0.1f);
            Barrel(4f, -8f, red); Barrel(5.2f, -8.4f, red); Barrel(4.6f, -9.5f, red);
            Barrel(-8f, -2f, blue); Barrel(-7f, -2.6f, blue);
            Barrel(22f, 2f, yel); Barrel(22.8f, 2.6f, yel); Barrel(21.6f, 3f, yel);
            Barrel(0f, 26f, red); Barrel(1f, 26.6f, red);

            // barikade
            Barrier(-6f, -14f, true); Barrier(6f, -14f, true); Barrier(0f, -3f, false);
            Barrier(-14f, -2f, false); Barrier(16f, -3f, false); Barrier(0f, 10f, true);

            // forklift
            var f = Group("Forklift", 6f, 0f, -6f, 35f);
            var prev = R; R = f;
            Box(0f, 0.3f, 0f, 1.3f, 0.8f, 2.2f, new Color(0.95f, 0.75f, 0.05f), true, 0.3f);
            Box(0f, 1.1f, -0.5f, 1.2f, 0.1f, 1f, steel, false);
            Box(-0.55f, 1.1f, -0.5f, 0.08f, 1f, 0.08f, steel, false);
            Box(0.55f, 1.1f, -0.5f, 0.08f, 1f, 0.08f, steel, false);
            Box(-0.3f, 0.2f, 1.5f, 0.1f, 2.4f, 0.1f, steel, true);
            Box(0.3f, 0.2f, 1.5f, 0.1f, 2.4f, 0.1f, steel, true);
            Box(-0.4f, 0.1f, 2.3f, 0.12f, 0.08f, 1.2f, steel, false);
            Box(0.4f, 0.1f, 2.3f, 0.12f, 0.08f, 1.2f, steel, false);
            foreach (float wz in new float[] { -0.8f, 0.8f })
                foreach (float wx in new float[] { -0.7f, 0.7f })
                    Gfx.Prim(PrimitiveType.Cylinder, R, new Vector3(wx, 0.3f, wz), new Vector3(0.6f, 0.12f, 0.6f), new Color(0.05f, 0.05f, 0.05f), new Vector3(0f, 0f, 90f), false);
            R = prev;

            // spawn
            Vector3[] es = { new Vector3(-12f, 0f, 28f), new Vector3(-4f, 0f, 30f), new Vector3(6f, 0f, 29f), new Vector3(16f, 0f, 28f),
                             new Vector3(22f, 0f, 16f), new Vector3(-14f, 0f, -2f), new Vector3(16f, 0f, -6f), new Vector3(-31f, 0f, 30f) };
            m.enemySpawns.AddRange(es);
            Vector3[] ps = { new Vector3(-24f, 0f, -14f), new Vector3(-24f, 0f, 0f), new Vector3(-4f, 0f, -8f), new Vector3(4f, 0f, 0f),
                             new Vector3(20f, 0f, -14f), new Vector3(29.5f, 4f, 0f), new Vector3(-8f, 0f, 14f), new Vector3(20f, 0f, 8f),
                             new Vector3(0f, 0f, 24f), new Vector3(14f, 2.9f, 13f) };
            m.pickupSpawns.AddRange(ps);
            return m;
        }

        static void Container(float x, float y, float z, bool alongX, Color c)
        {
            float L = 12f, W = 2.6f, H = 2.9f;
            float w = alongX ? L : W, d = alongX ? W : L;
            Box(x, y, z, w, H, d, c, true, 0.25f, 0.3f);
            Color rib = Gfx.Dk(c, 0.78f);
            for (int i = -4; i <= 4; i++)
            {
                float o = i * 1.3f;
                if (alongX)
                {
                    Box(x + o, y + 0.1f, z + W * 0.5f + 0.03f, 0.18f, H - 0.2f, 0.06f, rib, false);
                    Box(x + o, y + 0.1f, z - W * 0.5f - 0.03f, 0.18f, H - 0.2f, 0.06f, rib, false);
                }
                else
                {
                    Box(x + W * 0.5f + 0.03f, y + 0.1f, z + o, 0.06f, H - 0.2f, 0.18f, rib, false);
                    Box(x - W * 0.5f - 0.03f, y + 0.1f, z + o, 0.06f, H - 0.2f, 0.18f, rib, false);
                }
            }
        }

        // ================= MAP 2: KOTA GURUN =================
        static MapInfo Desert()
        {
            var m = new MapInfo();
            m.name = "KOTA GURUN";
            m.sky = new Color(0.55f, 0.74f, 0.95f);
            m.playerSpawn = new Vector3(0f, 0.05f, -40f);
            m.playerYaw = 0f;
            m.center = Vector3.zero;
            m.orbitRadius = 36f;
            m.orbitHeight = 17f;
            Env(new Color(0.58f, 0.55f, 0.5f), true, new Color(0.86f, 0.78f, 0.64f), 0.006f);
            Sun(new Vector3(50f, -35f, 0f), new Color(1f, 0.93f, 0.8f), 1.15f);

            Color sand = new Color(0.8f, 0.68f, 0.46f), asphalt = new Color(0.22f, 0.22f, 0.24f), wallC = new Color(0.72f, 0.6f, 0.42f);
            Box(0f, -1f, 0f, 98f, 1f, 98f, sand, true, 0.05f);
            Box(0f, 0f, 0f, 7f, 0.03f, 96f, asphalt, false, 0.2f);
            Box(0f, 0f, 0f, 96f, 0.04f, 7f, asphalt, false, 0.2f);
            for (int i = -22; i <= 22; i++)
            {
                if (Mathf.Abs(i) < 2) continue;
                Box(0f, 0.05f, i * 2f, 0.15f, 0.01f, 1f, new Color(0.9f, 0.8f, 0.2f), false);
                Box(i * 2f, 0.06f, 0f, 1f, 0.01f, 0.15f, new Color(0.9f, 0.8f, 0.2f), false);
            }

            // tembok keliling + pegunungan latar
            Box(0f, 0f, 48f, 100f, 6.5f, 2f, wallC); Box(0f, 0f, -48f, 100f, 6.5f, 2f, wallC);
            Box(48f, 0f, 0f, 2f, 6.5f, 100f, wallC); Box(-48f, 0f, 0f, 2f, 6.5f, 100f, wallC);
            for (int i = -23; i <= 23; i += 2)
            {
                Box(i * 2f, 6.5f, 48f, 1.6f, 0.8f, 2.2f, wallC, false);
                Box(i * 2f, 6.5f, -48f, 1.6f, 0.8f, 2.2f, wallC, false);
                Box(48f, 6.5f, i * 2f, 2.2f, 0.8f, 1.6f, wallC, false);
                Box(-48f, 6.5f, i * 2f, 2.2f, 0.8f, 1.6f, wallC, false);
            }
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f * Mathf.Deg2Rad;
                Gfx.Sph(R, new Vector3(Mathf.Cos(a) * 95f, -12f, Mathf.Sin(a) * 95f), Vector3.one * Rr(45f, 65f), new Color(0.76f, 0.62f, 0.42f), false, 0.05f);
            }

            // bangunan
            Building(-26f, -20f, 14f, 10f, 3.4f, "NE", new Color(0.78f, 0.66f, 0.45f), 'W');
            Building(24f, -22f, 12f, 12f, 3.2f, "NW", new Color(0.72f, 0.55f, 0.4f), 'E');
            Building(-28f, 18f, 12f, 14f, 3.6f, "SE", new Color(0.75f, 0.62f, 0.46f), 'W');
            Building(28f, 20f, 14f, 10f, 3.4f, "WN", new Color(0.8f, 0.7f, 0.5f), ' ');
            Building(0f, 36f, 22f, 8f, 4f, "S", new Color(0.7f, 0.58f, 0.4f), ' ');
            Building(-14f, -33f, 8f, 6f, 3f, "NS", new Color(0.76f, 0.64f, 0.46f), ' ');
            Building(14f, -6f, 5f, 5f, 3f, "W", new Color(0.7f, 0.5f, 0.38f), ' ');
            Building(-14f, 6f, 6f, 6f, 3f, "SE", new Color(0.74f, 0.6f, 0.44f), ' ');

            // air mancur
            Gfx.Cyl(R, new Vector3(0f, 0.3f, 0f), 3.2f, 0.6f, new Color(0.7f, 0.66f, 0.58f), true, 0.2f);
            Gfx.Cyl(R, new Vector3(0f, 0.31f, 0f), 2.7f, 0.62f, new Color(0.2f, 0.5f, 0.7f), false, 0.8f, 0f, 0.3f);
            Gfx.Cyl(R, new Vector3(0f, 1.2f, 0f), 0.4f, 2.4f, new Color(0.7f, 0.66f, 0.58f), true, 0.2f);
            Gfx.Cyl(R, new Vector3(0f, 2.4f, 0f), 0.9f, 0.2f, new Color(0.7f, 0.66f, 0.58f), false, 0.2f);

            // lapak
            Stall(8f, 9f, 0f, new Color(0.8f, 0.2f, 0.2f));
            Stall(-8f, -9f, 90f, new Color(0.2f, 0.5f, 0.8f));
            Stall(-9f, 11f, 20f, new Color(0.9f, 0.7f, 0.1f));
            Stall(9f, -10f, -30f, new Color(0.3f, 0.6f, 0.3f));

            // mobil
            Car(-6f, -8f, 20f, new Color(0.55f, 0.1f, 0.1f));
            Car(7f, 12f, -35f, new Color(0.2f, 0.3f, 0.5f));
            Car(-18f, -4f, 95f, new Color(0.7f, 0.65f, 0.4f));
            Car(22f, -8f, 0f, new Color(0.15f, 0.15f, 0.17f));
            Car(4f, -24f, 10f, new Color(0.6f, 0.6f, 0.62f));

            // pohon palem
            float[,] palms = { { -20, -6 }, { -20, 6 }, { 18, 4 }, { 18, -3 }, { -32, -8 }, { 33, -4 }, { 35, 10 }, { -36, 10 }, { 10, 22 }, { -10, 24 }, { -38, -30 }, { 38, -30 } };
            for (int i = 0; i < palms.GetLength(0); i++) Palm(palms[i, 0], palms[i, 1], Rr(5.5f, 8f));

            // karung pasir
            Sandbags(0f, -36f, 7f, 0f); Sandbags(-5f, -37f, 3f, 90f); Sandbags(5f, -37f, 3f, 90f);
            Sandbags(-16f, -14f, 4f, 0f); Sandbags(16f, 12f, 4f, 70f); Sandbags(-6f, 14f, 4f, 100f);
            Sandbags(32f, 6f, 5f, 0f); Sandbags(-32f, 4f, 5f, 10f); Sandbags(10f, -18f, 5f, -15f);

            // batu
            float[,] rocks = { { -40, -10 }, { 41, 12 }, { -38, 32 }, { 38, -34 }, { -24, 40 }, { 30, 40 }, { 0, -44 }, { 42, -12 } };
            for (int i = 0; i < rocks.GetLength(0); i++)
            {
                var r = Gfx.Prim(PrimitiveType.Sphere, R, new Vector3(rocks[i, 0], 0.6f, rocks[i, 1]), new Vector3(Rr(2f, 3.2f), Rr(1.5f, 2.6f), Rr(2f, 3.2f)),
                    new Color(0.58f, 0.5f, 0.4f), new Vector3(0f, Rr(0f, 180f), 0f), true, 0.05f);
            }

            // menara air
            foreach (float tx in new float[] { -1.5f, 1.5f })
                foreach (float tz in new float[] { -1.5f, 1.5f })
                    Gfx.Cyl(R, new Vector3(40f + tx, 4f, -38f + tz), 0.2f, 8f, new Color(0.35f, 0.3f, 0.28f), true, 0.2f);
            Gfx.Cyl(R, new Vector3(40f, 9.5f, -38f), 2.4f, 3f, new Color(0.55f, 0.35f, 0.25f), true, 0.2f);
            Gfx.Sph(R, new Vector3(40f, 11.2f, -38f), new Vector3(4.8f, 1.6f, 4.8f), new Color(0.45f, 0.3f, 0.22f), false);

            Vector3[] es = { new Vector3(-36f, 0f, 36f), new Vector3(-18f, 0f, 28f), new Vector3(18f, 0f, 28f), new Vector3(36f, 0f, 36f),
                             new Vector3(-40f, 0f, 2f), new Vector3(40f, 0f, 0f), new Vector3(-12f, 0f, -4f), new Vector3(30f, 0f, -4f),
                             new Vector3(-30f, 0f, -4f), new Vector3(6f, 0f, 22f), new Vector3(-6f, 0f, 22f) };
            m.enemySpawns.AddRange(es);
            Vector3[] ps = { new Vector3(-4f, 0f, -26f), new Vector3(6f, 0f, -16f), new Vector3(-22f, 0f, -4f), new Vector3(22f, 0f, 2f),
                             new Vector3(0f, 0f, 10f), new Vector3(-16f, 0f, 24f), new Vector3(16f, 0f, 22f), new Vector3(34f, 0f, -8f),
                             new Vector3(-26f, 3.8f, -20f), new Vector3(24f, 3.6f, -22f) };
            m.pickupSpawns.AddRange(ps);
            return m;
        }

        // sisi: N=+z, S=-z, E=+x, W=-x
        static void Building(float cx, float cz, float w, float d, float h, string doors, Color col, char rampSide)
        {
            float t = 0.5f, door = 2.6f, dh = 2.6f;
            Color roofC = Gfx.Dk(col, 0.85f);
            WallX(cx, cz + d * 0.5f, w, h, t, doors.Contains("N"), col, door, dh);
            WallX(cx, cz - d * 0.5f, w, h, t, doors.Contains("S"), col, door, dh);
            WallZ(cz, cx + w * 0.5f, d, h, t, doors.Contains("E"), col, door, dh);
            WallZ(cz, cx - w * 0.5f, d, h, t, doors.Contains("W"), col, door, dh);
            Box(cx, 0f, cz, w - 1f, 0.03f, d - 1f, new Color(0.5f, 0.44f, 0.36f), false);
            Box(cx, h, cz, w + 0.4f, 0.4f, d + 0.4f, roofC, true);
            float ry = h + 0.4f;
            Color par = Gfx.Dk(col, 0.95f);
            if (rampSide != 'N') Box(cx, ry, cz + d * 0.5f + 0.1f, w + 0.4f, 0.8f, 0.3f, par);
            if (rampSide != 'S') Box(cx, ry, cz - d * 0.5f - 0.1f, w + 0.4f, 0.8f, 0.3f, par);
            if (rampSide != 'E') Box(cx + w * 0.5f + 0.1f, ry, cz, 0.3f, 0.8f, d + 0.4f, par);
            if (rampSide != 'W') Box(cx - w * 0.5f - 0.1f, ry, cz, 0.3f, 0.8f, d + 0.4f, par);
            // jendela berlapis kayu
            Color shut = new Color(0.35f, 0.22f, 0.12f);
            for (int i = -1; i <= 1; i += 2)
            {
                if (!doors.Contains("N")) Box(cx + i * w * 0.25f, 1.3f, cz + d * 0.5f + 0.27f, 1.2f, 1f, 0.05f, shut, false);
                if (!doors.Contains("S")) Box(cx + i * w * 0.25f, 1.3f, cz - d * 0.5f - 0.27f, 1.2f, 1f, 0.05f, shut, false);
                if (!doors.Contains("E")) Box(cx + w * 0.5f + 0.27f, 1.3f, cz + i * d * 0.25f, 0.05f, 1f, 1.2f, shut, false);
                if (!doors.Contains("W")) Box(cx - w * 0.5f - 0.27f, 1.3f, cz + i * d * 0.25f, 0.05f, 1f, 1.2f, shut, false);
            }
            Vector3 top, run;
            float len = ry / Mathf.Tan(24f * Mathf.Deg2Rad);
            switch (rampSide)
            {
                case 'E': top = new Vector3(cx + w * 0.5f + 0.2f, ry, cz); run = Vector3.right; break;
                case 'W': top = new Vector3(cx - w * 0.5f - 0.2f, ry, cz); run = Vector3.left; break;
                case 'N': top = new Vector3(cx, ry, cz + d * 0.5f + 0.2f); run = Vector3.forward; break;
                case 'S': top = new Vector3(cx, ry, cz - d * 0.5f - 0.2f); run = Vector3.back; break;
                default: return;
            }
            Vector3 bottom = top + run * len;
            bottom.y = 0f;
            Ramp(bottom, top, 2.2f, new Color(0.6f, 0.52f, 0.4f));
        }

        static void WallX(float cx, float z, float len, float h, float t, bool hasDoor, Color c, float door, float dh)
        {
            if (!hasDoor) { Box(cx, 0f, z, len, h, t, c); return; }
            float seg = (len - door) * 0.5f;
            Box(cx - door * 0.5f - seg * 0.5f, 0f, z, seg, h, t, c);
            Box(cx + door * 0.5f + seg * 0.5f, 0f, z, seg, h, t, c);
            Box(cx, dh, z, door, h - dh, t, c);
        }

        static void WallZ(float cz, float x, float len, float h, float t, bool hasDoor, Color c, float door, float dh)
        {
            if (!hasDoor) { Box(x, 0f, cz, t, h, len, c); return; }
            float seg = (len - door) * 0.5f;
            Box(x, 0f, cz - door * 0.5f - seg * 0.5f, t, h, seg, c);
            Box(x, 0f, cz + door * 0.5f + seg * 0.5f, t, h, seg, c);
            Box(x, dh, cz, t, h - dh, door, c);
        }

        static void Stall(float x, float z, float rot, Color awn)
        {
            var g = Group("Stall", x, 0f, z, rot);
            var prev = R; R = g;
            foreach (float px in new float[] { -1.2f, 1.2f })
                foreach (float pz in new float[] { -0.8f, 0.8f })
                    Gfx.Cyl(R, new Vector3(px, 1.2f, pz), 0.06f, 2.4f, new Color(0.4f, 0.28f, 0.16f), false, 0.2f);
            Box(0f, 0f, 0f, 2.2f, 1f, 0.9f, new Color(0.55f, 0.4f, 0.25f), true, 0.2f);
            var a = Box(0f, 2.4f, 0f, 3f, 0.08f, 2.2f, awn, false);
            a.transform.localEulerAngles = new Vector3(8f, 0f, 0f);
            Color[] fr = { new Color(0.9f, 0.2f, 0.1f), new Color(0.95f, 0.6f, 0.1f), new Color(0.3f, 0.7f, 0.2f) };
            for (int i = 0; i < 3; i++) Box(-0.7f + i * 0.7f, 1f, 0f, 0.4f, 0.25f, 0.4f, fr[i], false);
            R = prev;
        }

        static void Car(float x, float z, float rot, Color c)
        {
            var g = Group("Car", x, 0f, z, rot);
            var prev = R; R = g;
            Box(0f, 0.25f, 0f, 1.9f, 0.7f, 4.2f, c, true, 0.5f, 0.3f);
            Box(0f, 0.95f, -0.2f, 1.7f, 0.65f, 2.1f, new Color(0.12f, 0.16f, 0.2f), true, 0.9f);
            Box(0f, 1.6f, -0.2f, 1.75f, 0.05f, 2.2f, Gfx.Dk(c, 0.9f), false, 0.5f, 0.3f);
            foreach (float wz in new float[] { -1.4f, 1.4f })
                foreach (float wx in new float[] { -0.95f, 0.95f })
                    Gfx.Prim(PrimitiveType.Cylinder, R, new Vector3(wx, 0.38f, wz), new Vector3(0.76f, 0.13f, 0.76f), new Color(0.05f, 0.05f, 0.05f), new Vector3(0f, 0f, 90f), false);
            Box(-0.6f, 0.55f, 2.11f, 0.4f, 0.15f, 0.05f, new Color(1f, 0.95f, 0.7f), false, 0.3f, 0f, 1f);
            Box(0.6f, 0.55f, 2.11f, 0.4f, 0.15f, 0.05f, new Color(1f, 0.95f, 0.7f), false, 0.3f, 0f, 1f);
            R = prev;
        }

        static void Palm(float x, float z, float h)
        {
            var tr = Gfx.Prim(PrimitiveType.Cylinder, R, new Vector3(x, h * 0.5f, z), new Vector3(0.45f, h * 0.5f, 0.45f),
                new Color(0.4f, 0.3f, 0.18f), new Vector3(Rr(-5f, 5f), 0f, Rr(-5f, 5f)), true, 0.1f);
            for (int i = 0; i < 7; i++)
            {
                var g = Group("Leaf", x, h, z, i * 51f);
                Gfx.Prim(PrimitiveType.Cube, g, new Vector3(0f, 0f, 1.5f), new Vector3(0.7f, 0.06f, 3.2f), new Color(0.2f, 0.5f, 0.2f), new Vector3(22f, 0f, 0f), false, 0.2f);
            }
        }

        static void Sandbags(float x, float z, float len, float rot)
        {
            var g = Group("Sandbags", x, 0f, z, rot);
            var prev = R; R = g;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 1.1f));
            for (int layer = 0; layer < 2; layer++)
                for (int i = 0; i < n; i++)
                {
                    float off = layer == 1 ? 0.55f : 0f;
                    Box(i * 1.1f - len * 0.5f + off, layer * 0.45f, 0f, 1.1f, 0.45f, 0.6f, new Color(0.7f, 0.62f, 0.45f), true, 0.05f);
                }
            R = prev;
        }
    }
}
