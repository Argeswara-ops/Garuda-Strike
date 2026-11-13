using System.Collections.Generic;
using UnityEngine;

namespace GarudaStrike
{
    /// <summary>Raycast peluru: mengabaikan pemilik, trigger non-hitbox, dan CharacterController musuh.</summary>
    public static class Combat
    {
        public static bool Cast(Vector3 origin, Vector3 dir, float range, Transform ignoreRoot, out RaycastHit best)
        {
            var hits = Physics.RaycastAll(origin, dir, range, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                var c = h.collider;
                if (ignoreRoot != null && c.transform.IsChildOf(ignoreRoot)) continue;
                if (c.isTrigger && c.GetComponent<Hitbox>() == null) continue;
                if (c is CharacterController && c.GetComponent<Player>() == null) continue;
                if (c.GetComponent<Grenade>() != null) continue;
                best = h;
                return true;
            }
            best = default(RaycastHit);
            return false;
        }
    }

    public class Player : MonoBehaviour
    {
        public Camera cam;
        public float health = 100f, armor = 0f, stamina = 100f;
        public int grenades = 2;
        public bool dead;
        public float yaw;

        public float CurSpread { get; private set; }
        public bool Reloading { get { return reloading; } }
        public float ReloadProgress { get { return reloading ? 1f - Mathf.Clamp01((reloadEnd - Time.time) / ws[cur].d.reload) : 0f; } }
        public bool Scoped { get { return ws != null && ws[cur].d.scope && adsT > 0.9f; } }
        public WeaponDef CurDef { get { return ws[cur].d; } }
        public int CurMag { get { return ws[cur].mag; } }
        public int CurReserve { get { return ws[cur].reserve; } }
        public int CurIndex { get { return cur; } }

        class WS { public WeaponDef d; public int mag, reserve; public Transform model, muzzle; public GameObject flash; }

        const float SwapTime = 0.42f;
        CharacterController cc;
        float pitch, recoil, bloom, shake, roll;
        Vector3 moveVel;
        float vy;
        bool grounded, wasGrounded, crouching, sprinting, sprintLock;
        float eye = 1.65f, stepDist, bobT, sprintT;
        Vector3 spawnPos;
        Vector2 lookDelta;
        float swayX, swayY, wallPull;

        WS[] ws;
        int cur = 1;
        float nextFire, reloadEnd, flashT, adsT;
        bool reloading, ads;
        float swapT = -1f;
        int swapTo;
        bool swapped;
        float kickBack, kickRot, throwT, nextGren;
        Transform viewRoot, knife;
        float meleeT = -1f;
        bool meleeHit;
        public float sens = 2f, baseFov = 80f;

        public static Player Create(Vector3 pos, float startYaw, Transform parent)
        {
            var g = new GameObject("Player");
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            var p = g.AddComponent<Player>();
            p.Init(startYaw);
            return p;
        }

        void Init(float startYaw)
        {
            yaw = startYaw;
            spawnPos = transform.position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cc = gameObject.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;

            var cg = new GameObject("Camera");
            cg.tag = "MainCamera";
            cg.transform.SetParent(transform, false);
            cg.transform.localPosition = new Vector3(0f, eye, 0f);
            cam = cg.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 450f;
            cam.fieldOfView = GameManager.I.fov;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = GameManager.I.map.sky;
            cg.AddComponent<AudioListener>();

            viewRoot = new GameObject("ViewModel").transform;
            viewRoot.SetParent(cam.transform, false);
            ws = new WS[Weapons.All.Length];
            for (int i = 0; i < ws.Length; i++)
            {
                var w = new WS();
                w.d = Weapons.All[i];
                w.mag = w.d.mag;
                w.reserve = Mathf.RoundToInt(w.d.reserve * 0.6f);
                w.model = Weapons.Build(i, viewRoot, out w.muzzle);
                w.flash = w.muzzle.Find("flash").gameObject;
                w.model.gameObject.SetActive(false);
                ws[i] = w;
            }
            ws[cur].model.gameObject.SetActive(true);
            knife = Weapons.BuildKnife(cam.transform);
            foreach (var r in cam.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || cam == null) return;
            if (dead) { DeadUpdate(); return; }
            if (gm.state != GameManager.State.Playing) return;
            if (Time.unscaledTime < gm.inputBlock) return;
            float dt = Time.deltaTime;
            sens = gm.sens;
            baseFov = gm.fov;
            ads = Inp.MouseHeld(1) && !reloading && swapT < 0f && meleeT < 0f;
            UpdateLook();
            UpdateMove(dt);
            UpdateWeapons(dt);
            UpdateView(dt);
            if (transform.position.y < -20f)
            {
                cc.enabled = false;
                transform.position = spawnPos;
                cc.enabled = true;
            }
        }

