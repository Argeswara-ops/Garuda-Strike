using UnityEngine;

namespace GarudaStrike
{
    public class WeaponDef
    {
        public string name, sound;
        public float damage, rate, reload, hipSpread, adsSpread, kick, adsFov;
        public float range = 300f, fallStart = 30f, fallEnd = 100f, minMul = 0.6f, moveMul = 1f;
        public int mag, reserve, pellets = 1;
        public bool auto, scope;
        public Vector3 adsPos;
    }

    public static class Weapons
    {
        public static readonly WeaponDef[] All =
        {
            new WeaponDef { name = "PISTOL P9", sound = "pistol", damage = 26f, rate = 6.5f, mag = 15, reserve = 90, reload = 1.35f,
                hipSpread = 1.2f, adsSpread = 0.35f, kick = 1.6f, adsFov = 66f, fallStart = 20f, fallEnd = 70f, minMul = 0.55f,
                auto = false, moveMul = 1.05f, adsPos = new Vector3(0f, -0.085f, 0.3f) },
            new WeaponDef { name = "SENAPAN SERBU AR-7", sound = "rifle", damage = 21f, rate = 10f, mag = 30, reserve = 180, reload = 2.0f,
                hipSpread = 2.2f, adsSpread = 0.5f, kick = 1.1f, adsFov = 58f, fallStart = 35f, fallEnd = 110f, minMul = 0.6f,
                auto = true, adsPos = new Vector3(0f, -0.1f, 0.32f) },
            new WeaponDef { name = "SHOTGUN S-12", sound = "shotgun", damage = 12f, rate = 1.15f, mag = 8, reserve = 40, reload = 2.6f,
                hipSpread = 4.5f, adsSpread = 3.2f, kick = 7f, adsFov = 70f, fallStart = 6f, fallEnd = 28f, minMul = 0.2f, pellets = 8,
                auto = false, moveMul = 0.95f, adsPos = new Vector3(0f, -0.075f, 0.3f) },
            new WeaponDef { name = "SNIPER SR-50", sound = "sniper", damage = 130f, rate = 0.85f, mag = 5, reserve = 30, reload = 3.0f,
                hipSpread = 6f, adsSpread = 0.05f, kick = 6f, adsFov = 16f, fallStart = 999f, fallEnd = 1000f, minMul = 1f,
                auto = false, scope = true, moveMul = 0.85f, adsPos = new Vector3(0f, -0.08f, 0.3f) },
        };

        static readonly Color Dark = new Color(0.09f, 0.09f, 0.11f);
        static readonly Color Steel = new Color(0.26f, 0.27f, 0.31f);
        static readonly Color Wood = new Color(0.38f, 0.22f, 0.10f);
        static readonly Color Glove = new Color(0.11f, 0.12f, 0.11f);
        static readonly Color Sleeve = new Color(0.22f, 0.30f, 0.20f);

        static GameObject P(Transform r, Vector3 pos, Vector3 s, Color c, float smooth = 0.5f, float metal = 0.5f, float emit = 0f)
        { return Gfx.Prim(PrimitiveType.Cube, r, pos, s, c, Vector3.zero, false, smooth, metal, emit); }

        static void Hand(Transform r, Vector3 pos)
        { Gfx.Prim(PrimitiveType.Cube, r, pos, new Vector3(0.075f, 0.075f, 0.1f), Glove, Vector3.zero, false, 0.2f); }

        static void Forearm(Transform r, Vector3 hand, Vector3 back, float len)
        {
            Vector3 dir = back.normalized;
            Gfx.Prim(PrimitiveType.Cube, r, hand + dir * (len * 0.5f), new Vector3(0.09f, 0.09f, len), Sleeve,
                Quaternion.LookRotation(dir).eulerAngles, false, 0.1f);
        }

        public static Transform Build(int idx, Transform parent, out Transform muzzle)
        {
            var root = new GameObject("Weapon" + idx).transform;
            root.SetParent(parent, false);
            muzzle = new GameObject("muzzle").transform;
            muzzle.SetParent(root, false);
            switch (idx)
            {
                case 0: Pistol(root); muzzle.localPosition = new Vector3(0f, 0.035f, 0.3f); break;
                case 1: Rifle(root); muzzle.localPosition = new Vector3(0f, 0.012f, 0.86f); break;
                case 2: Shotgun(root); muzzle.localPosition = new Vector3(0f, 0.02f, 0.88f); break;
                default: Sniper(root); muzzle.localPosition = new Vector3(0f, 0.01f, 1.0f); break;
            }
            var f = Gfx.Sph(muzzle, Vector3.zero, Vector3.one * 0.12f, new Color(1f, 0.85f, 0.35f), false, 0.1f, 0f, 5f);
            f.name = "flash";
            Gfx.PointLight(f.transform, Vector3.zero, new Color(1f, 0.8f, 0.4f), 8f, 2.5f);
            f.SetActive(false);
            return root;
        }

