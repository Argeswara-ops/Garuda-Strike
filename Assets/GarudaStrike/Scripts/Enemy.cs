using System.Collections.Generic;
using UnityEngine;

namespace GarudaStrike
{
    public enum EType { Grunt, Heavy, Runner, Boss }
    public enum PType { Health, Ammo, Armor, Grenade }

    public class Hitbox : MonoBehaviour
    {
        public Enemy owner;
        public float mult = 1f;
    }

    public class Enemy : MonoBehaviour
    {
        public static readonly List<Enemy> All = new List<Enemy>();
        static readonly float[] Angs = { 0f, 35f, -35f, 70f, -70f, 110f, -110f };

        public EType type;
        public float hp, maxHp, speed, damage, fireInterval, range, preferred, spread;
        public int score;
        public bool dead;
        public float hpBarT;

        CharacterController cc;
        Transform legL, legR, armL, armR, muzzle;
        Player P;
        float vy, animT, nextShot, strafeT, strafeDir = 1f, losTimer, steerT, steerAngle;
        bool los;
        Vector3 lastPos, stuckPos, unstickDir;
        float stuckCheck, unstick, deadT, deathYaw, meleeCd;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static int Alive()
        {
            int n = 0;
            for (int i = 0; i < All.Count; i++) if (!All[i].dead) n++;
            return n;
        }

        public static Enemy Spawn(EType t, Vector3 pos, float hpMul, float dmgMul, float spreadMul, Transform parent)
        {
            var g = new GameObject(t + "_Enemy");
            g.transform.SetParent(parent, false);
            g.transform.position = pos + Vector3.up * 0.05f;
            g.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var e = g.AddComponent<Enemy>();
            e.Build(t, hpMul, dmgMul, spreadMul);
            return e;
        }

        Transform Pivot(string n, Vector3 pos)
        {
            var t = new GameObject(n).transform;
            t.SetParent(transform, false);
            t.localPosition = pos;
            return t;
        }

        void SetHit(GameObject go, float mult)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
            var h = go.AddComponent<Hitbox>();
            h.owner = this;
            h.mult = mult;
        }

