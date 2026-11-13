using System.Collections.Generic;
using UnityEngine;

namespace GarudaStrike
{
    /// <summary>
    /// Pengendali utama: menu, pemilihan map, aturan permainan, gelombang musuh, skor, dan HUD.
    /// Otomatis aktif saat tombol Play ditekan di scene apa pun (lihat Boot()).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;
        public enum State { Menu, Playing, Paused, Dead, Over }
        public State state = State.Menu;
        enum Page { Main, Select, Help, Settings }
        Page page = Page.Main;

        // pengaturan
        public float sens = 2f, fov = 80f, vol = 0.85f;
        bool showFps = true, pauseSettings;
        public float inputBlock;

        // pilihan
        int selMap, selDiff = 1, selMode;
        static readonly string[] MapNames = { "GUDANG LOGISTIK", "KOTA GURUN" };
        static readonly string[] MapDesc =
        {
            "Gudang tertutup: rak tinggi, kontainer, catwalk. Pertempuran jarak dekat-menengah dengan banyak sudut buta.",
            "Kota terbengkalai di tengah gurun. Area luas, atap bangunan, mobil rusak. Cocok untuk sniper & manuver."
        };
        static readonly string[] DiffNames = { "MUDAH", "NORMAL", "SULIT" };
        static readonly string[] ModeNames = { "SURVIVAL 5 GELOMBANG", "TIME ATTACK 5:00" };
        const int MaxWave = 5;

        // dunia
[System.NonSerialized] public MapInfo map;        MapInfo bdMap;
        Transform worldRoot, bdRoot;
        Camera menuCam;
        float orbit;
        public Player player;
        System.Action pending;

        // status permainan
        int wave;
        float intermission, spawnTimer, timeLeft, playTime, deadTimer;
        bool bossSpawned, won, newRecord;
        readonly List<EType> queue = new List<EType>();
        public int score, kills, headKills, shots, hits, combo, bestCombo;
        float lastKill, damageTaken;

        // HUD
        class FeedItem { public string text; public Color col; public float t; }
        class Hurt { public Vector3 pos; public float t; }
        readonly List<FeedItem> feed = new List<FeedItem>();
        readonly List<Hurt> hurts = new List<Hurt>();
        float hurtFlash, hitT = -10f, fps = 60f, gameStartT;
        bool hitHead, hitKill;
        string bnr = "", bnrSub = "";
        float bnrT, bnrDur = 1f;

        // GUI
        bool stylesReady;
        GUIStyle sBtn, sBtnSel, sSlider, sThumb;
        readonly Dictionary<int, GUIStyle> styleCache = new Dictionary<int, GUIStyle>();
        Texture2D texVig, texScope;
        static readonly Color Orange = new Color(1f, 0.55f, 0.1f);
        static readonly Color Panel = new Color(0.04f, 0.05f, 0.07f, 0.86f);

        float HpMul { get { return selDiff == 0 ? 0.8f : (selDiff == 1 ? 1f : 1.35f); } }
        float DmgMul { get { return selDiff == 0 ? 0.6f : (selDiff == 1 ? 1f : 1.4f); } }
        float SpreadMul { get { return selDiff == 0 ? 1.4f : (selDiff == 1 ? 1f : 0.75f); } }
        float CountMul { get { return selDiff == 0 ? 0.75f : (selDiff == 1 ? 1f : 1.25f); } }