        void UpdateLook()
        {
            lookDelta = Inp.MouseDelta * sens * (cam.fieldOfView / Mathf.Max(1f, baseFov));
            yaw += lookDelta.x;
            pitch = Mathf.Clamp(pitch - lookDelta.y, -88f, 88f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void UpdateMove(float dt)
        {
            float ix = (Inp.Held(Act.Right) ? 1f : 0f) - (Inp.Held(Act.Left) ? 1f : 0f);
            float iz = (Inp.Held(Act.Forward) ? 1f : 0f) - (Inp.Held(Act.Back) ? 1f : 0f);
            Vector3 wish = transform.right * ix + transform.forward * iz;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            crouching = Inp.Held(Act.Crouch);
            bool wantSprint = Inp.Held(Act.Sprint) && iz > 0.1f && !crouching && !ads && !Inp.MouseHeld(0) && grounded;
            if (sprintLock && stamina > 25f) sprintLock = false;
            sprinting = wantSprint && !sprintLock && stamina > 0f;
            if (sprinting)
            {
                stamina -= 20f * dt;
                if (stamina <= 0f) { stamina = 0f; sprintLock = true; sprinting = false; }
            }
            else stamina = Mathf.Min(100f, stamina + 14f * dt);

            float sp = crouching ? 2.6f : (sprinting ? 8f : 5.2f);
            if (ads) sp *= 0.6f;
            sp *= ws[cur].d.moveMul;
            float accel = grounded ? 45f : 8f;
            moveVel = Vector3.MoveTowards(moveVel, wish * sp, accel * dt);

            if (grounded && vy < 0f) vy = -2f;
            if (grounded && Inp.Down(Act.Jump)) vy = 7f;
            vy -= 22f * dt;

            cc.Move((moveVel + Vector3.up * vy) * dt);
            wasGrounded = grounded;
            grounded = cc.isGrounded;
            if (grounded && !wasGrounded)
            {
                if (vy < -13f) TakeDamage((-vy - 13f) * 6f, transform.position);
                Sfx.Play2D("step", 0.7f, 0.7f);
            }

            float th = crouching ? 1.2f : 1.8f;
            cc.height = Mathf.MoveTowards(cc.height, th, 6f * dt);
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
            eye = Mathf.Lerp(eye, crouching ? 1.15f : 1.65f, 12f * dt);

            float spd = new Vector2(moveVel.x, moveVel.z).magnitude;
            if (grounded && spd > 0.5f)
            {
                stepDist += spd * dt;
                float stride = sprinting ? 3.2f : (crouching ? 1.8f : 2.3f);
                if (stepDist > stride)
                {
                    stepDist = 0f;
                    Sfx.Play2D("step", crouching ? 0.25f : 0.5f, Random.Range(0.85f, 1.15f));
                }
            }
            roll = Mathf.Lerp(roll, -ix * 1.4f, 8f * dt);
        }

        void UpdateWeapons(float dt)
        {
            int want = -1;
            if (Inp.Down(Act.Slot1)) want = 0;
            else if (Inp.Down(Act.Slot2)) want = 1;
            else if (Inp.Down(Act.Slot3)) want = 2;
            else if (Inp.Down(Act.Slot4)) want = 3;
            float sc = Inp.Scroll;
            if (sc > 0f) want = (cur + ws.Length - 1) % ws.Length;
            else if (sc < 0f) want = (cur + 1) % ws.Length;
            if (want >= 0 && want != cur && swapT < 0f && meleeT < 0f)
            {
                swapTo = want;
                swapT = 0f;
                swapped = false;
                reloading = false;
                Sfx.Play2D("click", 0.4f, 1.4f);
            }
            if (swapT >= 0f)
            {
                swapT += dt;
                if (!swapped && swapT >= SwapTime * 0.5f)
                {
                    swapped = true;
                    ws[cur].model.gameObject.SetActive(false);
                    ws[cur].flash.SetActive(false);
                    cur = swapTo;
                    ws[cur].model.gameObject.SetActive(true);
                }
                if (swapT >= SwapTime) swapT = -1f;
            }
            var w = ws[cur];

            // sebaran peluru
            float baseSp = ads ? w.d.adsSpread : w.d.hipSpread;
            float spd = new Vector2(moveVel.x, moveVel.z).magnitude;
            float mul = 1f + Mathf.Clamp01(spd / 6f) * 0.9f + (grounded ? 0f : 1.5f);
            if (crouching) mul *= 0.7f;
            CurSpread = Mathf.Lerp(CurSpread, baseSp * mul + bloom, 12f * dt);

            // ADS
            adsT = Mathf.MoveTowards(adsT, ads ? 1f : 0f, dt * (w.d.scope ? 5f : 8f));

            // reload
            if (Inp.Down(Act.Reload)) StartReload();
            if (reloading && Time.time >= reloadEnd)
            {
                int take = Mathf.Min(w.d.mag - w.mag, w.reserve);
                w.mag += take;
                w.reserve -= take;
                reloading = false;
            }

            // pisau
            if (Inp.Down(Act.Knife) && meleeT < 0f && swapT < 0f)
            {
                meleeT = 0f;
                meleeHit = false;
                reloading = false;
                knife.gameObject.SetActive(true);
                w.model.gameObject.SetActive(false);
                Sfx.Play2D("knife", 0.8f);
            }
            if (meleeT >= 0f)
            {
                meleeT += dt;
                if (!meleeHit && meleeT > 0.15f) DoKnife();
                if (meleeT > 0.42f)
                {
                    meleeT = -1f;
                    knife.gameObject.SetActive(false);
                    w.model.gameObject.SetActive(true);
                }
            }

            // granat
            if (Inp.Down(Act.Grenade) && grenades > 0 && Time.time > nextGren && swapT < 0f && meleeT < 0f)
            {
                grenades--;
                nextGren = Time.time + 1f;
                throwT = 0.45f;
                Grenade.Throw(cam.transform.position + cam.transform.forward * 0.6f,
                    cam.transform.forward * 15f + Vector3.up * 3.5f + new Vector3(moveVel.x, 0f, moveVel.z) * 0.5f);
                Sfx.Play2D("knife", 0.6f, 0.8f);
            }

            // tembak
            if (swapT < 0f && meleeT < 0f && !reloading)
            {
                bool fireWant = w.d.auto ? Inp.MouseHeld(0) : Inp.MouseDown(0);
                if (fireWant && Time.time >= nextFire)
                {
                    if (w.mag > 0) Fire(w);
                    else
                    {
                        Sfx.Play2D("empty", 0.7f);
                        nextFire = Time.time + 0.3f;
                        if (w.reserve > 0) StartReload();
                    }
                }
            }
            if (w.mag == 0 && w.reserve > 0 && !reloading && swapT < 0f && meleeT < 0f && Time.time >= nextFire + 0.15f) StartReload();

            if (flashT > 0f)
            {
                flashT -= dt;
                if (flashT <= 0f) w.flash.SetActive(false);
            }
            bloom = Mathf.MoveTowards(bloom, 0f, 5f * dt);
        }

        void StartReload()
        {
            var w = ws[cur];
            if (reloading || swapT >= 0f || meleeT >= 0f) return;
            if (w.mag >= w.d.mag || w.reserve <= 0) return;
            reloading = true;
            reloadEnd = Time.time + w.d.reload;
            Sfx.Play2D("reload", 0.8f, 1.6f / Mathf.Max(1f, w.d.reload));
        }

        void Fire(WS w)
        {
            var d = w.d;
            w.mag--;
            nextFire = Time.time + 1f / d.rate;
            var gm = GameManager.I;
            gm.shots++;

            float k = d.kick * (ads ? 0.55f : 1f) * (crouching ? 0.8f : 1f);
            recoil += k;
            yaw += Random.Range(-0.25f, 0.25f) * k;
            bloom = Mathf.Min(bloom + d.kick * 0.3f, 4f);
            kickBack = Mathf.Min(kickBack + 0.04f + d.kick * 0.004f, 0.14f);
            kickRot += d.kick * 1.3f;
            flashT = 0.045f;
            w.flash.SetActive(true);
            Sfx.Play2D(d.sound, d.scope ? 1f : 0.85f, Random.Range(0.96f, 1.04f));

            Vector3 org = cam.transform.position;
            bool anyHit = false, anyHead = false, anyKill = false;
            for (int i = 0; i < d.pellets; i++)
            {
                Vector2 r = Random.insideUnitCircle * CurSpread;
                Vector3 dir = cam.transform.TransformDirection(Quaternion.Euler(r.y, r.x, 0f) * Vector3.forward);
                Vector3 end = org + dir * Mathf.Min(d.range, 150f);
                RaycastHit h;
                if (Combat.Cast(org, dir, d.range, transform, out h))
                {
                    end = h.point;
                    var hb = h.collider.GetComponent<Hitbox>();
                    if (hb != null && hb.owner != null)
                    {
                        if (!hb.owner.dead)
                        {
                            float f = Mathf.Lerp(1f, d.minMul, Mathf.InverseLerp(d.fallStart, d.fallEnd, h.distance));
                            bool head = hb.mult >= 2f;
                            bool kill = hb.owner.Damage(d.damage * hb.mult * f, head, h.point, h.normal);
                            anyHit = true;
                            anyHead |= head;
                            anyKill |= kill;
                        }
                    }
                    else
                    {
                        Fx.Sparks(h.point, h.normal, new Color(1f, 0.8f, 0.4f), 4);
                        Fx.Hole(h.point, h.normal);
                    }
                }
                if (i < 3) Fx.Tracer(w.muzzle.position, end, new Color(1f, 0.85f, 0.4f), 0.015f, 0.04f);
            }
            if (anyHit) gm.OnHit(anyHead, anyKill);
        }

        void DoKnife()
        {
            meleeHit = true;
            RaycastHit h;
            if (!Combat.Cast(cam.transform.position, cam.transform.forward, 2.4f, transform, out h)) return;
            var hb = h.collider.GetComponent<Hitbox>();
            if (hb != null && hb.owner != null && !hb.owner.dead)
            {
                bool head = hb.mult >= 2f;
                bool kill = hb.owner.Damage(head ? 140f : 80f, head, h.point, h.normal);
                GameManager.I.OnHit(head, kill);
                Sfx.Play2D("melee", 0.9f);
            }
            else
            {
                Fx.Sparks(h.point, h.normal, new Color(1f, 0.9f, 0.6f), 5);
                Sfx.Play2D("melee", 0.5f, 1.4f);
            }
        }

        void UpdateView(float dt)
        {
            var d = ws[cur].d;
            Vector3 hip = new Vector3(0.2f, -0.2f, 0.42f);
            Vector3 pos = Vector3.Lerp(hip, d.adsPos, adsT);
            Vector3 rot = Vector3.zero;

            float spd = new Vector2(moveVel.x, moveVel.z).magnitude;
            bool moving = grounded && spd > 0.5f;
            if (moving) bobT += dt * (sprinting ? 12f : 8f) * (crouching ? 0.7f : 1f);
            float bobAmt = moving ? (sprinting ? 0.02f : 0.01f) * (1f - adsT * 0.9f) : 0f;
            pos.x += Mathf.Sin(bobT) * bobAmt;
            pos.y += Mathf.Abs(Mathf.Cos(bobT)) * bobAmt - bobAmt * 0.5f;

            swayX = Mathf.Lerp(swayX, Mathf.Clamp(-lookDelta.x * 0.004f, -0.05f, 0.05f), 8f * dt);
            swayY = Mathf.Lerp(swayY, Mathf.Clamp(-lookDelta.y * 0.004f, -0.05f, 0.05f), 8f * dt);
            pos.x += swayX;
            pos.y += swayY;
            rot.y += swayX * 60f;

            sprintT = Mathf.MoveTowards(sprintT, sprinting ? 1f : 0f, dt * 6f);
            pos.x -= 0.08f * sprintT;
            pos.y -= 0.03f * sprintT;
            rot.y += 35f * sprintT;
            rot.x += 12f * sprintT;

            kickBack = Mathf.Lerp(kickBack, 0f, 14f * dt);
            kickRot = Mathf.Lerp(kickRot, 0f, 12f * dt);
            pos.z -= kickBack;
            rot.x -= kickRot;

            if (reloading)
            {
                float s = Mathf.Sin(ReloadProgress * Mathf.PI);
                pos.y -= 0.1f * s;
                pos.x += 0.04f * s;
                rot.x += 55f * s;
                rot.z -= 25f * s;
            }
            if (swapT >= 0f)
            {
                float l = Mathf.Sin(Mathf.Clamp01(swapT / SwapTime) * Mathf.PI);
                pos.y -= 0.35f * l;
                rot.x += 40f * l;
            }
            if (throwT > 0f)
            {
                throwT -= dt;
                float s = Mathf.Sin(Mathf.Clamp01(throwT / 0.45f) * Mathf.PI);
                pos.y -= 0.15f * s;
                rot.x += 30f * s;
            }

            RaycastHit h;
            float wt = 0f;
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out h, 1.1f, ~0, QueryTriggerInteraction.Ignore))
                wt = Mathf.Clamp01((1.1f - h.distance) / 0.7f);
            wallPull = Mathf.Lerp(wallPull, wt, 12f * dt);
            pos.z -= 0.2f * wallPull;
            pos.y -= 0.05f * wallPull;
            rot.x += 35f * wallPull;

            viewRoot.localPosition = pos;
            viewRoot.localRotation = Quaternion.Euler(rot);
            viewRoot.gameObject.SetActive(!Scoped);

            if (meleeT >= 0f)
            {
                float t = Mathf.Clamp01(meleeT / 0.42f);
                float a = Mathf.Lerp(55f, -65f, Mathf.SmoothStep(0f, 1f, t));
                knife.localPosition = new Vector3(0.15f - 0.2f * Mathf.Sin(t * Mathf.PI), -0.2f, 0.38f);
                knife.localRotation = Quaternion.Euler(10f, a, -20f);
            }

            // FOV
            float tf = Mathf.Lerp(baseFov, d.adsFov, adsT) + (sprinting ? 6f : 0f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, tf, 14f * dt);

            // kamera: recoil, bob, getar
            recoil = Mathf.Lerp(recoil, 0f, 8f * dt);
            shake = Mathf.MoveTowards(shake, 0f, 3f * dt);
            float bobY = moving ? Mathf.Sin(bobT * 2f) * 0.025f * (1f - adsT) : 0f;
            Vector3 so = Random.insideUnitSphere * shake * 0.05f;
            cam.transform.localPosition = new Vector3(0f, eye + bobY, 0f) + so;
            cam.transform.localRotation = Quaternion.Euler(pitch - recoil + so.x * 20f, so.y * 20f, roll + so.z * 30f);
        }