        void Build(EType t, float hpMul, float dmgMul, float spreadMul)
        {
            type = t;
            P = GameManager.I.player;
            float s;
            Color uni, vest, helmet, pants, accent;
            Color skin = new Color(0.78f, 0.58f, 0.43f);
            switch (t)
            {
                case EType.Heavy:
                    s = 1.2f; hp = 190f; speed = 2.3f; damage = 8f; fireInterval = 0.3f; range = 45f; preferred = 13f; spread = 5f; score = 250;
                    uni = new Color(0.2f, 0.22f, 0.26f); vest = new Color(0.35f, 0.12f, 0.1f); helmet = new Color(0.08f, 0.08f, 0.1f);
                    pants = new Color(0.15f, 0.16f, 0.2f); accent = new Color(1f, 0.4f, 0.1f);
                    break;
                case EType.Runner:
                    s = 0.95f; hp = 50f; speed = 6.4f; damage = 14f; fireInterval = 0.9f; range = 2f; preferred = 0f; spread = 0f; score = 150;
                    uni = new Color(0.5f, 0.1f, 0.1f); vest = new Color(0.12f, 0.1f, 0.1f); helmet = new Color(0.6f, 0.1f, 0.1f);
                    pants = new Color(0.12f, 0.12f, 0.14f); accent = new Color(1f, 0.8f, 0.2f);
                    break;
                case EType.Boss:
                    s = 1.55f; hp = 950f; speed = 2.6f; damage = 11f; fireInterval = 0.26f; range = 65f; preferred = 17f; spread = 2.5f; score = 2000;
                    uni = new Color(0.45f, 0.08f, 0.08f); vest = new Color(0.65f, 0.5f, 0.1f); helmet = new Color(0.12f, 0.1f, 0.1f);
                    pants = new Color(0.15f, 0.1f, 0.1f); accent = new Color(1f, 0.15f, 0.1f);
                    break;
                default:
                    s = 1f; hp = 65f; speed = 3.4f; damage = 7f; fireInterval = 0.75f; range = 48f; preferred = 14f; spread = 4f; score = 100;
                    uni = new Color(0.27f, 0.33f, 0.21f); vest = new Color(0.4f, 0.36f, 0.24f); helmet = new Color(0.2f, 0.25f, 0.16f);
                    pants = new Color(0.22f, 0.26f, 0.18f); accent = new Color(0.9f, 0.8f, 0.3f);
                    break;
            }
            hp *= hpMul; maxHp = hp; damage *= dmgMul; spread *= spreadMul;
            transform.localScale = Vector3.one * s;
            cc = gameObject.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.32f; cc.center = new Vector3(0f, 0.9f, 0f); cc.stepOffset = 0.35f; cc.skinWidth = 0.04f;

            // kaki
            legL = Pivot("legL", new Vector3(-0.13f, 0.9f, 0f));
            legR = Pivot("legR", new Vector3(0.13f, 0.9f, 0f));
            Transform[] legs = { legL, legR };
            foreach (var lg in legs)
            {
                SetHit(Gfx.Cube(lg, new Vector3(0f, -0.4f, 0f), new Vector3(0.18f, 0.8f, 0.2f), pants), 0.7f);
                Gfx.Cube(lg, new Vector3(0f, -0.84f, 0.05f), new Vector3(0.2f, 0.12f, 0.3f), new Color(0.05f, 0.05f, 0.05f), false);
            }
            // badan
            SetHit(Gfx.Cube(transform, new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 0.6f, 0.28f), uni), 1f);
            Gfx.Cube(transform, new Vector3(0f, 1.22f, 0.03f), new Vector3(0.54f, 0.42f, 0.3f), vest, false);
            Gfx.Cube(transform, new Vector3(0f, 0.93f, 0f), new Vector3(0.52f, 0.08f, 0.3f), Gfx.Dk(accent, 0.5f), false);
            // kepala
            SetHit(Gfx.Sph(transform, new Vector3(0f, 1.68f, 0f), new Vector3(0.24f, 0.27f, 0.25f), skin), 2.5f);
            Gfx.Sph(transform, new Vector3(0f, 1.75f, -0.01f), new Vector3(0.29f, 0.2f, 0.3f), helmet, false);
            Gfx.Cube(transform, new Vector3(0f, 1.68f, 0.12f), new Vector3(0.2f, 0.05f, 0.05f), accent, false, 0.2f, 0f, 1.5f);
            if (t == EType.Heavy || t == EType.Boss)
            {
                Gfx.Cube(transform, new Vector3(-0.36f, 1.5f, 0f), new Vector3(0.2f, 0.12f, 0.3f), accent * 0.8f, false);
                Gfx.Cube(transform, new Vector3(0.36f, 1.5f, 0f), new Vector3(0.2f, 0.12f, 0.3f), accent * 0.8f, false);
            }
            if (t == EType.Boss) Gfx.Cube(transform, new Vector3(0f, 1.15f, -0.17f), new Vector3(0.5f, 0.8f, 0.04f), new Color(0.3f, 0.04f, 0.04f), false);