        static void Pistol(Transform r)
        {
            P(r, new Vector3(0f, 0.035f, 0.1f), new Vector3(0.05f, 0.055f, 0.3f), Steel, 0.6f, 0.7f);
            P(r, new Vector3(0f, -0.005f, 0.08f), new Vector3(0.045f, 0.04f, 0.24f), Dark);
            var g = P(r, new Vector3(0f, -0.085f, -0.01f), new Vector3(0.042f, 0.13f, 0.07f), Dark, 0.2f, 0f);
            g.transform.localEulerAngles = new Vector3(12f, 0f, 0f);
            Gfx.Tube(r, new Vector3(0f, 0.035f, 0.27f), 0.014f, 0.05f, Dark);
            P(r, new Vector3(0f, 0.068f, 0.22f), new Vector3(0.012f, 0.014f, 0.014f), new Color(1f, 1f, 0.8f), 0.1f, 0f, 1.5f);
            P(r, new Vector3(0f, 0.068f, -0.03f), new Vector3(0.025f, 0.014f, 0.014f), Dark);
            Hand(r, new Vector3(0.0f, -0.1f, -0.02f));
            Forearm(r, new Vector3(0.0f, -0.12f, -0.03f), new Vector3(0.45f, -0.45f, -1f), 0.4f);
            Hand(r, new Vector3(-0.045f, -0.105f, 0.02f));
            Forearm(r, new Vector3(-0.05f, -0.125f, 0.0f), new Vector3(-0.7f, -0.5f, -1f), 0.4f);
        }

        static void Rifle(Transform r)
        {
            P(r, new Vector3(0f, 0f, 0.1f), new Vector3(0.06f, 0.085f, 0.4f), Dark);
            P(r, new Vector3(0f, -0.005f, 0.43f), new Vector3(0.07f, 0.075f, 0.26f), Steel);
            Gfx.Tube(r, new Vector3(0f, 0.012f, 0.68f), 0.012f, 0.24f, Steel);
            Gfx.Tube(r, new Vector3(0f, 0.012f, 0.82f), 0.019f, 0.06f, Dark);
            P(r, new Vector3(0f, -0.035f, -0.2f), new Vector3(0.05f, 0.09f, 0.2f), Wood, 0.3f, 0f);
            var mg = P(r, new Vector3(0f, -0.12f, 0.15f), new Vector3(0.04f, 0.15f, 0.07f), Dark, 0.3f, 0.2f);
            mg.transform.localEulerAngles = new Vector3(-10f, 0f, 0f);
            var gr = P(r, new Vector3(0f, -0.1f, -0.03f), new Vector3(0.04f, 0.11f, 0.05f), Dark, 0.2f, 0f);
            gr.transform.localEulerAngles = new Vector3(15f, 0f, 0f);
            P(r, new Vector3(0f, 0.065f, 0.12f), new Vector3(0.04f, 0.04f, 0.09f), Dark);
            P(r, new Vector3(0f, 0.076f, 0.075f), new Vector3(0.012f, 0.012f, 0.006f), new Color(1f, 0.1f, 0.1f), 0.1f, 0f, 3f);
            P(r, new Vector3(0f, 0.06f, 0.62f), new Vector3(0.01f, 0.04f, 0.01f), Dark);
            Hand(r, new Vector3(0f, -0.125f, -0.02f));
            Forearm(r, new Vector3(0f, -0.14f, -0.04f), new Vector3(0.4f, -0.45f, -1f), 0.4f);
            Hand(r, new Vector3(0f, -0.07f, 0.4f));
            Forearm(r, new Vector3(0f, -0.085f, 0.38f), new Vector3(-0.8f, -0.4f, -1f), 0.45f);
        }