        void DeadUpdate()
        {
            float dt = Time.deltaTime;
            eye = Mathf.Lerp(eye, 0.3f, 4f * dt);
            roll = Mathf.Lerp(roll, 65f, 3f * dt);
            pitch = Mathf.Lerp(pitch, 15f, 2f * dt);
            cam.transform.localPosition = new Vector3(0f, eye, 0f);
            cam.transform.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }

        public void Shake(float s) { shake = Mathf.Min(1.5f, shake + s); }

        public void TakeDamage(float amt, Vector3 src)
        {
            if (dead || amt <= 0f) return;
            float a = Mathf.Min(armor, amt * 0.6f);
            armor -= a;
            health -= amt - a;
            shake = Mathf.Min(1.2f, shake + amt * 0.03f);
            GameManager.I.OnPlayerHurt(src, amt);
            Sfx.Play2D("hurt", 0.9f);
            if (health <= 0f)
            {
                health = 0f;
                dead = true;
                viewRoot.gameObject.SetActive(false);
                knife.gameObject.SetActive(false);
                GameManager.I.OnPlayerDead();
            }
        }

        public bool Collect(PType t)
        {
            switch (t)
            {
                case PType.Health:
                    if (health >= 100f) return false;
                    health = Mathf.Min(100f, health + 40f);
                    return true;
                case PType.Armor:
                    if (armor >= 100f) return false;
                    armor = Mathf.Min(100f, armor + 50f);
                    return true;
                case PType.Grenade:
                    if (grenades >= 5) return false;
                    grenades = Mathf.Min(5, grenades + 2);
                    return true;
                default:
                    bool any = false;
                    foreach (var w in ws)
                    {
                        int max = w.d.reserve;
                        if (w.reserve < max)
                        {
                            w.reserve = Mathf.Min(max, w.reserve + Mathf.CeilToInt(max * 0.5f));
                            any = true;
                        }
                    }
                    return any;
            }
        }
    }