        // ============ boot ============
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I == null)
            {
                var g = new GameObject("GarudaStrike");
                g.AddComponent<GameManager>();
            }
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            foreach (var c in Camera.allCameras) c.gameObject.SetActive(false);
            foreach (var l in Resources.FindObjectsOfTypeAll<Light>())
                if (l.gameObject.scene.IsValid()) l.gameObject.SetActive(false);
            foreach (var al in Resources.FindObjectsOfTypeAll<AudioListener>())
                if (al.gameObject.scene.IsValid()) al.enabled = false;

            sens = PlayerPrefs.GetFloat("gs_sens", 2f);
            fov = PlayerPrefs.GetFloat("gs_fov", 80f);
            vol = PlayerPrefs.GetFloat("gs_vol", 0.85f);
            showFps = PlayerPrefs.GetInt("gs_fps", 1) == 1;
            Time.timeScale = 1f;
            Sfx.Build();
            Sfx.Music(true, 0.35f);
            BuildTextures();
            ShowBackdrop(0);
        }

        void BuildTextures()
        {
            int n = 128;
            texVig = new Texture2D(n, n, TextureFormat.RGBA32, false);
            texVig.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x / (float)(n - 1) - 0.5f) * 2f, dy = (y / (float)(n - 1) - 0.5f) * 2f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(r - 0.25f) / 0.9f, 2f));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            texVig.SetPixels32(px);
            texVig.Apply();

            n = 512;
            texScope = new Texture2D(n, n, TextureFormat.RGBA32, false);
            texScope.wrapMode = TextureWrapMode.Clamp;
            px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x / (float)(n - 1) - 0.5f) * 2f, dy = (y / (float)(n - 1) - 0.5f) * 2f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = r > 0.93f ? 1f : (r > 0.9f ? (r - 0.9f) / 0.03f : 0f);
                    px[y * n + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            texScope.SetPixels32(px);
            texScope.Apply();
        }

        // ============ dunia / menu ============
        void ShowBackdrop(int m)
        {
            if (bdRoot != null) Destroy(bdRoot.gameObject);
            bdRoot = new GameObject("Backdrop").transform;
            Fx.Root = bdRoot;
            bdMap = MapBuilder.Build(m, bdRoot);
            if (menuCam == null)
            {
                var g = new GameObject("MenuCam");
                g.tag = "MainCamera";
                menuCam = g.AddComponent<Camera>();
                menuCam.fieldOfView = 62f;
                g.AddComponent<AudioListener>();
            }
            menuCam.clearFlags = CameraClearFlags.SolidColor;
            menuCam.backgroundColor = bdMap.sky;
        }

        void ClearWorld()
        {
            if (worldRoot != null) Destroy(worldRoot.gameObject);
            worldRoot = null;
            player = null;
        }

        void StartGame()
        {
            ClearWorld();
            if (bdRoot != null) { Destroy(bdRoot.gameObject); bdRoot = null; }
            if (menuCam != null) { menuCam.gameObject.SetActive(false); Destroy(menuCam.gameObject); menuCam = null; }

            worldRoot = new GameObject("World").transform;
            Fx.Root = worldRoot;
            Fx.Reset();
            map = MapBuilder.Build(selMap, worldRoot);
            player = Player.Create(map.playerSpawn, map.playerYaw, worldRoot);

            score = kills = headKills = shots = hits = combo = bestCombo = 0;
            lastKill = -100f;
            damageTaken = 0f;
            playTime = 0f;
            bossSpawned = false;
            wave = 0;
            queue.Clear();
            feed.Clear();
            hurts.Clear();
            hurtFlash = 0f;
            gameStartT = Time.unscaledTime;
            Time.timeScale = 1f;
            state = State.Playing;
            inputBlock = Time.unscaledTime + 0.3f;
            Sfx.Music(true, 0.15f);

            SpawnMapPickups();
            if (selMode == 0) StartWave(1);
            else
            {
                timeLeft = 300f;
                spawnTimer = 3f;
                Banner("TIME ATTACK", "Kumpulkan skor sebanyak mungkin dalam 5 menit!", 4f);
            }
        }

        void ToMenu()
        {
            Time.timeScale = 1f;
            ClearWorld();
            state = State.Menu;
            page = Page.Main;
            pauseSettings = false;
            Sfx.Music(true, 0.35f);
            ShowBackdrop(selMap);
        }

        void Pause()
        {
            state = State.Paused;
            Time.timeScale = 0f;
            pauseSettings = false;
        }

        void Resume()
        {
            SaveSettings();
            state = State.Playing;
            Time.timeScale = 1f;
            inputBlock = Time.unscaledTime + 0.25f;
        }

        void SaveSettings()
        {
            PlayerPrefs.SetFloat("gs_sens", sens);
            PlayerPrefs.SetFloat("gs_fov", fov);
            PlayerPrefs.SetFloat("gs_vol", vol);
            PlayerPrefs.SetInt("gs_fps", showFps ? 1 : 0);
            PlayerPrefs.Save();
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus && state == State.Playing) Pause();
        }

        // ============ aturan permainan ============
        void StartWave(int n)
        {
            wave = n;
            BuildQueue(n);
            intermission = n == 1 ? 4f : 9f;
            if (n > 1 && player != null && !player.dead)
            {
                player.health = Mathf.Min(100f, player.health + 30f);
                player.Collect(PType.Ammo);
                player.grenades = Mathf.Min(5, player.grenades + 1);
                foreach (var p in new List<Pickup>(Pickup.All)) Destroy(p.gameObject);
                SpawnMapPickups();
                Feed("BONUS GELOMBANG: +30 DARAH, AMUNISI, +1 GRANAT", new Color(0.5f, 1f, 0.6f));
            }
            Banner("GELOMBANG " + n, n == 1 ? "Bersiaplah..." : "Gelombang sebelumnya selesai! Bersiap...", intermission);
        }

        void BuildQueue(int w)
        {
            queue.Clear();
            float cm = CountMul;
            int g = Mathf.Max(3, Mathf.RoundToInt((4 + w * 2) * cm));
            int r = w >= 2 ? Mathf.Max(1, Mathf.RoundToInt((w - 1) * cm)) : 0;
            int h = w >= 3 ? Mathf.Max(1, Mathf.RoundToInt((w - 2) * cm)) : 0;
            for (int i = 0; i < g; i++) queue.Add(EType.Grunt);
            for (int i = 0; i < r; i++) queue.Add(EType.Runner);
            for (int i = 0; i < h; i++) queue.Add(EType.Heavy);
            for (int i = queue.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = queue[i]; queue[i] = queue[j]; queue[j] = tmp;
            }
            if (w == MaxWave) queue.Insert(queue.Count / 2, EType.Boss);
        }

        void SpawnMapPickups()
        {
            foreach (var p in map.pickupSpawns)
            {
                float r = Random.value;
                PType t = r < 0.3f ? PType.Health : (r < 0.65f ? PType.Ammo : (r < 0.85f ? PType.Armor : PType.Grenade));
                Pickup.Create(t, p, worldRoot);
            }
        }

        Vector3 PickSpawn()
        {
            var list = map.enemySpawns;
            for (int i = 0; i < 14; i++)
            {
                Vector3 c = list[Random.Range(0, list.Count)] + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
                if (player != null && Vector3.Distance(c, player.transform.position) < 16f) continue;
                if (Physics.CheckSphere(c + Vector3.up * 1.1f, 0.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                return c;
            }
            return list[Random.Range(0, list.Count)];
        }

        void SpawnEnemy(EType t)
        {
            Vector3 p = PickSpawn();
            Enemy.Spawn(t, p, HpMul, DmgMul, SpreadMul, worldRoot);
            Fx.Burst(p + Vector3.up, new Color(0.5f, 0.8f, 1f), 1.2f, 0.3f);
        }

        void WaveLogic(float dt)
        {
            if (intermission > 0f)
            {
                intermission -= dt;
                if (intermission <= 0f)
                {
                    Banner("GELOMBANG " + wave, "Habisi semua musuh!", 2.5f);
                    Sfx.Play2D("horn", 0.8f);
                    spawnTimer = 0.5f;
                }
                return;
            }
            if (queue.Count > 0)
            {
                spawnTimer -= dt;
                if (spawnTimer <= 0f && Enemy.Alive() < 12)
                {
                    SpawnEnemy(queue[0]);
                    queue.RemoveAt(0);
                    spawnTimer = 0.8f;
                }
            }
            else if (Enemy.Alive() == 0)
            {
                score += 500 * wave;
                Feed("GELOMBANG " + wave + " SELESAI  +" + (500 * wave), new Color(0.5f, 1f, 0.5f));
                if (wave >= MaxWave) EndGame(true);
                else StartWave(wave + 1);
            }
        }

        void TimeLogic(float dt)
        {
            timeLeft -= dt;
            if (timeLeft <= 0f) { EndGame(true); return; }
            spawnTimer -= dt;
            float el = 300f - timeLeft;
            int cap = 6 + Mathf.FloorToInt(el / 60f) * 2;
            if (spawnTimer <= 0f && Enemy.Alive() < cap)
            {
                float r = Random.value;
                EType t = EType.Grunt;
                if (el > 60f && r > 0.85f) t = EType.Heavy;
                else if (el > 20f && r > 0.55f) t = EType.Runner;
                SpawnEnemy(t);
                spawnTimer = 1.2f;
            }
            if (!bossSpawned && timeLeft < 150f)
            {
                bossSpawned = true;
                SpawnEnemy(EType.Boss);
                Banner("BOS MUNCUL!", "Kalahkan komandan musuh", 3f);
                Sfx.Play2D("horn", 0.9f, 0.8f);
            }
        }

        public void OnHit(bool head, bool kill)
        {
            hits++;
            hitT = Time.unscaledTime;
            hitHead = head;
            hitKill = kill;
            Sfx.Play2D(kill ? "kill" : (head ? "headshot" : "hit"), 0.6f);
        }

        public void OnEnemyKilled(Enemy e, bool head)
        {
            kills++;
            if (head) headKills++;
            if (Time.time - lastKill < 5f) combo++; else combo = 1;
            lastKill = Time.time;
            bestCombo = Mathf.Max(bestCombo, combo);
            int mult = Mathf.Min(combo, 5);
            int pts = Mathf.RoundToInt((e.score + (head ? 50 : 0)) * (1f + 0.2f * (mult - 1)));
            score += pts;
            Feed("+" + pts + (head ? "  HEADSHOT" : "") + (mult > 1 ? "  x" + mult : ""), head ? new Color(1f, 0.85f, 0.2f) : Color.white);
            int drops = e.type == EType.Boss ? 3 : 1;
            for (int i = 0; i < drops; i++)
            {
                float r = Random.value;
                if (e.type == EType.Boss) r = i == 0 ? 0.1f : (i == 1 ? 0.3f : 0.5f);
                PType? t = null;
                if (r < 0.22f) t = PType.Health;
                else if (r < 0.45f) t = PType.Ammo;
                else if (r < 0.55f) t = PType.Armor;
                else if (r < 0.62f) t = PType.Grenade;
                if (t.HasValue)
                    Pickup.Create(t.Value, e.transform.position + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)), worldRoot, 30f);
            }
        }

        public void OnPlayerHurt(Vector3 src, float amt)
        {
            hurtFlash = Mathf.Clamp01(hurtFlash + amt / 35f);
            damageTaken += amt;
            hurts.Add(new Hurt { pos = src, t = Time.time });
        }

        public void OnPlayerDead()
        {
            if (state != State.Playing) return;
            state = State.Dead;
            deadTimer = 2.4f;
            Banner("GUGUR", "", 2.4f);
        }

        void EndGame(bool win)
        {
            if (state == State.Over) return;
            won = win;
            state = State.Over;
            string key = "gs_hs_" + selMap + "_" + selMode;
            int old = PlayerPrefs.GetInt(key, 0);
            newRecord = score > old;
            if (newRecord) { PlayerPrefs.SetInt(key, score); PlayerPrefs.Save(); }
            Time.timeScale = 0f;
        }

        public void Feed(string text, Color c)
        {
            feed.Add(new FeedItem { text = text, col = c, t = Time.unscaledTime });
            if (feed.Count > 6) feed.RemoveAt(0);
        }

        void Banner(string t, string sub, float dur)
        {
            bnr = t;
            bnrSub = sub;
            bnrT = dur;
            bnrDur = Mathf.Max(0.1f, dur);
        }

        // ============ update ============
        void Update()
        {
            if (pending != null) { var a = pending; pending = null; a(); }
            AudioListener.volume = vol;
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.05f);
            bool locked = state == State.Playing || state == State.Dead;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            float dt = Time.deltaTime;

            if (state == State.Menu)
            {
                if (Inp.Down(Act.Pause) && page != Page.Main) page = Page.Main;
                if (menuCam != null && bdMap != null)
                {
                    orbit += Time.unscaledDeltaTime * 7f;
                    float ang = orbit * Mathf.Deg2Rad;
                    Vector3 c = bdMap.center;
                    menuCam.transform.position = c + new Vector3(Mathf.Cos(ang) * bdMap.orbitRadius, bdMap.orbitHeight, Mathf.Sin(ang) * bdMap.orbitRadius);
                    menuCam.transform.LookAt(c + Vector3.up * 1.5f);
                }
                return;
            }

            if (Inp.Down(Act.Pause))
            {
                if (state == State.Playing) Pause();
                else if (state == State.Paused) Resume();
            }
            if (state == State.Dead)
            {
                deadTimer -= dt;
                if (deadTimer <= 0f) EndGame(false);
            }
            if (state != State.Playing && state != State.Dead) return;

            hurtFlash = Mathf.MoveTowards(hurtFlash, 0f, dt * 0.8f);
            if (bnrT > 0f) bnrT -= dt;
            if (state != State.Playing) return;
            playTime += dt;
            if (selMode == 0) WaveLogic(dt); else TimeLogic(dt);
        }

        // ============ GUI ============
        Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        void InitStyles()
        {
            stylesReady = true;
            sBtn = new GUIStyle(GUI.skin.button) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sBtn.normal.background = MakeTex(new Color(0.12f, 0.14f, 0.18f, 0.92f));
            sBtn.hover.background = MakeTex(Orange);
            sBtn.active.background = MakeTex(new Color(0.8f, 0.4f, 0.05f));
            sBtn.normal.textColor = Color.white;
            sBtn.hover.textColor = Color.black;
            sBtn.active.textColor = Color.black;
            sBtn.border = new RectOffset(0, 0, 0, 0);
            sBtnSel = new GUIStyle(sBtn);
            sBtnSel.normal.background = MakeTex(new Color(0.85f, 0.42f, 0.05f));
            sBtnSel.normal.textColor = Color.white;
            sSlider = new GUIStyle(GUI.skin.horizontalSlider);
            sSlider.normal.background = MakeTex(new Color(0.3f, 0.32f, 0.36f));
            sSlider.fixedHeight = 10;
            sSlider.border = new RectOffset(0, 0, 0, 0);
            sThumb = new GUIStyle(GUI.skin.horizontalSliderThumb);
            sThumb.normal.background = MakeTex(Orange);
            sThumb.hover.background = MakeTex(Color.white);
            sThumb.active.background = MakeTex(Color.white);
            sThumb.fixedWidth = 26;
            sThumb.fixedHeight = 38;
            sThumb.border = new RectOffset(0, 0, 0, 0);
        }

        GUIStyle St(int size, TextAnchor a = TextAnchor.UpperLeft, bool bold = true, bool wrap = false)
        {
            int key = size * 1000 + (int)a * 10 + (bold ? 1 : 0) + (wrap ? 500 : 0);
            GUIStyle s;
            if (styleCache.TryGetValue(key, out s)) return s;
            s = new GUIStyle(GUI.skin.label);
            s.fontSize = size;
            s.alignment = a;
            s.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            s.wordWrap = wrap;
            s.richText = true;
            s.normal.textColor = Color.white;
            styleCache[key] = s;
            return s;
        }

        void Fill(Rect r, Color c)
        {
            var o = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = o;
        }

        void Txt(Rect r, string s, GUIStyle st, Color c)
        {
            var o = st.normal.textColor;
            st.normal.textColor = c;
            GUI.Label(r, s, st);
            st.normal.textColor = o;
        }

        void TxtS(Rect r, string s, GUIStyle st, Color c)
        {
            Rect r2 = r;
            r2.x += 2f;
            r2.y += 2f;
            Txt(r2, s, st, new Color(0f, 0f, 0f, 0.75f));
            Txt(r, s, st, c);
        }

        bool Btn(Rect r, string s, bool sel = false)
        {
            bool c = GUI.Button(r, s, sel ? sBtnSel : sBtn);
            if (c) Sfx.Play2D("click", 0.6f);
            return c;
        }

        void OnGUI()
        {
            if (!stylesReady) InitStyles();
            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            float vw = Screen.width / s;

            switch (state)
            {
                case State.Menu: DrawMenu(vw); break;
                case State.Playing:
                case State.Dead:
                    DrawHud(vw);
                    break;
                case State.Paused:
                    DrawHud(vw);
                    DrawPause(vw);
                    break;
                case State.Over:
                    DrawHud(vw);
                    DrawResult(vw);
                    break;
            }
            if (showFps) TxtS(new Rect(vw - 110f, 1050f, 100f, 24f), Mathf.RoundToInt(fps) + " FPS", St(18, TextAnchor.MiddleRight), new Color(1f, 1f, 1f, 0.6f));
        }

        // ---------- menu ----------
        void DrawMenu(float vw)
        {
            Fill(new Rect(0f, 0f, 780f, 1080f), new Color(0f, 0f, 0f, 0.68f));
            Fill(new Rect(780f, 0f, 6f, 1080f), Orange);

            if (page == Page.Main)
            {
                TxtS(new Rect(70f, 90f, 700f, 130f), "GARUDA", St(112), Color.white);
                TxtS(new Rect(70f, 195f, 700f, 130f), "STRIKE", St(112), Orange);
                TxtS(new Rect(76f, 330f, 700f, 40f), "OPERASI PEMBERSIHAN  •  FPS 3D", St(26, TextAnchor.UpperLeft, false), new Color(0.85f, 0.85f, 0.85f));
                if (Btn(new Rect(70f, 440f, 580f, 76f), "MULAI MISI")) page = Page.Select;
                if (Btn(new Rect(70f, 535f, 580f, 76f), "CARA BERMAIN & ATURAN")) page = Page.Help;
                if (Btn(new Rect(70f, 630f, 580f, 76f), "PENGATURAN")) page = Page.Settings;
                if (Btn(new Rect(70f, 725f, 580f, 76f), "KELUAR"))
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }
                TxtS(new Rect(76f, 1020f, 700f, 30f), "Versi 1.0  •  Seluruh aset dibuat prosedural", St(20, TextAnchor.UpperLeft, false), new Color(1f, 1f, 1f, 0.55f));
            }
            else if (page == Page.Select) DrawSelect();
            else if (page == Page.Help) DrawHelp(vw);
            else DrawSettings(vw, false);
        }

        void DrawSelect()
        {
            TxtS(new Rect(60f, 40f, 700f, 70f), "PILIH MISI", St(52), Orange);
            for (int i = 0; i < 2; i++)
            {
                Rect r = new Rect(60f, 125f + i * 195f, 660f, 175f);
                bool sel = selMap == i;
                Fill(r, sel ? new Color(0.9f, 0.45f, 0.05f, 0.95f) : new Color(0.1f, 0.12f, 0.16f, 0.95f));
                Fill(new Rect(r.x, r.y, 10f, r.height), i == 0 ? new Color(0.4f, 0.6f, 1f) : new Color(1f, 0.85f, 0.4f));
                TxtS(new Rect(r.x + 30f, r.y + 10f, 600f, 50f), MapNames[i], St(38), Color.white);
                Txt(new Rect(r.x + 30f, r.y + 60f, 610f, 80f), MapDesc[i], St(21, TextAnchor.UpperLeft, false, true), new Color(1f, 1f, 1f, 0.9f));
                int hs0 = PlayerPrefs.GetInt("gs_hs_" + i + "_" + selMode, 0);
                Txt(new Rect(r.x + 30f, r.y + 140f, 600f, 30f), "REKOR: " + hs0, St(20), new Color(1f, 0.95f, 0.5f));
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    Sfx.Play2D("click", 0.6f);
                    if (selMap != i) { selMap = i; int m = i; pending = () => ShowBackdrop(m); }
                }
            }
            TxtS(new Rect(60f, 525f, 400f, 36f), "MODE", St(26), new Color(1f, 1f, 1f, 0.8f));
            for (int i = 0; i < 2; i++)
                if (Btn(new Rect(60f + i * 335f, 565f, 325f, 62f), ModeNames[i], selMode == i)) selMode = i;
            TxtS(new Rect(60f, 645f, 400f, 36f), "KESULITAN", St(26), new Color(1f, 1f, 1f, 0.8f));
            for (int i = 0; i < 3; i++)
                if (Btn(new Rect(60f + i * 223f, 685f, 214f, 62f), DiffNames[i], selDiff == i)) selDiff = i;
            if (Btn(new Rect(60f, 790f, 660f, 90f), "MULAI PERTEMPURAN")) pending = StartGame;
            if (Btn(new Rect(60f, 900f, 660f, 66f), "KEMBALI")) page = Page.Main;
        }

        void DrawHelp(float vw)
        {
            Fill(new Rect(800f, 60f, vw - 860f, 960f), Panel);
            TxtS(new Rect(60f, 40f, 700f, 70f), "CARA BERMAIN", St(52), Orange);
            string[,] keys =
            {
                { "W A S D", "Bergerak" }, { "MOUSE", "Melihat / membidik" }, { "KLIK KIRI", "Tembak" }, { "KLIK KANAN", "Bidik (ADS / scope sniper)" },
                { "R", "Isi ulang amunisi" }, { "1 - 4 / SCROLL", "Ganti senjata" }, { "SHIFT", "Lari (memakai stamina)" }, { "CTRL / C", "Jongkok" },
                { "SPASI", "Lompat" }, { "V", "Serang dengan pisau" }, { "G", "Lempar granat" }, { "ESC", "Jeda permainan" }
            };
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                Txt(new Rect(60f, 125f + i * 56f, 260f, 44f), keys[i, 0], St(24), Orange);
                Txt(new Rect(320f, 125f + i * 56f, 450f, 44f), keys[i, 1], St(24, TextAnchor.UpperLeft, false), Color.white);
            }
            if (Btn(new Rect(60f, 930f, 660f, 66f), "KEMBALI")) page = Page.Main;

            TxtS(new Rect(840f, 80f, 800f, 60f), "ATURAN PERMAINAN", St(44), Orange);
            string rules =
                "<b>Mode Survival:</b> bertahan melewati 5 gelombang musuh. Gelombang 5 menghadirkan Bos.\n\n" +
                "<b>Mode Time Attack:</b> kumpulkan skor tertinggi dalam 5 menit; musuh muncul terus-menerus.\n\n" +
                "<b>Nyawa:</b> hanya satu. Darah 100 + Armor 100 (armor menyerap 60% damage). Jatuh dari ketinggian juga melukai.\n\n" +
                "<b>Damage:</b> kepala 2.5x, badan 1x, kaki 0.7x. Shotgun melemah dengan jarak; sniper tidak.\n\n" +
                "<b>Musuh:</b> Prajurit (seimbang), Pelari (cepat, serang jarak dekat), Berat (tangguh), Bos (sangat kuat).\n\n" +
                "<b>Item:</b> musuh dan peta menyediakan Medkit, Amunisi, Armor, dan Granat. Item hasil jatuhan hilang dalam 30 detik.\n\n" +
                "<b>Skor:</b> poin per eliminasi + bonus headshot + kombo beruntun (kill dalam 5 detik, hingga x5) + bonus selesai gelombang.\n\n" +
                "<b>Tips:</b> gunakan tembok sebagai perlindungan, pantau radar di kanan atas, dan jangan lari terus agar stamina tidak habis.";
            Txt(new Rect(840f, 150f, vw - 940f, 840f), rules, St(25, TextAnchor.UpperLeft, false, true), new Color(1f, 1f, 1f, 0.95f));
        }

        void DrawSettings(float vw, bool inGame)
        {
            float x = inGame ? vw * 0.5f - 400f : 60f;
            float y = inGame ? 330f : 40f;
            float w = inGame ? 800f : 660f;
            TxtS(new Rect(x, y, w, 70f), "PENGATURAN", St(inGame ? 42 : 52), Orange);
            y += 100f;
            sens = SliderRow(x, y, w, "Sensitivitas Mouse", sens, 0.5f, 6f, sens.ToString("0.0"));
            fov = SliderRow(x, y + 110f, w, "Bidang Pandang (FOV)", fov, 60f, 110f, Mathf.RoundToInt(fov).ToString());
            vol = SliderRow(x, y + 220f, w, "Volume", vol, 0f, 1f, Mathf.RoundToInt(vol * 100f) + "%");
            if (Btn(new Rect(x, y + 335f, w, 60f), "Tampilkan FPS: " + (showFps ? "AKTIF" : "MATI"))) showFps = !showFps;
            var names = QualitySettings.names;
            int ql = QualitySettings.GetQualityLevel();
            int cnt = Mathf.Min(names.Length, 3);
            float bw = (w - (cnt - 1) * 10f) / cnt;
            TxtS(new Rect(x, y + 410f, w, 36f), "Kualitas Grafis", St(24), new Color(1f, 1f, 1f, 0.85f));
            for (int i = 0; i < cnt; i++)
            {
                int idx = names.Length <= 3 ? i : Mathf.RoundToInt(i * (names.Length - 1) / (float)(cnt - 1));
                if (Btn(new Rect(x + i * (bw + 10f), y + 450f, bw, 56f), names[idx].ToUpper(), ql == idx)) QualitySettings.SetQualityLevel(idx, true);
            }
            if (Btn(new Rect(x, y + 540f, w, 66f), "KEMBALI"))
            {
                SaveSettings();
                if (inGame) pauseSettings = false; else page = Page.Main;
            }
        }

        float SliderRow(float x, float y, float w, string label, float v, float min, float max, string valText)
        {
            TxtS(new Rect(x, y, w - 120f, 36f), label, St(24), new Color(1f, 1f, 1f, 0.85f));
            TxtS(new Rect(x + w - 140f, y, 140f, 36f), valText, St(24, TextAnchor.UpperRight), Orange);
            return GUI.HorizontalSlider(new Rect(x, y + 48f, w, 40f), v, min, max, sSlider, sThumb);
        }

        // ---------- jeda & hasil ----------
        void DrawPause(float vw)
        {
            Fill(new Rect(0f, 0f, vw, 1080f), new Color(0f, 0f, 0f, 0.6f));
            if (pauseSettings) { DrawSettings(vw, true); return; }
            float x = vw * 0.5f - 280f;
            TxtS(new Rect(x, 250f, 560f, 100f), "JEDA", St(80, TextAnchor.UpperCenter), Orange);
            if (Btn(new Rect(x, 400f, 560f, 76f), "LANJUTKAN")) Resume();
            if (Btn(new Rect(x, 495f, 560f, 76f), "PENGATURAN")) pauseSettings = true;
            if (Btn(new Rect(x, 590f, 560f, 76f), "ULANGI MISI")) { Time.timeScale = 1f; pending = StartGame; }
            if (Btn(new Rect(x, 685f, 560f, 76f), "MENU UTAMA")) pending = ToMenu;
        }

        string Rank()
        {
            float k = selMode == 0 ? score / 9000f : score / 12000f;
            if (!won && selMode == 0) k *= 0.6f;
            return k >= 1f ? "S" : (k >= 0.65f ? "A" : (k >= 0.35f ? "B" : "C"));
        }

        void DrawResult(float vw)
        {
            Fill(new Rect(0f, 0f, vw, 1080f), new Color(0f, 0f, 0f, 0.7f));
            float x = vw * 0.5f - 460f;
            Fill(new Rect(x, 120f, 920f, 840f), Panel);
            Fill(new Rect(x, 120f, 920f, 8f), won ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.95f, 0.25f, 0.2f));
            string title = won ? (selMode == 0 ? "MISI BERHASIL" : "WAKTU HABIS") : "GUGUR DI MEDAN LAGA";
            TxtS(new Rect(x, 150f, 920f, 90f), title, St(60, TextAnchor.UpperCenter), won ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.35f, 0.3f));
            TxtS(new Rect(x, 235f, 920f, 40f), MapNames[selMap] + "  •  " + ModeNames[selMode] + "  •  " + DiffNames[selDiff], St(24, TextAnchor.UpperCenter, false), new Color(1f, 1f, 1f, 0.8f));
            TxtS(new Rect(x, 285f, 920f, 110f), score.ToString(), St(96, TextAnchor.UpperCenter), Orange);
            TxtS(new Rect(x, 395f, 920f, 40f), "SKOR AKHIR", St(24, TextAnchor.UpperCenter), new Color(1f, 1f, 1f, 0.7f));
            if (newRecord) TxtS(new Rect(x, 432f, 920f, 40f), "REKOR BARU!", St(30, TextAnchor.UpperCenter), new Color(1f, 0.95f, 0.3f));

            float acc = shots > 0 ? 100f * hits / shots : 0f;
            int mins = Mathf.FloorToInt(playTime / 60f), secs = Mathf.FloorToInt(playTime % 60f);
            string[,] rows =
            {
                { "Eliminasi", kills.ToString() }, { "Headshot", headKills.ToString() }, { "Akurasi", acc.ToString("0") + "%" },
                { "Kombo terbaik", "x" + Mathf.Min(bestCombo, 99) }, { "Damage diterima", Mathf.RoundToInt(damageTaken).ToString() },
                { selMode == 0 ? "Gelombang dicapai" : "Waktu bertahan", selMode == 0 ? wave + " / " + MaxWave : mins.ToString("00") + ":" + secs.ToString("00") }
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float rx = x + 60f + (i % 2) * 430f, ry = 490f + (i / 2) * 70f;
                Txt(new Rect(rx, ry, 250f, 36f), rows[i, 0], St(24, TextAnchor.UpperLeft, false), new Color(1f, 1f, 1f, 0.75f));
                Txt(new Rect(rx + 200f, ry, 160f, 36f), rows[i, 1], St(28, TextAnchor.UpperRight), Color.white);
            }
            TxtS(new Rect(x + 60f, 715f, 300f, 40f), "PERINGKAT", St(24), new Color(1f, 1f, 1f, 0.75f));
            TxtS(new Rect(x + 60f, 740f, 300f, 120f), Rank(), St(100), Orange);
            if (Btn(new Rect(x + 400f, 740f, 460f, 70f), "MAIN LAGI")) { Time.timeScale = 1f; pending = StartGame; }
            if (Btn(new Rect(x + 400f, 825f, 460f, 70f), "MENU UTAMA")) pending = ToMenu;
        }

        // ---------- HUD ----------
        void DrawScope(float vw)
        {
            float side = (vw - 1080f) * 0.5f;
            Fill(new Rect(0f, 0f, side, 1080f), Color.black);
            Fill(new Rect(vw - side, 0f, side, 1080f), Color.black);
            GUI.DrawTexture(new Rect(side, 0f, 1080f, 1080f), texScope);
            Color k = new Color(0f, 0f, 0f, 0.9f);
            Fill(new Rect(side + 40f, 539f, 1000f, 2f), k);
            Fill(new Rect(vw * 0.5f - 1f, 40f, 2f, 1000f), k);
            for (int i = 1; i <= 6; i++)
            {
                Fill(new Rect(vw * 0.5f - 1f + i * 40f - 1f, 530f, 2f, 20f), k);
                Fill(new Rect(vw * 0.5f - 1f - i * 40f - 1f, 530f, 2f, 20f), k);
            }
        }

        void DrawCross(float cx, float cy, Player p)
        {
            float gap = 6f + p.CurSpread * 9f;
            float len = 12f, th = 3f;
            for (int pass = 0; pass < 2; pass++)
            {
                float e = pass == 0 ? 1.5f : 0f;
                Color col = pass == 0 ? new Color(0f, 0f, 0f, 0.6f) : new Color(1f, 1f, 1f, 0.95f);
                Fill(new Rect(cx - th * 0.5f - e, cy - gap - len - e, th + e * 2f, len + e * 2f), col);
                Fill(new Rect(cx - th * 0.5f - e, cy + gap - e, th + e * 2f, len + e * 2f), col);
                Fill(new Rect(cx - gap - len - e, cy - th * 0.5f - e, len + e * 2f, th + e * 2f), col);
                Fill(new Rect(cx + gap - e, cy - th * 0.5f - e, len + e * 2f, th + e * 2f), col);
            }
            Fill(new Rect(cx - 1.5f, cy - 1.5f, 3f, 3f), new Color(1f, 0.3f, 0.2f, 0.9f));
        }

        void DrawRadar(float vw, Player p)
        {
            float size = 230f, x0 = vw - size - 30f, y0 = 30f, half = size * 0.5f, range = 45f;
            Fill(new Rect(x0, y0, size, size), new Color(0f, 0f, 0f, 0.5f));
            Fill(new Rect(x0, y0, size, 3f), Orange);
            Fill(new Rect(x0 + half - 1f, y0, 2f, size), new Color(1f, 1f, 1f, 0.08f));
            Fill(new Rect(x0, y0 + half - 1f, size, 2f), new Color(1f, 1f, 1f, 0.08f));
            Vector2 c = new Vector2(x0 + half, y0 + half);
            Quaternion inv = Quaternion.Inverse(Quaternion.Euler(0f, p.yaw, 0f));
            foreach (var k in Pickup.All)
            {
                Vector3 l = inv * (k.transform.position - p.transform.position);
                Vector2 q = new Vector2(l.x, -l.z) / range * half;
                if (Mathf.Abs(q.x) > half - 6f || Mathf.Abs(q.y) > half - 6f) continue;
                Fill(new Rect(c.x + q.x - 3f, c.y + q.y - 3f, 6f, 6f), new Color(0.4f, 1f, 0.5f, 0.9f));
            }
            foreach (var e in Enemy.All)
            {
                if (e.dead) continue;
                Vector3 l = inv * (e.transform.position - p.transform.position);
                Vector2 q = new Vector2(l.x, -l.z) / range * half;
                bool far = q.magnitude > half - 8f;
                if (far) q = q.normalized * (half - 8f);
                Fill(new Rect(c.x + q.x - 4f, c.y + q.y - 4f, 8f, 8f), far ? new Color(1f, 0.2f, 0.2f, 0.5f) : new Color(1f, 0.2f, 0.2f, 1f));
            }
            Fill(new Rect(c.x - 4f, c.y - 4f, 8f, 8f), Color.white);
            Fill(new Rect(c.x - 1.5f, c.y - 16f, 3f, 12f), Color.white);
        }

        void DrawHud(float vw)
        {
            var p = player;
            if (p == null) return;
            float cx = vw * 0.5f, cy = 540f;

            if (p.Scoped && !p.dead) DrawScope(vw);

            float va = hurtFlash;
            if (p.health < 30f && !p.dead) va = Mathf.Max(va, 0.25f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f));
            if (va > 0.01f)
            {
                GUI.color = new Color(1f, 0f, 0f, Mathf.Clamp01(va));
                GUI.DrawTexture(new Rect(0f, 0f, vw, 1080f), texVig);
                GUI.color = Color.white;
            }

            // indikator arah serangan
            var mtx = GUI.matrix;
            for (int i = hurts.Count - 1; i >= 0; i--)
            {
                float age = Time.time - hurts[i].t;
                if (age > 1.6f) { hurts.RemoveAt(i); continue; }
                Vector3 to = hurts[i].pos - p.transform.position;
                to.y = 0f;
                float ang = Vector3.SignedAngle(p.transform.forward, to, Vector3.up);
                GUIUtility.RotateAroundPivot(ang, new Vector2(cx, cy));
                Fill(new Rect(cx - 60f, cy - 230f, 120f, 14f), new Color(1f, 0.1f, 0.1f, 1f - age / 1.6f));
                GUI.matrix = mtx;
            }

            if (!p.Scoped && !p.dead) DrawCross(cx, cy, p);

            float ht = Time.unscaledTime - hitT;
            if (ht < 0.22f)
            {
                Color hc = hitKill ? new Color(1f, 0.2f, 0.2f) : (hitHead ? new Color(1f, 0.85f, 0.2f) : Color.white);
                hc.a = 1f - ht / 0.22f;
                for (int i = 0; i < 4; i++)
                {
                    GUIUtility.RotateAroundPivot(45f + 90f * i, new Vector2(cx, cy));
                    Fill(new Rect(cx + 10f, cy - 1.5f, 14f, 3f), hc);
                    GUI.matrix = mtx;
                }
            }

            // bar darah musuh
            if (p.cam != null && !p.dead)
            {
                float sc = Screen.height / 1080f;
                foreach (var e in Enemy.All)
                {
                    if (e.dead || e.hpBarT <= 0f) continue;
                    Vector3 wp = e.transform.position + Vector3.up * 2.3f * e.transform.localScale.y;
                    Vector3 sp = p.cam.WorldToScreenPoint(wp);
                    if (sp.z <= 0f) continue;
                    float gx = sp.x / sc, gy = (Screen.height - sp.y) / sc;
                    Fill(new Rect(gx - 41f, gy - 1f, 82f, 10f), new Color(0f, 0f, 0f, 0.7f));
                    Fill(new Rect(gx - 40f, gy, 80f * Mathf.Clamp01(e.hp / e.maxHp), 8f), e.type == EType.Boss ? new Color(1f, 0.3f, 0.9f) : new Color(1f, 0.25f, 0.2f));
                }
            }

            DrawRadar(vw, p);

            // kiri atas: skor
            Fill(new Rect(30f, 30f, 360f, 118f), new Color(0f, 0f, 0f, 0.45f));
            Fill(new Rect(30f, 30f, 6f, 118f), Orange);
            TxtS(new Rect(52f, 34f, 330f, 60f), "SKOR  " + score, St(40), Color.white);
            TxtS(new Rect(52f, 86f, 330f, 36f), "ELIMINASI  " + kills + (combo > 1 && Time.time - lastKill < 5f ? "   <color=#FFD633>x" + Mathf.Min(combo, 5) + " KOMBO</color>" : ""), St(24), new Color(1f, 1f, 1f, 0.9f));

            // tengah atas
            if (selMode == 0)
            {
                int left = Enemy.Alive() + queue.Count;
                TxtS(new Rect(cx - 250f, 24f, 500f, 50f), "GELOMBANG " + wave + " / " + MaxWave, St(38, TextAnchor.UpperCenter), Color.white);
                TxtS(new Rect(cx - 250f, 70f, 500f, 36f), intermission > 0f ? "Mulai dalam " + Mathf.CeilToInt(intermission) + " detik" : "MUSUH TERSISA: " + left, St(26, TextAnchor.UpperCenter), intermission > 0f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.5f, 0.4f));
            }
            else
            {
                int t = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
                TxtS(new Rect(cx - 250f, 24f, 500f, 60f), (t / 60).ToString("00") + ":" + (t % 60).ToString("00"), St(54, TextAnchor.UpperCenter), t < 30 ? new Color(1f, 0.4f, 0.3f) : Color.white);
                TxtS(new Rect(cx - 250f, 82f, 500f, 36f), "MUSUH AKTIF: " + Enemy.Alive(), St(24, TextAnchor.UpperCenter), new Color(1f, 0.6f, 0.5f));
            }

            // banner
            if (bnrT > 0f && bnr != "")
            {
                float a = Mathf.Clamp01(Mathf.Min(bnrT, bnrDur - bnrT) * 2f + 0.01f);
                TxtS(new Rect(cx - 600f, 230f, 1200f, 100f), bnr, St(76, TextAnchor.UpperCenter), new Color(1f, 0.6f, 0.15f, a));
                if (bnrSub != "") TxtS(new Rect(cx - 600f, 320f, 1200f, 50f), bnrSub, St(28, TextAnchor.UpperCenter, false), new Color(1f, 1f, 1f, a));
            }

            // info kill
            for (int i = feed.Count - 1; i >= 0; i--) if (Time.unscaledTime - feed[i].t > 4.5f) feed.RemoveAt(i);
            for (int i = 0; i < feed.Count; i++)
            {
                float age = Time.unscaledTime - feed[i].t;
                Color c = feed[i].col;
                c.a = Mathf.Clamp01(4.5f - age);
                TxtS(new Rect(vw - 520f, 280f + i * 34f, 490f, 32f), feed[i].text, St(24, TextAnchor.UpperRight), c);
            }

            // kiri bawah: darah & armor
            float bx = 50f, by = 930f;
            Fill(new Rect(bx - 12f, by - 14f, 470f, 136f), new Color(0f, 0f, 0f, 0.45f));
            float hpF = Mathf.Clamp01(p.health / 100f);
            TxtS(new Rect(bx, by - 4f, 70f, 30f), "DARAH", St(18), new Color(1f, 1f, 1f, 0.8f));
            Fill(new Rect(bx + 80f, by, 360f, 26f), new Color(0.15f, 0.05f, 0.05f, 0.9f));
            Fill(new Rect(bx + 80f, by, 360f * hpF, 26f), Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.3f, 0.9f, 0.3f), hpF));
            TxtS(new Rect(bx + 80f, by - 2f, 360f, 30f), Mathf.CeilToInt(p.health).ToString(), St(22, TextAnchor.MiddleCenter), Color.white);
            TxtS(new Rect(bx, by + 36f, 70f, 30f), "ARMOR", St(18), new Color(1f, 1f, 1f, 0.8f));
            Fill(new Rect(bx + 80f, by + 40f, 360f, 26f), new Color(0.05f, 0.08f, 0.15f, 0.9f));
            Fill(new Rect(bx + 80f, by + 40f, 360f * Mathf.Clamp01(p.armor / 100f), 26f), new Color(0.25f, 0.55f, 1f));
            TxtS(new Rect(bx + 80f, by + 38f, 360f, 30f), Mathf.CeilToInt(p.armor).ToString(), St(22, TextAnchor.MiddleCenter), Color.white);
            TxtS(new Rect(bx, by + 76f, 70f, 30f), "STAMINA", St(16), new Color(1f, 1f, 1f, 0.7f));
            Fill(new Rect(bx + 80f, by + 84f, 360f, 8f), new Color(0.2f, 0.2f, 0.1f, 0.9f));
            Fill(new Rect(bx + 80f, by + 84f, 360f * Mathf.Clamp01(p.stamina / 100f), 8f), new Color(1f, 0.85f, 0.25f));

            // kanan bawah: amunisi
            float ax = vw - 470f, ay = 900f;
            Fill(new Rect(ax, ay - 8f, 440f, 170f), new Color(0f, 0f, 0f, 0.45f));
            TxtS(new Rect(ax + 16f, ay, 420f, 36f), p.CurDef.name, St(26), Orange);
            Color ac = p.CurMag <= Mathf.Max(1, p.CurDef.mag / 4) ? new Color(1f, 0.4f, 0.3f) : Color.white;
            TxtS(new Rect(ax + 16f, ay + 30f, 200f, 90f), p.CurMag.ToString(), St(80, TextAnchor.UpperLeft), ac);
            TxtS(new Rect(ax + 170f, ay + 62f, 200f, 60f), "/ " + p.CurReserve, St(44, TextAnchor.UpperLeft), new Color(1f, 1f, 1f, 0.75f));
            TxtS(new Rect(ax + 300f, ay + 38f, 130f, 40f), "GRANAT x" + p.grenades, St(20, TextAnchor.UpperRight), new Color(0.6f, 1f, 0.6f));
            if (p.Reloading)
            {
                TxtS(new Rect(ax + 16f, ay + 122f, 420f, 30f), "MENGISI ULANG...", St(20), new Color(1f, 0.9f, 0.4f));
                Fill(new Rect(ax + 200f, ay + 132f, 220f, 8f), new Color(1f, 1f, 1f, 0.2f));
                Fill(new Rect(ax + 200f, ay + 132f, 220f * p.ReloadProgress, 8f), new Color(1f, 0.9f, 0.4f));
            }
            else if (p.CurMag == 0 && p.CurReserve == 0)
                TxtS(new Rect(ax + 16f, ay + 122f, 420f, 30f), "AMUNISI HABIS! (tekan V / ganti senjata)", St(18), new Color(1f, 0.3f, 0.3f));

            // slot senjata
            for (int i = 0; i < Weapons.All.Length; i++)
            {
                Rect r = new Rect(cx - 250f + i * 126f, 1015f, 120f, 44f);
                Fill(r, i == p.CurIndex ? new Color(0.9f, 0.45f, 0.05f, 0.9f) : new Color(0f, 0f, 0f, 0.5f));
                string[] sh = { "PISTOL", "RIFLE", "SHOTGUN", "SNIPER" };
                TxtS(r, (i + 1) + "  " + sh[i], St(18, TextAnchor.MiddleCenter), Color.white);
            }

            if (Time.unscaledTime - gameStartT < 9f && state == State.Playing)
                TxtS(new Rect(cx - 500f, 940f, 1000f, 40f), "WASD gerak  •  Klik kiri tembak  •  Klik kanan bidik  •  R isi ulang  •  1-4 senjata  •  G granat  •  V pisau", St(22, TextAnchor.UpperCenter, false), new Color(1f, 1f, 1f, 0.85f));
        }
    }
}
