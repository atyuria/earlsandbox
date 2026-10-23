using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Carl
{
    /// <summary>
    /// The RADIO (G menu, MISC): hold it and LEFT CLICK (or press M while you carry it) and a picture of the map from
    /// straight above comes up. A circle follows the mouse over it; pick what you want and click.
    ///   1  BOMBING RUN   a jet comes over high and lays a stick of six heavy bombs along the arrow
    ///   2  GUN RUN       a jet dives on the spot and walks a long burst of cannon fire (some of it high explosive)
    ///                    across it along the arrow, then pulls out
    ///   3  ARTILLERY     about twenty seconds of shells landing all over the circle
    ///   MOUSE move the circle · SCROLL zoom · WASD move the map · Q / E turn the arrow · LEFT CLICK call it ·
    ///   ESC / M back out
    /// Your body stands where you left it while you're on the map. A red smoke goes down on the mark and it arrives
    /// some seconds later; each needs a while before it can be called again. It hits what's in the circle, mates and
    /// you included: the map shows you and your side so you can see how close you're calling it.
    /// </summary>
    public class Radio : MonoBehaviour
    {
        public static Radio I;
        public static bool Open => I != null && I.open;
        /// <summary>The frame the map last closed (that ESC or click mustn't also do something else).</summary>
        public static int ClosedFrame = -10;
        public static void Attach(GameObject g) => I = g.AddComponent<Radio>();

        public enum Strike { Bombs, GunRun, Artillery, Missile }
        bool open;
        Strike pick;
        readonly float[] ready = new float[4];
        GUIStyle card, title, line, lineOn, small, tag;
        static readonly float[] Cooldown = { 80f, 40f, 90f, 30f };
        static readonly string[] Names = { "BOMBING RUN (6 BOMBS)", "GUN RUN (CANNON)", "ARTILLERY (20 S)", "MISSILE (PRECISE)" };
        /// <summary>How big the circle is on the ground (m): about what each covers.</summary>
        static readonly float[] Radius = { 20f, 14f, 24f, 5f };

        // the map (MapPainter draws it): the bit of it in view
        Vector3 centre;
        float size = 70f, runYaw, openedAt;
        static Texture2D ring, dot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; ClosedFrame = -10; }

        void Awake() { useGUILayout = false; }

        /// <summary>A new map: everything ready again.</summary>
        public void Clear() { if (open) Close(); for (int i = 0; i < ready.Length; i++) ready[i] = 0f; }

        static WeaponController W => SandboxGame.Instance != null ? SandboxGame.Instance.weapons : null;

        /// <summary>Do you carry the radio (taken in the G menu, MISC)?</summary>
        static bool Carried(out bool inHand)
        {
            inHand = false;
            var w = W;
            if (w == null || w.owned == null) return false;
            int i = w.defs.FindIndex(WeaponController.IsRadio);
            if (i < 0 || !w.owned[i]) return false;
            inHand = w.Armed && w.current == i;
            return true;
        }

        void Update()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            var g = SandboxGame.Instance;
            var me = PlayerAvatar.Body;
            bool can = g != null && !g.InMenu && g.player != null && !g.player.flying && FpvDrone.Piloting == null
                       && (g.menus == null || !g.menus.IsOpen) && !MedKit.Busy && !SquadCommand.Open
                       && (me == null || !me.Dead && me.physio.consciousness > 0.5f);
            if (!can) { if (open) Close(); return; }
            // the map is drawn in the background a moment after it loads, ready for when you want it
            if (g.MapRoot != null && Time.timeSinceLevelLoad > 0f) MapPainter.Ensure(this, g.MapRoot);
            if (kb == null || mouse == null) return;
            if (!open)
            {
                bool carried = Carried(out bool inHand);
                bool locked = Cursor.lockState == CursorLockMode.Locked && !Menus.Blocking;
                bool ask = kb.mKey.wasPressedThisFrame || inHand && locked && mouse.leftButton.wasPressedThisFrame;
                if (!ask) return;
                if (!carried) { g.Toast("NO RADIO  ·  TAKE ONE IN THE G MENU (MISC)"); return; }
                OpenMap();
                return;
            }
            Menus.LastMenuFrame = Time.frameCount;     // (the clicks and keys are for the map, not the gun)
            if (kb.escapeKey.wasPressedThisFrame || kb.mKey.wasPressedThisFrame) { Close(); return; }
            if (kb.digit1Key.wasPressedThisFrame) pick = Strike.Bombs;
            if (kb.digit2Key.wasPressedThisFrame) pick = Strike.GunRun;
            if (kb.digit3Key.wasPressedThisFrame) pick = Strike.Artillery;
            if (kb.digit4Key.wasPressedThisFrame) pick = Strike.Missile;
            float dt = Time.unscaledDeltaTime;
            // pan, zoom, turn the run
            Vector3 pan = Vector3.zero;
            if (kb.wKey.isPressed) pan.z += 1f;
            if (kb.sKey.isPressed) pan.z -= 1f;
            if (kb.dKey.isPressed) pan.x += 1f;
            if (kb.aKey.isPressed) pan.x -= 1f;
            centre += pan * size * 1.2f * dt * (kb.leftShiftKey.isPressed ? 2.5f : 1f);
            float sc = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(sc) > 0.1f) size = Mathf.Clamp(size * (sc > 0f ? 0.88f : 1.14f), 25f, 220f);
            if (kb.qKey.isPressed) runYaw -= 110f * dt;
            if (kb.eKey.isPressed) runYaw += 110f * dt;
            // (a moment's grace: the click that opened the map isn't also the one that calls it in)
            if (Time.unscaledTime - openedAt < 0.25f) return;
            if (mouse.leftButton.wasPressedThisFrame) TryCall(pick);
        }

        void OpenMap()
        {
            var g = SandboxGame.Instance;
            open = true;
            openedAt = Time.unscaledTime;
            var eye = g.player.cam.transform;
            centre = (PlayerAvatar.Body != null ? PlayerAvatar.Body.Center : eye.position) + Vector3.ProjectOnPlane(eye.forward, Vector3.up).normalized * 25f;
            runYaw = eye.eulerAngles.y;
            MapPainter.Ensure(this, g.MapRoot);
            g.player.cam.enabled = false;     // (your ears stay where you are)
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Menus.LastMenuFrame = Time.frameCount;
            Sfx.Play("click_empty", Ears, 0.4f, 0f, 1f, 0.05f);
        }

        void Close()
        {
            open = false;
            ClosedFrame = Time.frameCount;
            Menus.LastMenuFrame = Time.frameCount;
            var g = SandboxGame.Instance;
            if (g != null && g.player != null && FpvDrone.Piloting == null) g.player.cam.enabled = true;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }

        static Vector3 Ears => Camera.main != null ? Camera.main.transform.position : Vector3.zero;

        /// <summary>How many screen pixels a metre of ground is at this zoom.</summary>
        float Ppm => Screen.height / (size * 2f);

        /// <summary>The ground under the mouse on the map.</summary>
        bool UnderMouse(out Vector3 at)
        {
            at = Vector3.zero;
            if (Mouse.current == null) return false;
            Vector2 m = Mouse.current.position.ReadValue();     // (from the bottom left)
            float x = centre.x + (m.x - Screen.width * 0.5f) / Ppm, z = centre.z + (m.y - Screen.height * 0.5f) / Ppm;
            at = new Vector3(x, centre.y, z);
            // down onto whatever is there (not the top of someone's head)
            var hits = Physics.RaycastAll(new Vector3(x, centre.y + 300f, z), Vector3.down, 700f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.collider is CharacterController) continue;
                if (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic) continue;
                if (h.distance < best) { best = h.distance; at = h.point; }
            }
            return true;
        }

        Vector3 Along => Quaternion.Euler(0f, runYaw, 0f) * Vector3.forward;

        void TryCall(Strike s)
        {
            var g = SandboxGame.Instance;
            float r = ready[(int)s];
            if (Time.time < r) { g.Toast($"NEGATIVE, NOTHING FREE FOR THAT  ·  {r - Time.time:0} S"); Sfx.Play("click_empty", Ears, 0.4f, 0f, 1f, 0.05f); return; }
            if (!UnderMouse(out var at)) return;
            Call(s, at, Along);
            Close();
        }

        /// <summary>Calls it in on a spot, the jet flying in along a direction (the map, or a playtest).</summary>
        public void Call(Strike s, Vector3 at, Vector3 along)
        {
            var g = SandboxGame.Instance;
            // (with INFINITE AMMO there's always another aircraft, another battery: no wait at all)
            ready[(int)s] = GameSettings.InfiniteAmmo ? 0f : Time.time + Cooldown[(int)s];
            float delay;
            if (s == Strike.Artillery)
            {
                delay = Random.Range(9f, 12f);
                Barrage.Fire(at, Radius[2], delay, 20f);
                MarkerSmoke.Drop(at, delay + 4f);
            }
            else if (s == Strike.Missile)
            {
                delay = Random.Range(5f, 6f);
                Missile.Inbound(at, along, delay);
                MarkerSmoke.Drop(at, delay + 3f);
            }
            else
            {
                delay = Random.Range(7f, 9f);
                Jet.Inbound(s, at, along, delay);
                MarkerSmoke.Drop(at, delay + 6f);
            }
            float dist = PlayerAvatar.Body != null ? Vector3.Distance(PlayerAvatar.Body.Center, at) : 999f;
            string who = s == Strike.Bombs ? "VIPER 1-1: COPY, BOMBS" : s == Strike.GunRun ? "HOG 2-1: COPY, GUN RUN" : s == Strike.Missile ? "REAPER 3-1: COPY, ONE MISSILE" : "STEEL RAIN: SHOT OVER, SPLASH";
            g?.Toast($"{who} ON THE RED SMOKE, {Mathf.RoundToInt(delay)} SECONDS" + (dist < Radius[(int)s] + 20f ? "  ·  DANGER CLOSE" : ""));
            Sfx.Play("reload_magin", Ears, 0.4f, 0f, 1f, 0.05f);
        }

        // ------------------------------------------------------------ the map

        static Texture2D Ring()
        {
            if (ring != null) return ring;
            const int S = 256;
            ring = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f, r = Mathf.Sqrt(u * u + v * v);
                    float edge = Mathf.Clamp01(1f - Mathf.Abs(r - 0.97f) / 0.018f);
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(edge * 255f));
                }
            ring.SetPixels32(px); ring.Apply(false);
            return ring;
        }

        static Texture2D Dot()
        {
            if (dot != null) return dot;
            const int S = 32;
            dot = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f;
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01((1f - Mathf.Sqrt(u * u + v * v)) * 6f) * 255f));
                }
            dot.SetPixels32(px); dot.Apply(false);
            return dot;
        }

        /// <summary>Where a point on the ground is on the screen (north up).</summary>
        Vector2 ToScreen(Vector3 world)
        {
            Vector3 d = world - centre;
            return new Vector2(Screen.width * 0.5f + d.x * Ppm, Screen.height * 0.5f - d.z * Ppm);
        }

        static void Line(Vector2 a, Vector2 b, float w)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.5f) return;
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
            GUI.DrawTexture(new Rect(a.x, a.y - w * 0.5f, len, w), Texture2D.whiteTexture);
            GUI.matrix = m;
        }

        void Marker(Vector3 world, Color c, float px, string label = null)
        {
            var p = ToScreen(world);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(p.x - px / 2 - 2, p.y - px / 2 - 2, px + 4, px + 4), Dot());
            GUI.color = c;
            GUI.DrawTexture(new Rect(p.x - px / 2, p.y - px / 2, px, px), Dot());
            GUI.color = Color.white;
            if (label != null) GUI.Label(new Rect(p.x + px / 2 + 4, p.y - 9, 200, 18), label, tag);
        }

        void OnGUI()
        {
            if (!open || Event.current.type != EventType.Repaint) return;
            GUI.depth = -30;
            if (card == null)
            {
                card = new GUIStyle { normal = { background = Menus.Rounded(new Color(0.07f, 0.09f, 0.07f, 0.92f), new Color(0.6f, 1f, 0.6f, 0.15f), 10) }, border = new RectOffset(10, 10, 10, 10) };
                title = new GUIStyle { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.6f, 1f, 0.65f) } };
                line = new GUIStyle { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.75f, 0.8f, 0.75f) } };
                lineOn = new GUIStyle(line) { normal = { textColor = new Color(1f, 0.85f, 0.35f) } };
                small = new GUIStyle { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.65f, 0.72f, 0.65f) } };
                tag = new GUIStyle { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            }
            float W = Screen.width, H = Screen.height;
            // the map: the painted battlefield, the bit of it in view (bare earth past its edges)
            GUI.color = new Color(0.42f, 0.35f, 0.28f);
            GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (!MapPainter.Ready || MapPainter.Tex == null)
            {
                GUI.Label(new Rect(0, H * 0.5f - 12, W, 24), $"PLOTTING THE MAP…  {MapPainter.Progress * 100f:0}%", new GUIStyle(title) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });
                return;
            }
            var wr = MapPainter.World;
            float hw = size * W / H;
            var uv = new Rect((centre.x - hw - wr.x) / wr.width, (centre.z - size - wr.y) / wr.height, hw * 2f / wr.width, size * 2f / wr.height);
            // (only the part the picture covers, so its edges don't smear out)
            Rect vis = Rect.MinMaxRect(Mathf.Max(uv.xMin, 0f), Mathf.Max(uv.yMin, 0f), Mathf.Min(uv.xMax, 1f), Mathf.Min(uv.yMax, 1f));
            if (vis.width > 0f && vis.height > 0f)
            {
                var sr = Rect.MinMaxRect((vis.xMin - uv.xMin) / uv.width * W, (1f - (vis.yMax - uv.yMin) / uv.height) * H, (vis.xMax - uv.xMin) / uv.width * W, (1f - (vis.yMin - uv.yMin) / uv.height) * H);
                GUI.DrawTextureWithTexCoords(sr, MapPainter.Tex, vis);
            }
            // a faint frame round the edge, like paper
            GUI.color = new Color(0.12f, 0.1f, 0.08f, 0.5f);
            GUI.DrawTexture(new Rect(0, 0, W, 6), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(0, H - 6, W, 6), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 0, 6, H), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(W - 6, 0, 6, H), Texture2D.whiteTexture);
            GUI.color = Color.white;
            // you, and your side
            foreach (var g in EarlGunner.All)
                if (g != null && g.team == Teams.Friendly && g.Fighting && g.Body != null) Marker(g.Body.Center, new Color(0.35f, 1f, 0.45f, 1f), 9f);
            if (PlayerAvatar.Body != null) Marker(PlayerAvatar.Body.Center, new Color(0.3f, 0.75f, 1f, 1f), 14f, "YOU");
            // the circle, and (for the aircraft) the arrow they fly in along
            if (UnderMouse(out var at))
            {
                var c = ToScreen(at);
                float ppm = Ppm;
                float r = Radius[(int)pick] * ppm;
                float dYou = PlayerAvatar.Body != null ? Vector3.Distance(PlayerAvatar.Body.Center, at) : 999f;
                bool close = dYou < Radius[(int)pick] + 20f;
                var col = close ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(1f, 0.25f, 0.2f, 1f);
                GUI.color = new Color(0f, 0f, 0f, 0.5f);
                GUI.DrawTexture(new Rect(c.x - r - 2, c.y - r - 2, r * 2f + 4, r * 2f + 4), Ring());
                GUI.color = col;
                GUI.DrawTexture(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), Ring());
                Line(c + new Vector2(-7, 0), c + new Vector2(7, 0), 2f);
                Line(c + new Vector2(0, -7), c + new Vector2(0, 7), 2f);
                if (pick != Strike.Artillery)
                {
                    Vector3 a = Along;
                    Vector2 dir = new Vector2(a.x, -a.z).normalized;
                    float half = r + 30f;
                    Vector2 tail = c - dir * half, tip = c + dir * half;
                    Line(tail, c - dir * (r + 4f), 3f);
                    Line(c + dir * (r + 4f), tip, 3f);
                    Vector2 side = new Vector2(-dir.y, dir.x);
                    Line(tip, tip - dir * 14f + side * 9f, 3f);
                    Line(tip, tip - dir * 14f - side * 9f, 3f);
                }
                GUI.color = Color.white;
                GUI.Label(new Rect(c.x + r + 10, c.y - 9, 260, 18), $"{dYou:0} M FROM YOU" + (close ? "  ·  DANGER CLOSE" : ""), tag);
            }
            // the card: what to call, what's ready, the keys
            var cr = new Rect(W - 390, 20, 370, 218);
            GUI.Box(cr, GUIContent.none, card);
            GUI.Label(new Rect(cr.x + 16, cr.y + 12, 340, 22), "RADIO  ·  CALL FOR FIRE", title);
            for (int i = 0; i < 4; i++)
            {
                float left = Mathf.Max(0f, ready[i] - Time.time);
                GUI.Label(new Rect(cr.x + 16, cr.y + 42 + i * 22, 340, 20), $"{i + 1}   {Names[i]}" + (left > 0f ? $"  ·  {left:0} S" : "  ·  READY") + ((int)pick == i ? "   <" : ""), (int)pick == i ? lineOn : line);
            }
            GUI.Label(new Rect(cr.x + 16, cr.y + 138, 340, 80), "1 - 4 pick  ·  LEFT CLICK call it on the circle\nMOUSE move the circle  ·  SCROLL zoom  ·  WASD move the map  ·  Q / E turn the arrow (the way the jet flies in)\nESC or M back out", small);
        }
    }

    /// <summary>The red smoke on the mark (so you, and the pilot, can see where it's going).</summary>
    public class MarkerSmoke : MonoBehaviour
    {
        float left, next;
        public static void Drop(Vector3 at, float seconds)
        {
            var m = new GameObject("MarkerSmoke").AddComponent<MarkerSmoke>();
            m.transform.position = at + Vector3.up * 0.1f;
            m.left = seconds;
            if (SandboxGame.Instance != null && SandboxGame.Instance.MapRoot != null) m.transform.SetParent(SandboxGame.Instance.MapRoot, true);
        }

        void Update()
        {
            left -= Time.deltaTime;
            if (left <= 0f) { Destroy(gameObject); return; }
            if (Time.time < next) return;
            next = Time.time + 0.12f;
            Haze.Puff(transform.position + Random.insideUnitSphere * 0.2f, Vector3.up * Random.Range(1.2f, 2.2f) + Random.insideUnitSphere * 0.4f, Random.Range(0.4f, 0.8f), 0.5f, new Color(0.85f, 0.15f, 0.12f), true);
        }
    }

    /// <summary>A strike aircraft on one pass: in, the bombs or the gun, and away.</summary>
    public class Jet : MonoBehaviour
    {
        Radio.Strike kind;
        Vector3 target, along, start;
        float s, speed, wait, fired, nextRound;
        bool bombed;
        const int Bombs = 6;
        int bombsLeft = Bombs;
        AudioSource roar, gun;
        Vector3 lastPos;
        const float RunIn = 1900f;

        static AudioClip roarClip, gunClip;

        public static void Inbound(Radio.Strike k, Vector3 target, Vector3 along, float delay)
        {
            var go = new GameObject(k == Radio.Strike.Bombs ? "Jet (bombs)" : "Jet (gun run)");
            var j = go.AddComponent<Jet>();
            j.kind = k; j.target = target; j.along = along; j.wait = delay;
            j.speed = k == Radio.Strike.Bombs ? 170f : 140f;
            // it times its run so it's over the mark when it said it would be
            j.s = -(delay * j.speed - RunIn);
            j.start = target - along * RunIn;
            j.Build();
            j.transform.position = j.PathAt(j.s);
            j.lastPos = j.transform.position;
        }

        /// <summary>Where it is when it has come d metres along its run (the mark is at RunIn).</summary>
        Vector3 PathAt(float d)
        {
            float toGo = RunIn - d;
            float alt;
            if (kind == Radio.Strike.Bombs) alt = 150f;
            // the gun run: a long shallow dive down to the mark, then the pull-out and climb away
            else alt = toGo > 160f ? 45f + (toGo - 160f) * 0.13f : 45f + (160f - toGo) * 0.3f;
            Vector3 p = start + along * d;
            p.y = target.y + alt;
            return p;
        }

        void Build()
        {
            var grey = Mats.Lit(new Color(0.42f, 0.45f, 0.47f), 0.45f, 0.2f);
            var dark = Mats.Lit(new Color(0.18f, 0.19f, 0.2f), 0.5f);
            var glass = Mats.Lit(new Color(0.15f, 0.2f, 0.25f), 0.9f);
            Part(PrimitiveType.Capsule, Vector3.zero, new Vector3(1.6f, 8f, 1.6f), Quaternion.Euler(90f, 0f, 0f), grey);   // fuselage
            Part(PrimitiveType.Sphere, new Vector3(0f, 0.6f, 3.6f), new Vector3(0.9f, 0.7f, 2.2f), Quaternion.identity, glass);   // canopy
            Part(PrimitiveType.Cube, new Vector3(0f, 0f, 0.3f), new Vector3(13f, 0.25f, 3f), Quaternion.identity, grey);   // wings
            Part(PrimitiveType.Cube, new Vector3(0f, 0.2f, -6.5f), new Vector3(5.5f, 0.2f, 1.6f), Quaternion.identity, grey);   // tailplane
            Part(PrimitiveType.Cube, new Vector3(0f, 1.6f, -6.4f), new Vector3(0.25f, 3f, 2f), Quaternion.Euler(-20f, 0f, 0f), grey);   // fin
            Part(PrimitiveType.Cylinder, new Vector3(0f, 0f, -8f), new Vector3(1.1f, 0.4f, 1.1f), Quaternion.Euler(90f, 0f, 0f), dark);   // exhaust
            for (int i = -1; i <= 1; i += 2) Part(PrimitiveType.Capsule, new Vector3(i * 3.2f, -0.5f, 0.6f), new Vector3(0.45f, 1.6f, 0.45f), Quaternion.Euler(90f, 0f, 0f), dark);   // stores
            roar = gameObject.AddComponent<AudioSource>();
            roar.clip = RoarClip(); roar.loop = true; roar.spatialBlend = 1f;
            roar.rolloffMode = AudioRolloffMode.Linear; roar.minDistance = 40f; roar.maxDistance = 2600f; roar.dopplerLevel = 1.2f;
            roar.volume = 0.9f * GameSettings.Volume;
            roar.Play();
            gun = gameObject.AddComponent<AudioSource>();
            gun.clip = GunClip(); gun.loop = true; gun.spatialBlend = 0.85f;
            gun.rolloffMode = AudioRolloffMode.Linear; gun.minDistance = 60f; gun.maxDistance = 2500f;
            gun.volume = 1f * GameSettings.Volume;
        }

        void Part(PrimitiveType t, Vector3 at, Vector3 size, Quaternion rot, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(transform, false);
            g.transform.localPosition = at; g.transform.localRotation = rot; g.transform.localScale = size;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        void Update()
        {
            if (WorldFreeze.On) return;
            float dt = Time.deltaTime;
            s += speed * dt;
            Vector3 p = PathAt(s);
            transform.position = p;
            Vector3 v = p - lastPos;
            if (v.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(v, Vector3.up);
            lastPos = p;
            float toGo = RunIn - s;
            if (kind == Radio.Strike.Bombs) BombRun(toGo);
            else GunRun(toGo);
            if (toGo < -1700f) Destroy(gameObject);
        }

        // ------------------------------------------------------------ the bombs

        void BombRun(float toGo)
        {
            if (bombsLeft <= 0) return;
            // each bomb falls for T seconds and carries forward the jet's speed: let go when that lands it on its spot
            const float T = 5.4f;   // (from 150 m)
            // a stick of six walked along the line, 7 m apart, the middle of it on the mark
            float spot = -17.5f + (Bombs - bombsLeft) * 7f;
            if (toGo - spot > speed * T) return;
            Vector3 impact = target + along * spot;
            if (Physics.Raycast(impact + Vector3.up * 30f, Vector3.down, out var gh, 80f, ~0, QueryTriggerInteraction.Ignore)) impact = gh.point;
            Bomb.Release(transform.position - Vector3.up * 1f, impact, T);
            bombsLeft--;
        }

        // ------------------------------------------------------------ the gun

        void GunRun(float toGo)
        {
            const float open = 900f, close = 250f;
            bool firing = toGo < open && toGo > close;
            if (firing && !gun.isPlaying) gun.Play();
            if (!firing && gun.isPlaying) gun.Stop();
            if (!firing || Time.time < nextRound || Ballistics.Instance == null) return;
            nextRound = Time.time + 1f / 22f;
            fired++;
            // the burst walks from short of the mark, across it, to past it
            float k = Mathf.InverseLerp(open, close, toGo);
            Vector2 sc = Random.insideUnitCircle * 3f;
            Vector3 aim = target + along * Mathf.Lerp(-30f, 22f, k) + new Vector3(sc.x, 0f, sc.y);
            Vector3 muzzle = transform.position + transform.forward * 7f - transform.up * 0.6f;
            float dist = Vector3.Distance(muzzle, aim);
            const float v = 1000f;
            float t = dist / v * 1.06f;
            Vector3 dir = (aim + Vector3.up * (0.5f * 9.81f * t * t) - muzzle).normalized;
            Ballistics.FiringBody = null;
            Ballistics.Instance.Fire(muzzle, dir, v, 0.06f, 0.025f, HitKind.Bullet, (int)fired % 3 == 0);
            // every third an explosive round: a small burst where it lands
            if ((int)fired % 3 == 0 && Physics.Raycast(muzzle, dir, out var hit, dist + 40f, ~0, QueryTriggerInteraction.Ignore))
                StartCoroutine(Burst(hit.point + hit.normal * 0.1f, hit.distance / v));
        }

        System.Collections.IEnumerator Burst(Vector3 at, float after)
        {
            yield return new WaitForSeconds(after);
            var g = Grenade.Throw(at, Vector3.zero);
            g.fuse = 0.0001f;
            g.shrapnel = 18;
            g.team = Teams.Friendly;
        }

        // ------------------------------------------------------------ sounds

        /// <summary>A jet: a deep roar with a hiss on top.</summary>
        public static AudioClip RoarClipPublic() => RoarClip();
        static AudioClip RoarClip()
        {
            if (roarClip != null) return roarClip;
            int rate = 22050, n = rate * 2;
            var d = new float[n];
            var rnd = new System.Random(11);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = (float)rnd.NextDouble() * 2f - 1f;
                lp = Mathf.Lerp(lp, w, 0.04f);
                lp2 = Mathf.Lerp(lp2, w, 0.35f);
                float t = i / (float)rate;
                d[i] = lp * 2.2f + lp2 * 0.25f + Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.08f;
            }
            // (looped: blend the end into the start)
            for (int i = 0; i < 2000; i++) { float k = i / 2000f; d[n - 2000 + i] = Mathf.Lerp(d[n - 2000 + i], d[i], k); }
            roarClip = AudioClip.Create("JetRoar", n, 1, rate, false);
            roarClip.SetData(d, 0);
            return roarClip;
        }

        /// <summary>A rotary cannon: a rasping buzz of rounds too fast to tell apart.</summary>
        static AudioClip GunClip()
        {
            if (gunClip != null) return gunClip;
            int rate = 22050, n = rate / 2;
            var d = new float[n];
            var rnd = new System.Random(5);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float ph = (t * 65f) % 1f;
                float env = Mathf.Exp(-ph * 9f);
                d[i] = ((float)rnd.NextDouble() * 2f - 1f) * env * 0.9f + Mathf.Sin(2f * Mathf.PI * 65f * t) * 0.3f;
            }
            gunClip = AudioClip.Create("JetGun", n, 1, rate, false);
            gunClip.SetData(d, 0);
            return gunClip;
        }
    }

    /// <summary>A heavy bomb falling onto its spot, and what it does when it gets there.</summary>
    public class Bomb : MonoBehaviour
    {
        Vector3 from, v0, impact;
        float t, T;
        static readonly Vector3 G = new Vector3(0f, -9.81f, 0f);

        public static void Release(Vector3 from, Vector3 impact, float T)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(go.GetComponent<Collider>());
            go.name = "Bomb";
            go.transform.localScale = new Vector3(0.45f, 1.1f, 0.45f);
            go.GetComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(0.3f, 0.33f, 0.25f), 0.3f);
            var b = go.AddComponent<Bomb>();
            b.from = from; b.impact = impact; b.T = T;
            b.v0 = (impact - from - 0.5f * G * T * T) / T;
            go.transform.position = from;
        }

        void Update()
        {
            if (WorldFreeze.On) return;
            t += Time.deltaTime;
            Vector3 p = from + v0 * t + 0.5f * G * t * t;
            Vector3 vel = v0 + G * t;
            transform.position = p;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, -vel.normalized);
            if (t >= T) { Blast(impact, 1f); Destroy(gameObject); }
        }

        /// <summary>
        /// A big blast: a ring of charges round the middle so the overpressure and fragments reach out well past
        /// a grenade's (everyone within a few metres is thrown down; fragments fly a long way). <paramref name="size"/>
        /// 1 is an aircraft bomb, less an artillery shell.
        /// </summary>
        public static void Blast(Vector3 at, float size)
        {
            Vector3 c = at + Vector3.up * 0.3f;
            var g = Grenade.Throw(c, Vector3.zero);
            g.fuse = 0.0001f; g.shrapnel = Mathf.RoundToInt(240 * size); g.team = Teams.Friendly;
            int ring = size >= 0.9f ? 2 : 1;
            for (int i = 0; i < ring; i++)
            {
                float a = i / (float)ring * Mathf.PI * 2f + Random.value;
                Vector3 p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3.5f * size;
                var r = Grenade.Throw(p, Vector3.zero);
                r.fuse = 0.0001f + i * 0.02f; r.shrapnel = Mathf.RoundToInt(90 * size); r.team = Teams.Friendly;
            }
            Fx.Explosion(c + Vector3.up * 1.5f * size);
            Sfx.Play("explosion", c, 1f, 1f, 40f * size, 0.05f, 900f, 22000f);
            HumanBrain.BroadcastNoise(c, 400f, 1f);
            // knocked flat: anyone close enough who isn't behind something solid
            foreach (var h in HumanBody.All.ToArray())
            {
                if (h == null || h.Dead || h.Pelvis == null) continue;
                float d = Vector3.Distance(h.Center, c);
                float reach = h == PlayerAvatar.Body ? 3.5f : 9f;     // (you: only from close by)
                if (d < reach * size && EarlGunner.Clear(c + Vector3.up * 0.5f, h.Center, h)) h.ragdoll.Fall("blown off his feet");
            }
            if (SandboxGame.Instance != null && SandboxGame.Instance.cam != null)
                SandboxGame.Instance.Shake(Mathf.Clamp01((1.6f - Vector3.Distance(c, SandboxGame.Instance.cam.transform.position) / 60f) * Mathf.Lerp(0.6f, 1f, size)));
        }
    }

    /// <summary>
    /// An artillery barrage: after the guns have fired and the shells are on their way, about twenty seconds of
    /// shells coming down at random all over the area round the mark, each one with its scream on the way in.
    /// </summary>
    public class Barrage : MonoBehaviour
    {
        Vector3 target;
        float radius, wait, left, next;
        static AudioClip screamClip;

        public static void Fire(Vector3 at, float radius, float delay, float seconds)
        {
            var b = new GameObject("Barrage").AddComponent<Barrage>();
            b.target = at; b.radius = radius; b.wait = delay; b.left = seconds;
        }

        void Update()
        {
            if (WorldFreeze.On) return;
            float dt = Time.deltaTime;
            if (wait > 0f) { wait -= dt; return; }
            left -= dt;
            if (left <= 0f) { Destroy(gameObject, 3f); enabled = false; return; }
            if (Time.time < next) return;
            next = Time.time + Random.Range(0.45f, 1.3f);
            // somewhere in the area (shells scatter, more of them toward the middle)
            Vector2 r = Random.insideUnitCircle * radius;
            Vector3 p = target + new Vector3(r.x, 0f, r.y);
            if (Physics.Raycast(p + Vector3.up * 40f, Vector3.down, out var gh, 90f, ~0, QueryTriggerInteraction.Ignore)) p = gh.point;
            StartCoroutine(Incoming(p));
        }

        System.Collections.IEnumerator Incoming(Vector3 at)
        {
            // the scream of it coming in, then the hit
            var go = new GameObject("Shell");
            go.transform.position = at + Vector3.up * 25f;
            var a = go.AddComponent<AudioSource>();
            a.clip = Scream(); a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 15f; a.maxDistance = 600f;
            a.volume = 0.6f * GameSettings.Volume; a.pitch = Random.Range(0.9f, 1.1f);
            a.Play();
            Destroy(go, 2f);
            yield return new WaitForSeconds(0.9f);
            Bomb.Blast(at, 0.7f);
        }

        /// <summary>A falling whistle, high to low.</summary>
        static AudioClip Scream()
        {
            if (screamClip != null) return screamClip;
            int rate = 22050, n = (int)(rate * 0.9f);
            var d = new float[n];
            var rnd = new System.Random(9);
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                ph += 2f * Mathf.PI * Mathf.Lerp(1400f, 500f, t) / rate;
                float env = Mathf.SmoothStep(0f, 1f, t * 3f) * (1f - t * 0.3f);
                d[i] = (Mathf.Sin(ph) * 0.35f + ((float)rnd.NextDouble() * 2f - 1f) * 0.08f) * env;
            }
            screamClip = AudioClip.Create("ShellScream", n, 1, rate, false);
            screamClip.SetData(d, 0);
            return screamClip;
        }
    }
}