    public class Grenade : MonoBehaviour
    {
        float fuse = 2.2f;

        public static void Throw(Vector3 pos, Vector3 vel)
        {
            var g = Gfx.Prim(PrimitiveType.Sphere, Fx.Root, pos, Vector3.one * 0.14f, new Color(0.15f, 0.26f, 0.12f), Vector3.zero, true, 0.4f, 0.3f);
            var rb = g.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            g.AddComponent<Grenade>();
            rb.AddForce(vel, ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.VelocityChange);
        }

        void Update()
        {
            fuse -= Time.deltaTime;
            if (fuse <= 0f) Explode();
        }

        void Explode()
        {
            Vector3 pos = transform.position;
            Fx.Explosion(pos, 6f);
            var seen = new HashSet<Enemy>();
            bool hurtPlayer = false, anyHit = false, anyKill = false;
            var cols = Physics.OverlapSphere(pos, 7f, ~0, QueryTriggerInteraction.Collide);
            foreach (var c in cols)
            {
                var hb = c.GetComponent<Hitbox>();
                if (hb != null && hb.owner != null && !hb.owner.dead && seen.Add(hb.owner))
                {
                    float d1 = Vector3.Distance(pos, hb.owner.transform.position + Vector3.up);
                    float f1 = Mathf.Clamp01(1f - d1 / 7f);
                    if (f1 > 0f)
                    {
                        anyHit = true;
                        if (hb.owner.Damage(170f * f1 + 15f, false, hb.owner.transform.position + Vector3.up, Vector3.up)) anyKill = true;
                    }
                }
                var pl = c.GetComponent<Player>();
                if (pl != null && !pl.dead && !hurtPlayer)
                {
                    hurtPlayer = true;
                    float d2 = Vector3.Distance(pos, pl.transform.position + Vector3.up);
                    float f2 = Mathf.Clamp01(1f - d2 / 7f);
                    pl.TakeDamage(70f * f2, pos);
                    pl.Shake(1f);
                }
            }
            var gp = GameManager.I != null ? GameManager.I.player : null;
            if (gp != null && !hurtPlayer && Vector3.Distance(pos, gp.transform.position) < 20f) gp.Shake(0.5f);
            if (anyHit && GameManager.I != null) GameManager.I.OnHit(false, anyKill);
            Destroy(gameObject);
        }
    }
}