            // lengan
            armR = Pivot("armR", new Vector3(0.32f, 1.45f, 0f));
            armL = Pivot("armL", new Vector3(-0.32f, 1.45f, 0f));
            Transform[] arms = { armR, armL };
            foreach (var a in arms)
            {
                Gfx.Cube(a, new Vector3(0f, -0.25f, 0f), new Vector3(0.13f, 0.5f, 0.14f), uni, false);
                Gfx.Cube(a, new Vector3(0f, -0.54f, 0f), new Vector3(0.11f, 0.11f, 0.11f), skin, false);
            }
            if (t != EType.Runner)
            {
                armR.localRotation = Quaternion.Euler(-85f, -10f, 0f);
                armL.localRotation = Quaternion.Euler(-70f, 25f, 0f);
                Gfx.Cube(transform, new Vector3(0.13f, 1.38f, 0.38f), new Vector3(0.07f, 0.11f, 0.7f), new Color(0.08f, 0.08f, 0.1f), false, 0.4f, 0.5f);
                Gfx.Tube(transform, new Vector3(0.13f, 1.4f, 0.8f), 0.02f, 0.3f, new Color(0.3f, 0.3f, 0.34f));
                Gfx.Cube(transform, new Vector3(0.13f, 1.27f, 0.35f), new Vector3(0.05f, 0.16f, 0.07f), new Color(0.08f, 0.08f, 0.1f), false);
                muzzle = new GameObject("muzzle").transform;
                muzzle.SetParent(transform, false);
                muzzle.localPosition = new Vector3(0.13f, 1.4f, 0.98f);
            }
            else
            {
                Gfx.Cube(armR, new Vector3(0f, -0.6f, 0.2f), new Vector3(0.03f, 0.04f, 0.45f), new Color(0.85f, 0.86f, 0.9f), false, 0.8f, 0.9f);
            }
            nextShot = Time.time + Random.Range(1f, 2f);
            lastPos = transform.position;
            stuckPos = transform.position;
        }

        bool HasLos(Vector3 target)
        {
            Vector3 eye = transform.position + Vector3.up * 1.5f * transform.localScale.y;
            Vector3 d = target - eye;
            RaycastHit h;
            if (!Combat.Cast(eye, d.normalized, d.magnitude + 1f, transform, out h)) return false;
            return h.collider.GetComponentInParent<Player>() != null;
        }

        float FindAngle(Vector3 d)
        {
            Vector3 o = transform.position + Vector3.up * 0.7f;
            RaycastHit h;
            for (int i = 0; i < Angs.Length; i++)
            {
                Vector3 dd = Quaternion.Euler(0f, Angs[i], 0f) * d;
                if (!Physics.SphereCast(o, 0.3f, dd, out h, 1.8f, ~0, QueryTriggerInteraction.Ignore)) return Angs[i];
                if (h.collider.GetComponentInParent<Enemy>() != null || h.collider.GetComponentInParent<Player>() != null) return Angs[i];
            }
            return 150f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (dead) { DeadAnim(dt); return; }
            if (P == null) P = GameManager.I.player;
            if (P == null) return;
            if (hpBarT > 0f) hpBarT -= dt;

            bool pAlive = !P.dead;
            Vector3 pos = transform.position;
            Vector3 chest = P.transform.position + Vector3.up * 1.1f;
            Vector3 toP = P.transform.position - pos;
            toP.y = 0f;
            float dist = toP.magnitude;
            Vector3 dirP = dist > 0.01f ? toP / dist : transform.forward;

            losTimer -= dt;
            if (losTimer <= 0f) { losTimer = 0.15f; los = pAlive && HasLos(chest); }

            if (pAlive) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dirP), 8f * dt);

            Vector3 desired = Vector3.zero;
            float spd = speed;
            if (pAlive)
            {
                if (type == EType.Runner)
                {
                    desired = dist > 1.6f ? dirP : Vector3.zero;
                }
                else
                {
                    strafeT -= dt;
                    if (strafeT <= 0f) { strafeT = Random.Range(1.2f, 3f); strafeDir = Random.value < 0.5f ? -1f : 1f; }
                    Vector3 side = Vector3.Cross(Vector3.up, dirP) * strafeDir;
                    if (!los || dist > preferred + 4f) desired = dirP + side * 0.2f;
                    else if (dist < preferred - 5f) desired = -dirP * 0.7f + side * 0.6f;
                    else desired = side * 0.7f;
                    if (los && dist <= preferred + 4f) spd *= 0.7f;
                }
            }
            bool wantsMove = desired.sqrMagnitude > 0.01f;
            if (wantsMove)
            {
                desired.y = 0f;
                desired.Normalize();
                steerT -= dt;
                if (steerT <= 0f) { steerT = 0.12f; steerAngle = FindAngle(desired); }
                desired = Quaternion.Euler(0f, steerAngle, 0f) * desired;
            }
            stuckCheck -= dt;
            if (stuckCheck <= 0f)
            {
                stuckCheck = 0.6f;
                if (wantsMove && (transform.position - stuckPos).sqrMagnitude < 0.04f)
                {
                    unstick = 0.8f;
                    Vector3 pr = Vector3.Cross(Vector3.up, dirP) * (Random.value < 0.5f ? 1f : -1f);
                    unstickDir = (pr + Random.insideUnitSphere * 0.3f);
                    unstickDir.y = 0f;
                    unstickDir.Normalize();
                }
                stuckPos = transform.position;
            }
            if (unstick > 0f) { unstick -= dt; desired = unstickDir; }

            if (cc.isGrounded) vy = -2f; else vy -= 22f * dt;
            cc.Move((desired * spd + Vector3.up * vy) * dt);

            float sNow = (transform.position - lastPos).magnitude / dt;
            lastPos = transform.position;
            animT += dt * sNow * 2.2f;
            float sw = Mathf.Sin(animT) * Mathf.Clamp01(sNow / 2f) * 40f;
            legL.localRotation = Quaternion.Euler(sw, 0f, 0f);
            legR.localRotation = Quaternion.Euler(-sw, 0f, 0f);
            if (type == EType.Runner)
            {
                armR.localRotation = Quaternion.Euler(-sw * 1.5f - 20f, 0f, 0f);
                armL.localRotation = Quaternion.Euler(sw * 1.5f, 0f, 0f);
            }

            if (type == EType.Runner)
            {
                meleeCd -= dt;
                if (pAlive && dist < 1.9f && meleeCd <= 0f && Mathf.Abs(P.transform.position.y - pos.y) < 2f)
                {
                    meleeCd = fireInterval;
                    P.TakeDamage(damage, pos);
                    Sfx.Play("melee", pos, 1f);
                }
            }
            else if (pAlive && los && dist < range && Time.time >= nextShot)
            {
                Shoot(chest);
                nextShot = Time.time + fireInterval * Random.Range(0.8f, 1.4f);
            }
        }

        void Shoot(Vector3 target)
        {
            Vector3 o = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.4f;
            Vector3 dir = (target - o).normalized;
            float sp = spread * (1f + Vector3.Distance(o, target) / 35f);
            dir = Quaternion.Euler(Random.Range(-sp, sp), Random.Range(-sp, sp), 0f) * dir;
            RaycastHit h;
            Vector3 end = o + dir * range;
            if (Combat.Cast(o, dir, range, transform, out h))
            {
                end = h.point;
                var pl = h.collider.GetComponentInParent<Player>();
                if (pl != null) pl.TakeDamage(damage, transform.position);
                else Fx.Sparks(h.point, h.normal, new Color(1f, 0.7f, 0.3f), 3);
            }
            Fx.Tracer(o, end, new Color(1f, 0.35f, 0.15f), 0.03f, 0.06f);
            Fx.Burst(o, new Color(1f, 0.8f, 0.3f), 0.35f, 0.05f);
            Sfx.Play("enemy", o, 0.8f, Random.Range(0.9f, 1.1f));
        }

        /// <summary>Mengembalikan true bila musuh mati karena serangan ini.</summary>
        public bool Damage(float amt, bool head, Vector3 point, Vector3 normal)
        {
            if (dead) return false;
            hp -= amt;
            hpBarT = 3f;
            Fx.Sparks(point, normal, new Color(0.55f, 0.04f, 0.04f), head ? 8 : 5, 3f, 0f);
            if (hp <= 0f)
            {
                Die(head);
                return true;
            }
            return false;
        }

        void Die(bool head)
        {
            dead = true;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            deathYaw = transform.eulerAngles.y;
            Sfx.Play("hurt", transform.position, 0.7f, 1.2f);
            if (GameManager.I != null) GameManager.I.OnEnemyKilled(this, head);
        }

        void DeadAnim(float dt)
        {
            deadT += dt;
            float f = Mathf.Clamp01(deadT / 0.45f);
            f = f * f * (3f - 2f * f);
            transform.rotation = Quaternion.Euler(-88f * f, deathYaw, 0f);
            if (deadT > 4f) transform.position += Vector3.down * 0.4f * dt;
            if (deadT > 7f) Destroy(gameObject);
        }
    }

    public class Pickup : MonoBehaviour
    {
        public static readonly List<Pickup> All = new List<Pickup>();
        public PType type;
        public float life = -1f;
        Transform vis;
        float seed;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static Pickup Create(PType t, Vector3 pos, Transform parent, float lifetime = -1f)
        {
            var g = new GameObject("Pickup_" + t);
            g.transform.SetParent(parent, false);
            g.transform.position = pos + Vector3.up * 0.65f;
            var p = g.AddComponent<Pickup>();
            p.type = t;
            p.life = lifetime;
            p.seed = Random.value * 6f;
            p.BuildVisual();
            return p;
        }

        void BuildVisual()
        {
            vis = new GameObject("vis").transform;
            vis.SetParent(transform, false);
            Color ring;
            switch (type)
            {
                case PType.Health:
                    ring = new Color(1f, 0.25f, 0.25f);
                    Gfx.Cube(vis, Vector3.zero, new Vector3(0.42f, 0.3f, 0.3f), new Color(0.95f, 0.95f, 0.95f), false, 0.3f);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Gfx.Cube(vis, new Vector3(0f, 0f, 0.155f * s), new Vector3(0.22f, 0.07f, 0.02f), new Color(0.9f, 0.1f, 0.1f), false, 0.3f, 0f, 1f);
                        Gfx.Cube(vis, new Vector3(0f, 0f, 0.155f * s), new Vector3(0.07f, 0.22f, 0.02f), new Color(0.9f, 0.1f, 0.1f), false, 0.3f, 0f, 1f);
                    }
                    break;
                case PType.Ammo:
                    ring = new Color(1f, 0.85f, 0.2f);
                    Gfx.Cube(vis, Vector3.zero, new Vector3(0.46f, 0.26f, 0.3f), new Color(0.3f, 0.36f, 0.2f), false, 0.2f);
                    Gfx.Cube(vis, new Vector3(0f, 0.135f, 0f), new Vector3(0.48f, 0.02f, 0.08f), new Color(1f, 0.85f, 0.2f), false, 0.2f, 0f, 1f);
                    for (int i = -1; i <= 1; i++) Gfx.Cyl(vis, new Vector3(i * 0.1f, 0.2f, 0f), 0.035f, 0.14f, new Color(0.85f, 0.65f, 0.2f), false, 0.7f, 0.8f);
                    break;
                case PType.Armor:
                    ring = new Color(0.3f, 0.6f, 1f);
                    Gfx.Cube(vis, Vector3.zero, new Vector3(0.4f, 0.46f, 0.14f), new Color(0.15f, 0.25f, 0.45f), false, 0.4f, 0.3f);
                    Gfx.Cube(vis, new Vector3(0f, 0.04f, 0.08f), new Vector3(0.28f, 0.26f, 0.03f), new Color(0.5f, 0.6f, 0.75f), false, 0.6f, 0.6f, 0.6f);
                    break;
                default:
                    ring = new Color(0.4f, 1f, 0.4f);
                    Gfx.Sph(vis, new Vector3(-0.1f, 0f, 0f), Vector3.one * 0.2f, new Color(0.15f, 0.28f, 0.12f), false, 0.4f, 0.3f);
                    Gfx.Sph(vis, new Vector3(0.1f, 0f, 0f), Vector3.one * 0.2f, new Color(0.15f, 0.28f, 0.12f), false, 0.4f, 0.3f);
                    Gfx.Cyl(vis, new Vector3(-0.1f, 0.12f, 0f), 0.03f, 0.05f, new Color(0.6f, 0.6f, 0.6f), false, 0.7f, 0.8f);
                    Gfx.Cyl(vis, new Vector3(0.1f, 0.12f, 0f), 0.03f, 0.05f, new Color(0.6f, 0.6f, 0.6f), false, 0.7f, 0.8f);
                    break;
            }
            var rg = Gfx.Cyl(transform, new Vector3(0f, -0.62f, 0f), 0.55f, 0.02f, ring, false, 0.1f, 0f, 2f);
            Gfx.NoShadow(rg);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            vis.Rotate(0f, 100f * dt, 0f);
            vis.localPosition = new Vector3(0f, 0.08f * Mathf.Sin(Time.time * 2.5f + seed), 0f);
            if (life > 0f)
            {
                life -= dt;
                if (life <= 0f) { Destroy(gameObject); return; }
                if (life < 5f) vis.gameObject.SetActive(Mathf.Repeat(life * 4f, 1f) > 0.35f);
            }
            var gm = GameManager.I;
            if (gm == null || gm.player == null || gm.player.dead) return;
            Vector3 d = gm.player.transform.position + Vector3.up * 0.9f - transform.position;
            d.y *= 0.5f;
            if (d.sqrMagnitude < 2.2f && gm.player.Collect(type))
            {
                Sfx.Play2D("pickup", 0.8f);
                switch (type)
                {
                    case PType.Health: gm.Feed("+40 DARAH", new Color(1f, 0.4f, 0.4f)); break;
                    case PType.Armor: gm.Feed("+50 ARMOR", new Color(0.4f, 0.7f, 1f)); break;
                    case PType.Grenade: gm.Feed("+2 GRANAT", new Color(0.5f, 1f, 0.5f)); break;
                    default: gm.Feed("AMUNISI DIISI", new Color(1f, 0.85f, 0.3f)); break;
                }
                Destroy(gameObject);
            }
        }
    }
}