namespace Carl
{
    /// <summary>
    /// A single guided missile: it comes down steeply along the arrow, trailing smoke, and hits the middle of the
    /// mark - a much smaller blast than a bomb, for one target (a man, a dugout, a corner).
    /// </summary>
    public class Missile : MonoBehaviour
    {
        Vector3 from, to;
        float t, T, nextPuff;
        AudioSource roar;

        public static void Inbound(Vector3 at, Vector3 along, float delay)
        {
            var go = new GameObject("Missile");
            var m = go.AddComponent<Missile>();
            m.to = at;
            m.from = at - along * 500f + Vector3.up * 380f;
            m.T = delay;
            go.transform.position = m.from;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(go.transform, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.25f, 1f, 0.25f);
            body.GetComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(0.55f, 0.57f, 0.55f), 0.4f, 0.3f);
            var l = new GameObject("Motor").AddComponent<Light>();
            l.transform.SetParent(go.transform, false); l.transform.localPosition = Vector3.back * 1.2f;
            l.type = LightType.Point; l.range = 12f; l.intensity = 4f; l.color = new Color(1f, 0.7f, 0.35f);
            m.roar = go.AddComponent<AudioSource>();
            m.roar.clip = Jet.RoarClipPublic(); m.roar.loop = true; m.roar.spatialBlend = 1f; m.roar.pitch = 1.6f;
            m.roar.rolloffMode = AudioRolloffMode.Linear; m.roar.minDistance = 20f; m.roar.maxDistance = 1200f; m.roar.volume = 0.6f * GameSettings.Volume;
            m.roar.Play();
        }

        void Update()
        {
            if (WorldFreeze.On) return;
            t += Time.deltaTime;
            // (a little faster at the end, as it dives)
            float k = Mathf.Clamp01(t / T);
            Vector3 p = Vector3.Lerp(from, to, k * k * 0.35f + k * 0.65f);
            Vector3 d = p - transform.position;
            if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d);
            transform.position = p;
            if (Time.time > nextPuff)
            {
                nextPuff = Time.time + 0.03f;
                Haze.Puff(p - transform.forward * 1.2f, -transform.forward * 0.5f, Random.Range(0.3f, 0.5f), 0.4f, new Color(0.75f, 0.74f, 0.72f), true);
            }
            if (k >= 1f)
            {
                Bomb.Blast(to, 0.5f);
                Destroy(gameObject);
            }
        }
    }
}