        static void Shotgun(Transform r)
        {
            P(r, new Vector3(0f, 0f, 0.05f), new Vector3(0.06f, 0.085f, 0.3f), Dark);
            Gfx.Tube(r, new Vector3(0f, 0.02f, 0.56f), 0.02f, 0.6f, Steel);
            Gfx.Tube(r, new Vector3(0f, -0.03f, 0.5f), 0.017f, 0.5f, Dark);
            P(r, new Vector3(0f, -0.045f, 0.45f), new Vector3(0.07f, 0.06f, 0.2f), Wood, 0.3f, 0f);
            var st = P(r, new Vector3(0f, -0.04f, -0.25f), new Vector3(0.05f, 0.1f, 0.3f), Wood, 0.3f, 0f);
            st.transform.localEulerAngles = new Vector3(-5f, 0f, 0f);
            P(r, new Vector3(0f, 0.052f, 0.84f), new Vector3(0.01f, 0.02f, 0.01f), new Color(1f, 0.8f, 0.2f), 0.2f, 0f, 1f);
            var gr = P(r, new Vector3(0f, -0.09f, -0.04f), new Vector3(0.04f, 0.1f, 0.05f), Wood, 0.3f, 0f);
            gr.transform.localEulerAngles = new Vector3(15f, 0f, 0f);
            Hand(r, new Vector3(0f, -0.1f, -0.03f));
            Forearm(r, new Vector3(0f, -0.115f, -0.05f), new Vector3(0.4f, -0.45f, -1f), 0.4f);
            Hand(r, new Vector3(0f, -0.08f, 0.45f));
            Forearm(r, new Vector3(0f, -0.095f, 0.43f), new Vector3(-0.8f, -0.4f, -1f), 0.45f);
        }

        static void Sniper(Transform r)
        {
            P(r, new Vector3(0f, 0f, 0.05f), new Vector3(0.055f, 0.07f, 0.45f), Dark);
            Gfx.Tube(r, new Vector3(0f, 0.01f, 0.7f), 0.014f, 0.5f, Steel);
            Gfx.Tube(r, new Vector3(0f, 0.01f, 0.97f), 0.02f, 0.07f, Dark);
            Gfx.Tube(r, new Vector3(0f, 0.075f, 0.08f), 0.03f, 0.3f, Dark);
            Gfx.Tube(r, new Vector3(0f, 0.075f, -0.07f), 0.037f, 0.04f, Dark);
            Gfx.Tube(r, new Vector3(0f, 0.075f, 0.23f), 0.037f, 0.04f, Dark);
            Gfx.Tube(r, new Vector3(0f, 0.075f, -0.092f), 0.026f, 0.005f, new Color(0.2f, 0.5f, 1f), false, 0.9f, 0f, 1.5f);
            var st = P(r, new Vector3(0f, -0.04f, -0.28f), new Vector3(0.05f, 0.11f, 0.35f), Wood, 0.3f, 0f);
            st.transform.localEulerAngles = new Vector3(-4f, 0f, 0f);
            P(r, new Vector3(0f, -0.08f, 0.08f), new Vector3(0.04f, 0.07f, 0.06f), Dark);
            var b1 = P(r, new Vector3(0.03f, -0.07f, 0.65f), new Vector3(0.01f, 0.12f, 0.01f), Steel);
            b1.transform.localEulerAngles = new Vector3(10f, 0f, 8f);
            var b2 = P(r, new Vector3(-0.03f, -0.07f, 0.65f), new Vector3(0.01f, 0.12f, 0.01f), Steel);
            b2.transform.localEulerAngles = new Vector3(10f, 0f, -8f);
            Hand(r, new Vector3(0f, -0.1f, -0.06f));
            Forearm(r, new Vector3(0f, -0.115f, -0.08f), new Vector3(0.4f, -0.45f, -1f), 0.4f);
            Hand(r, new Vector3(0f, -0.065f, 0.4f));
            Forearm(r, new Vector3(0f, -0.08f, 0.38f), new Vector3(-0.8f, -0.4f, -1f), 0.45f);
        }

        public static Transform BuildKnife(Transform parent)
        {
            var r = new GameObject("Knife").transform;
            r.SetParent(parent, false);
            P(r, new Vector3(0f, 0f, 0.2f), new Vector3(0.012f, 0.05f, 0.26f), new Color(0.82f, 0.84f, 0.88f), 0.85f, 0.9f);
            P(r, new Vector3(0f, 0f, 0.06f), new Vector3(0.05f, 0.08f, 0.02f), Dark);
            P(r, new Vector3(0f, 0f, -0.03f), new Vector3(0.032f, 0.045f, 0.14f), Wood, 0.3f, 0f);
            Hand(r, new Vector3(0f, 0f, -0.03f));
            Forearm(r, new Vector3(0f, 0f, -0.05f), new Vector3(0.3f, -0.5f, -1f), 0.4f);
            r.gameObject.SetActive(false);
            return r;
        }
    }
}
