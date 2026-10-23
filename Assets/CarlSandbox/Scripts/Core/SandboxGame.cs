using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Carl
{
    /// <summary>
    /// Entry point. Builds the whole sandbox at runtime when you press Play in any scene
    /// (add a GameObject named "NoCarlSandbox" to a scene to opt out), owns input and the HUD.
    /// </summary>
    public class SandboxGame : MonoBehaviour
    {
        public static SandboxGame Instance;

        public Camera cam;
        public PlayerController player;
        public WeaponController weapons;
        public XrayController xray;
        public MedicalPanel medical;
        public Menus menus;
        public int mapIndex;
        public Vector3 SpawnPoint { get; private set; }
        public const int MaxCarls = 100;

        Transform mapRoot;
        float playerYaw;
        bool slowMo, showHelp = true;
        readonly List<(string msg, float until)> toasts = new List<(string, float)>();
        GUIStyle hudCard, hudSmall, hudBig, hudCenter, hudBar, hudBarSel, hudLabel;
        Texture2D scopeTex;
        float fpsSmooth = 60f;
        const float BaseFixedDt = 0.01f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<SandboxGame>() != null) return;
            if (GameObject.Find("NoCarlSandbox") != null) return;
            new GameObject("CarlSandbox").AddComponent<SandboxGame>();
        }

        void Awake()
        {
            Instance = this;
            Time.fixedDeltaTime = BaseFixedDt;
            Time.timeScale = 1f;
            Physics.defaultMaxAngularSpeed = 50f;

            // turn off anything in the scene that would fight us (old cameras, test objects)
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go == gameObject) continue;
                if (go.GetComponentInChildren<Camera>() != null || go.name.StartsWith("MCP_")) go.SetActive(false);
            }
            SetupLighting();

            BloodSystem.Create();
            Ballistics.Create();
            Fx.Create();
            Sfx.Create();
            player = PlayerController.Create(Vector3.zero, 0f);
            PlayerBody.Attach(player.gameObject);
            PlayerAvatar.Attach(player);
            cam = player.cam;
            weapons = player.gameObject.AddComponent<WeaponController>();
            weapons.Setup(player);
            xray = gameObject.AddComponent<XrayController>();
            medical = gameObject.AddComponent<MedicalPanel>();
            gameObject.AddComponent<VisorView>();
            gameObject.AddComponent<MiniEarl>();
            MedKit.Attach(gameObject);
            SquadCommand.Attach(gameObject);
            menus = gameObject.AddComponent<Menus>();
            menus.Init(this);
            LoadMap(0);
            LockCursor(true);
            Toast("F SPAWN MENU · G GUNS · ESC SETTINGS · X X-RAY · MIDDLE CLICK MEDICAL");
        }

        void SetupLighting()
        {
            Light sun = null;
            foreach (var l in FindObjectsByType<Light>())
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.gameObject.SetActive(true);
            sun.transform.rotation = Quaternion.Euler(58f, -32f, 0f);
            sun.intensity = 1.05f;
            sun.color = new Color(1f, 0.98f, 0.95f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.58f, 0.63f);
            RenderSettings.ambientEquatorColor = new Color(0.47f, 0.49f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.32f, 0.34f);
            RenderSettings.fog = false;
        }

        public void LoadMap(int index)
        {
            WorldFreeze.Set(false);
            ClearAll();
            if (mapRoot != null) { mapRoot.gameObject.SetActive(false); Destroy(mapRoot.gameObject); }   // (gone now: the new map is laid out against what is there)
            mapIndex = index;
            mapRoot = MapBuilder.Build(index, out var spawn, out playerYaw, out var carls);
            Haze.Clear();
            Haze.ShelterHeight = MapBuilder.Names[index] == "TRENCHES" ? 1.45f : -1000f;
            Haze.PrepareCamera(player.cam);
            SpawnPoint = spawn;
            player.Teleport(spawn, playerYaw);
            foreach (var c in carls) SpawnCarl(c + Vector3.up * 0.02f, Random.Range(0f, 360f));
            TrenchSquad.Clear();
            if (MapBuilder.Names[index] == "TRENCHES")
            {
                Physics.SyncTransforms();
                int n = TrenchSquad.Populate(spawn);
                Toast($"{n} HOSTILE EARLS IN THE TRENCHES");
            }
            Toast("MAP: " + MapBuilder.Names[index]);
        }

        public HumanBody SpawnCarl(Vector3 feet, float yaw)
        {
            if (HumanBody.All.Count >= MaxCarls) { Toast("EARL LIMIT REACHED"); return null; }
            return HumanBody.Spawn(ClearSpot(feet), yaw);
        }

        static bool BodyFits(Vector3 feet)
        {
            foreach (var c in Physics.OverlapCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * 1.65f, 0.27f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c is CharacterController || c.GetComponentInParent<BodyPart>() != null) continue;
                return false;
            }
            return true;
        }

        /// <summary>A standing spot near `feet` where a body fits, on top of the floor (never inside walls or floors).</summary>
        public static Vector3 ClearSpot(Vector3 feet)
        {
            Vector3 g = GroundBelow(feet + Vector3.up * 1.2f, feet) + Vector3.up * 0.03f;
            if (BodyFits(g)) return g;
            for (int ring = 1; ring <= 6; ring++)
                for (int k = 0; k < 10; k++)
                {
                    Vector3 off = Quaternion.AngleAxis(k * 36f, Vector3.up) * Vector3.forward * (ring * 0.3f);
                    Vector3 p = GroundBelow(feet + off + Vector3.up * 1.2f, feet + off) + Vector3.up * 0.03f;
                    if (BodyFits(p)) return p;
                }
            return g;
        }

        void ClearAll()
        {
            medical?.Select(null);
            if (medical != null && medical.selected != null) medical.Select(medical.selected);
            foreach (var h in HumanBody.All.ToArray()) if (h && h != PlayerAvatar.Body) Destroy(h.gameObject);
            HumanBody.All.RemoveAll(h => h != PlayerAvatar.Body);
            TrenchSquad.Clear();
            Haze.Clear();
            foreach (var g in FindObjectsByType<Grenade>()) Destroy(g.gameObject);
            // torn-off limbs, the strands they hung by, and armour that came off
            foreach (var c in FindObjectsByType<LimbChunk>()) Destroy(c.gameObject);
            foreach (var t in FindObjectsByType<SkinThread>()) Destroy(t.gameObject);
            foreach (var a in FindObjectsByType<ArmourPiece>()) if (a.fallen) Destroy(a.gameObject);
            foreach (var gb in FindObjectsByType<Gib>()) Destroy(gb.gameObject);
            BloodSystem.Instance?.ClearAll();
            Ballistics.Instance?.ClearAll();
        }

        public void ClearAllPublic() { ClearAll(); Toast("CLEARED"); }

        public void Toast(string msg) => toasts.Add((msg, Time.unscaledTime + 3f));
        public void Shake(float amount) => player?.Shake(amount * 0.6f);

        void LockCursor(bool on)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (menus.IsOpen && menus.open != MenuKind.Settings) menus.Toggle(menus.open);
                else menus.Toggle(MenuKind.Settings);
            }
            if (kb.fKey.wasPressedThisFrame && menus.open != MenuKind.Settings) menus.Toggle(MenuKind.Spawn);
            // the spawn menu stops the world (you can still walk about and place things)
            WorldFreeze.Set(menus.open == MenuKind.Spawn);
            WorldFreeze.Tick();
            if (kb.gKey.wasPressedThisFrame && menus.open != MenuKind.Settings) menus.Toggle(MenuKind.Gun);
            if (!menus.IsOpen)
            {
                if (Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame && medical.selected == null) LockCursor(true);
                if (Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame && mouse.position.ReadValue().x > 520f) LockCursor(true);
            }
            // V (held) is the free camera: PlayerController
            if (kb.tKey.wasPressedThisFrame)
            {
                slowMo = !slowMo;
                Time.timeScale = slowMo ? 0.2f : 1f;
                Time.fixedDeltaTime = BaseFixedDt * Time.timeScale;
                Toast(slowMo ? "SLOW-MO 0.2x" : "TIME 1.0x");
            }
            if (kb.xKey.wasPressedThisFrame) { xray.Toggle(); Toast(xray.on ? "X-RAY ON" : "X-RAY OFF"); }
            if (kb.tabKey.wasPressedThisFrame) showHelp = !showHelp;
            // H: your own injuries - the same medical monitor as for an Earl
            if (kb.hKey.wasPressedThisFrame && PlayerAvatar.Body != null && !SquadCommand.Open) medical.Select(medical.selected == PlayerAvatar.Body ? null : PlayerAvatar.Body);
            // N: flip your own helmet's visor up or down
            if (kb.nKey.wasPressedThisFrame && !menus.IsOpen && PlayerAvatar.Body != null)
            {
                var vis = ArmourSystem.VisorOf(PlayerAvatar.Body, out _);
                if (vis != null && kb.leftShiftKey.isPressed)
                {
                    // SHIFT+N: rip the visor off the helmet (a smashed one you can't see through)
                    vis.FallOff(cam.transform.forward + Vector3.down * 0.5f);
                    Toast("VISOR TORN OFF"); Sfx.Play("impact_metal", cam.transform.position, 0.4f, 0f, 1f, 0.1f);
                }
                else if (vis != null) { vis.ToggleVisor(); Toast(vis.isUp ? "VISOR UP" : "VISOR DOWN"); Sfx.Play("impact_metal", cam.transform.position, 0.25f, 0f, 1f, 0.1f); }
            }
            if (kb.cKey.wasPressedThisFrame) { ClearAll(); Toast("CLEARED"); }
            // P: up goes the drone you last picked (G menu, MISC)
            if (kb.pKey.wasPressedThisFrame && !menus.IsOpen && FpvDrone.Piloting == null && Cursor.lockState == CursorLockMode.Locked) FpvDrone.Launch(FpvDrone.Selected);
            if (kb.f1Key.wasPressedThisFrame) LoadMap(0);
            if (kb.f2Key.wasPressedThisFrame) LoadMap(1);
            if (kb.f3Key.wasPressedThisFrame) LoadMap(2);
            if (kb.f4Key.wasPressedThisFrame && !kb.leftShiftKey.isPressed) LoadMap(3);
            if (kb.kKey.wasPressedThisFrame && kb.leftShiftKey.isPressed)
            {
                foreach (var h in HumanBody.All) if (!h.Dead && h != PlayerAvatar.Body) h.physio.Kill("ADMIN KILL", true);
            }
            if (SquadCommand.MiddleTapFrame == Time.frameCount) SelectUnderCrosshair();   // (held, it's the squad list)
            // debug: snap aim to the nearest Carl (F9 chest, F10 head, F11 thigh)
            if (kb.f8Key.wasPressedThisFrame)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                Vector3 p = player.transform.position + fwd * 3.5f;
                SpawnCarl(p + Vector3.up * 0.02f, Quaternion.LookRotation(-fwd).eulerAngles.y);
            }
            if (kb.f5Key.wasPressedThisFrame)
            {
                Vector3 back = -cam.transform.forward; back.y = 0f;
                player.LookAt(cam.transform.position + back.normalized * 5f + Vector3.down * 0.6f);
            }
            if (kb.f6Key.wasPressedThisFrame)
            {
                var target = NearestToCrosshair();
                if (target != null) StartCoroutine(DragTest(target));
            }
            if (kb.f4Key.wasPressedThisFrame && kb.leftShiftKey.isPressed) AutoTest.Run();
            if (kb.f7Key.wasPressedThisFrame)
            {
                var target = NearestToCrosshair();
                if (target != null) Grenade.Throw(target.Pelvis.transform.position + Vector3.right * 0.8f + Vector3.up * 0.3f, Vector3.zero).fuse = 1.2f;
            }
            if (kb.f9Key.wasPressedThisFrame) AimAtNearest(PartId.Chest);
            if (kb.f10Key.wasPressedThisFrame) AimAtNearest(PartId.Head);
            if (kb.f11Key.wasPressedThisFrame) AimAtNearest(PartId.ThighR);
            if (kb.f12Key.wasPressedThisFrame && kb.leftShiftKey.isPressed)
            {
                StartCoroutine(Burst());
            }
            else if (kb.f12Key.wasPressedThisFrame)
            {
                string dir = System.IO.Path.Combine(Application.dataPath, "..", "Screenshots");
                System.IO.Directory.CreateDirectory(dir);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"carl_{System.DateTime.Now:HHmmss}.png"));
            }
        }

        /// <summary>Debug: drags a body by the chest along a curved path to test drag marks.</summary>
        System.Collections.IEnumerator DragTest(HumanBody h)
        {
            h.ragdoll.Fall("drag test");
            var chest = h.Chest.rb;
            Vector3 c0 = chest.position;
            Vector3 center = c0 + Vector3.right * 1.6f;
            float t = 0f;
            while (t < 3f && h != null)
            {
                t += Time.fixedDeltaTime;
                float ang = Mathf.PI + t * 0.9f;
                Vector3 goal = center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 1.6f;
                goal.y = c0.y + 0.15f;
                Vector3 f = (goal - chest.position) * 400f - chest.linearVelocity * 30f;
                chest.AddForce(Vector3.ClampMagnitude(f, 1500f));
                yield return new WaitForFixedUpdate();
            }
        }

        System.Collections.IEnumerator Burst()
        {
            string dir = System.IO.Path.Combine(Application.dataPath, "..", "Screenshots", "burst");
            System.IO.Directory.CreateDirectory(dir);
            foreach (var f in System.IO.Directory.GetFiles(dir)) System.IO.File.Delete(f);
            for (int i = 0; i < 24; i++)
            {
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"f{i:00}.png"));
                yield return new WaitForSecondsRealtime(0.1f);
            }
        }

        HumanBody NearestToCrosshair()
        {
            HumanBody best = null; float bd = float.MaxValue;
            foreach (var h in HumanBody.All)
            {
                if (h == PlayerAvatar.Body) continue;
                float d = Vector3.Angle(cam.transform.forward, h.Center - cam.transform.position) + Vector3.Distance(h.Center, cam.transform.position) * 0.2f;
                if (d < bd) { bd = d; best = h; }
            }
            return best;
        }

        void AimAtNearest(PartId part)
        {
            var best = NearestToCrosshair();
            if (best == null) return;
            player.LookAt(best[part].transform.position);
        }

        void SpawnAtCrosshair(int i)
        {
            Vector3 origin = cam.transform.position;
            Vector3 dir = Quaternion.AngleAxis(i * 9f - (i > 0 ? 18f : 0f), Vector3.up) * cam.transform.forward;
            Vector3 point;
            if (Physics.Raycast(origin, dir, out var hit, 250f, ~0, QueryTriggerInteraction.Ignore) && !(hit.collider is CharacterController))
                point = hit.normal.y > 0.6f ? hit.point : hit.point + hit.normal * 0.6f;
            else point = origin + dir * 6f;
            // never spawn inside the player (overlap gets resolved by pushing the body through the floor)
            Vector3 flatToPoint = point - player.transform.position; flatToPoint.y = 0f;
            if (flatToPoint.magnitude < 1.3f)
            {
                Vector3 fwd = cam.transform.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f) fwd = player.transform.forward;
                point = player.transform.position + fwd.normalized * 1.6f + Vector3.up * 0.5f;
            }
            point = GroundBelow(point + Vector3.up * 1.5f, point);
            Vector3 face = cam.transform.position - point; face.y = 0f;
            float yaw = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face).eulerAngles.y : 0f;
            if (SpawnCarl(point + Vector3.up * 0.02f, yaw) != null) Toast($"EARL SPAWNED ({HumanBody.All.Count}/{MaxCarls})");
        }

        /// <summary>First static surface below `from`, ignoring the player and bodies.</summary>
        public static Vector3 GroundAt(Vector3 p) => GroundBelow(p + Vector3.up * 1.5f, p);
        public System.Collections.IEnumerator DragTestRoutine(HumanBody h) => DragTest(h);

        static Vector3 GroundBelow(Vector3 from, Vector3 fallback)
        {
            var hits = Physics.RaycastAll(from, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Vector3 p = fallback;
            foreach (var h in hits)
            {
                if (h.collider is CharacterController || h.collider.GetComponent<BodyPart>() != null) continue;
                if (h.distance < best) { best = h.distance; p = h.point; }
            }
            return p;
        }

        void SelectUnderCrosshair()
        {
            var hits = Physics.RaycastAll(cam.transform.position, cam.transform.forward, 300f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; HumanBody h = null; bool blocked = false;
            foreach (var hit in hits)
            {
                if (hit.collider is CharacterController || hit.distance >= best) continue;
                if (PlayerAvatar.Body != null && hit.collider.GetComponentInParent<HumanBody>() == PlayerAvatar.Body) continue;   // your own head
                best = hit.distance;
                var bp = hit.collider.GetComponent<BodyPart>();
                h = bp != null ? bp.body : null;
                blocked = bp == null;
            }
            if (h == null && Physics.SphereCast(cam.transform.position + cam.transform.forward * 0.5f, 0.35f, cam.transform.forward, out var sh, 300f, ~0, QueryTriggerInteraction.Ignore))
            {
                var bp = sh.collider.GetComponent<BodyPart>();
                if (bp != null) h = bp.body;
            }
            if (h == null)
            {
                if (medical.selected != null) medical.Select(medical.selected); // close
                return;
            }
            medical.Select(h);
        }

        // =========================================================== HUD

        void InitHud()
        {
            if (hudSmall != null) return;
            hudSmall = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.95f, 0.96f, 0.97f) } };
            hudBig = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight, normal = { textColor = Color.white } };
            hudCenter = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            var bt = Menus.Rounded(new Color(0.06f, 0.07f, 0.085f, 0.72f), new Color(1f, 1f, 1f, 0.07f), 7);
            var bs = Menus.Rounded(new Color(0.96f, 0.56f, 0.16f, 0.95f), new Color(1f, 0.85f, 0.6f, 0.5f), 7);
            hudBar = new GUIStyle(GUI.skin.box) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, border = new RectOffset(7, 7, 7, 7), normal = { background = bt, textColor = new Color(0.78f, 0.8f, 0.84f) } };
            hudBarSel = new GUIStyle(hudBar) { normal = { background = bs, textColor = new Color(0.08f, 0.06f, 0.04f) } };
            var lt = Menus.Rounded(new Color(0.06f, 0.07f, 0.085f, 0.85f), new Color(1f, 1f, 1f, 0.07f), 6);
            hudLabel = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft, border = new RectOffset(6, 6, 6, 6), padding = new RectOffset(18, 8, 3, 3), normal = { background = lt, textColor = Color.white } };
            hudCard = new GUIStyle { normal = { background = Menus.Rounded(new Color(0.06f, 0.07f, 0.085f, 0.78f), new Color(1f, 1f, 1f, 0.07f), 10) }, border = new RectOffset(10, 10, 10, 10) };

            int s = 512;
            scopeTex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(s / 2f, s / 2f)) / (s / 2f);
                    float a = Mathf.Clamp01((d - 0.96f) * 30f);
                    bool line = (Mathf.Abs(x - s / 2) < 1 || Mathf.Abs(y - s / 2) < 1) && d < 0.96f && d > 0.03f;
                    scopeTex.SetPixel(x, y, line ? new Color(0, 0, 0, 0.9f) : new Color(0, 0, 0, a));
                }
            scopeTex.Apply();
        }

        void OnGUI()
        {
            DrawHud();
            if (menus != null) menus.Draw();
        }

        void DrawHud()
        {
            InitHud();
            float W = Screen.width, H = Screen.height;

            // flying a drone: its goggles draw their own picture, none of yours
            bool drone = FpvDrone.Piloting != null;
            if (drone) { }
            else if (weapons != null && weapons.Scoped)
            {
                float size = H;
                float x0 = (W - size) / 2f;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, x0 + 1, H), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x0 + size - 1, 0, x0 + 1, H), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(x0, 0, size, size), scopeTex);
            }
            else if (GameSettings.Crosshair)
            {
                // crosshair (fades out while aiming down the sights, so it never sits on top of them)
                float aimFade = weapons != null && weapons.Current.type == WeaponType.Gun ? Mathf.Clamp01(1f - weapons.AimAmount * 2.5f) : 1f;
                GUI.color = new Color(1, 1, 1, 0.85f * aimFade);
                float c = 5f;
                // the crosshair sits where the gun actually points: it wanders as your arms sway, shake and drag
                Vector2 cp = new Vector2(W / 2, H / 2);
                if (weapons != null && weapons.Armed && cam != null)
                {
                    Vector3 sp = cam.WorldToScreenPoint(weapons.AimPoint);
                    if (sp.z > 0f) cp = new Vector2(sp.x, H - sp.y);
                }
                GUI.DrawTexture(new Rect(cp.x - 1, cp.y - c - 4, 2, c), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cp.x - 1, cp.y + 4, 2, c), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cp.x - c - 4, cp.y - 1, c, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cp.x + 4, cp.y - 1, c, 2), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // top bar
            GUI.Label(new Rect(W / 2 - 80, 8, 160, 20), $"EARLS  {HumanBody.All.Count - (PlayerAvatar.Body != null ? 1 : 0):00} / {MaxCarls}", hudCenter);
            fpsSmooth = Mathf.Lerp(fpsSmooth, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.05f);
            GUI.Label(new Rect(W - 120, 8, 110, 20), $"TIME  {Time.timeScale:0.0}x", hudSmall);
            GUI.Label(new Rect(W - 260, 8, 120, 20), $"{fpsSmooth:0} FPS", hudSmall);
            GUI.Label(new Rect(W - 260, 26, 250, 20), MapBuilder.Names[mapIndex] + (player.flying ? "  ·  FLY" : ""), hudSmall);

            // toasts
            toasts.RemoveAll(t => t.until < Time.unscaledTime);
            float ty = 30f;
            foreach (var t in toasts)
            {
                GUI.Label(new Rect(W / 2 - 300, ty, 600, 20), t.msg, hudCenter);
                ty += 18f;
            }

            // x-ray hover label
            if (xray.on)
            {
                // scanner overlay: blue tint at the edges and a slow sweep line
                GUI.color = new Color(0.2f, 0.6f, 1f, 0.07f);
                GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
                float sy = (Time.unscaledTime * 0.35f % 1f) * H;
                GUI.color = new Color(0.4f, 0.85f, 1f, 0.12f);
                GUI.DrawTexture(new Rect(0, sy, W, 3), Texture2D.whiteTexture);
                GUI.color = new Color(0.4f, 0.85f, 1f, 0.05f);
                GUI.DrawTexture(new Rect(0, sy - 30, W, 30), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            if (xray.on && !string.IsNullOrEmpty(xray.hoverLabel))
            {
                var r = new Rect(W / 2 + 14, H / 2 + 10, 340, 22);
                GUI.Label(r, xray.hoverLabel, hudLabel);
                GUI.color = xray.hoverColor;
                GUI.DrawTexture(new Rect(r.x + 6, r.y + 7, 8, 8), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // weapon bar + ammo
            float bw = 92f, bh = 28f;
            float bx = 16f, by = H - bh - 14f;
            if (drone) return;
            if (medical.selected == null && (menus == null || !menus.IsOpen))
            {
                int shown = 0; float x = bx;
                for (int i = 0; i < weapons.defs.Count; i++)
                {
                    if (!weapons.owned[i]) continue;
                    // each slot as wide as its name needs
                    var text = new GUIContent($"{i + 1}  {weapons.defs[i].name}");
                    float wdt = Mathf.Max(bw, hudBar.CalcSize(text).x + 18f);
                    GUI.Label(new Rect(x, by, wdt, bh), text, i == weapons.current ? hudBarSel : hudBar);
                    x += wdt + 4f; shown++;
                }
                if (shown == 0) GUI.Label(new Rect(bx, by, 300, bh), "NO WEAPONS  ·  G: take some from the arsenal", hudBar);
                GUI.Label(new Rect(bx, by - 40, 600, 20), weapons.Grabbing ? "GRABBING — RELEASE LEFT CLICK · RIGHT CLICK THROW · SCROLL DISTANCE" : "Q / E LEAN", hudSmall);
            }
            GUI.Box(new Rect(W - 230, H - 96, 214, 82), GUIContent.none, hudCard);
            GUI.Label(new Rect(W - 330, H - 64, 300, 50), weapons.AmmoText, hudBig);
            string fm = weapons.Armed ? weapons.FireMode(weapons.current) : "";
            GUI.Label(new Rect(W - 330, H - 88, 300, 20), weapons.Armed ? weapons.Current.name + (fm != "" ? "  ·  " + fm : "") : "", new GUIStyle(hudSmall) { alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.96f, 0.56f, 0.16f) } });

            if (showHelp && medical.selected == null && (menus == null || !menus.IsOpen))
            {
                string help =
                    "<b><color=#F58F29>CONTROLS</color></b>\n" +
                    "WASD move · SHIFT sprint · SPACE jump (hold: get up) · CTRL crouch · Y drop down · Z fire mode\n" +
                    "LMB fire / stab / swing · RMB aim / slash · R reload · B point aim on/off (RMB with it: raise it, no sights) · ALT hold: swing blade\n" +
                    "1-9 or wheel: weapons (take them in the G menu; BANDAGE / SURGERY KIT: LMB treat a wound)\n" +
                    "F spawn menu  ·  G gun menu  ·  V hold: free camera  ·  T slow-mo  ·  K end it\n" +
                    "X real-time x-ray  ·  MIDDLE CLICK medical panel  ·  L flashlight  ·  J laser  ·  P fly a drone (pick one in G, MISC)\n" +
                    "Friendlies (green armband): look at one, HOLD R to recruit  ·  HOLD MIDDLE MOUSE: squad orders\n" +
                    "PHYSICS GUN (always on you): hold LMB grab/drag · RMB throw · rub bodies on walls  ·  Q / E hold: lean\n" +
                    "F1 The Lab  ·  F2 The Yard  ·  F3 Houses  ·  F4 Trenches  ·  C clear  ·  SHIFT+K kill all\n" +
                    "H your injuries  ·  N visor up/down  ·  TAB hide help  ·  ESC free mouse";
                GUI.Box(new Rect(12, 12, 620, 196), GUIContent.none, hudCard);
                GUI.Label(new Rect(26, 22, 600, 180), help, new GUIStyle(hudSmall) { richText = true, wordWrap = true, fontSize = 12 });
            }
        }
    }
}
