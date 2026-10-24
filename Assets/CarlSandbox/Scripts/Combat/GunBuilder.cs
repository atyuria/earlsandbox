using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Carl
{
    /// <summary>The parts of a gun that come off and go on (the gunsmith, as in Tarkov).</summary>
    public enum PartSlot { Barrel, Cover, Handguard, Muzzle, Grip, Stock, Mag }

    /// <summary>One real part: what it is, what it fits and what it does to the gun.</summary>
    public class GunPart
    {
        public string platform, id, name, desc;
        public PartSlot slot;
        /// <summary>Its model is the platform's prefix + the slot's + this (e.g. "AK_Stock_fold"); null: nothing there.</summary>
        public string model;
        /// <summary>Calibre it fits: 0 any, 545 or 762.</summary>
        public int cal;
        public float recoil = 1f, spread = 1f, velocity = 1f, loud = 1f, reload = 1f, weight;
        public int rounds;
        public bool suppressor, railed, needsShort;
        /// <summary>Barrels: their length (mm). Handguards: the shortest barrel they go over.</summary>
        public int length, minBarrel;
    }

    /// <summary>A gun as built: its platform and the part in each slot.</summary>
    public class GunBuild
    {
        public string platform = "AK";
        public string[] parts = new string[7];
        public string this[PartSlot s] { get => parts[(int)s]; set => parts[(int)s] = value; }
        public GunBuild Clone() => new GunBuild { platform = platform, parts = (string[])parts.Clone() };
        public string Key => platform + "_" + string.Join(".", parts);
    }

    /// <summary>
    /// The gunsmith: the Kalashnikovs are one platform and the AR-15 another, and any part of a platform goes on
    /// any of its guns (barrels of four lengths, dust covers or charging handles, handguards, muzzle devices and
    /// suppressors, grips, stocks and magazines - each a real part, modelled on it). A build is put together into
    /// one model, and the gun's numbers follow its parts: a short barrel is handier and louder with less
    /// velocity, a brake or a good stock tames the recoil, a suppressor quietens it, a bigger magazine is slower
    /// to change.
    /// </summary>
    public static class GunBuilder
    {
        static readonly List<GunPart> parts = new List<GunPart>();
        static readonly Dictionary<string, (string platform, int cal, GunBuild stock, string model)> guns = new Dictionary<string, (string, int, GunBuild, string)>();
        static readonly string[] slotNamesAK = { "BARREL", "DUST COVER", "HANDGUARD", "MUZZLE", "PISTOL GRIP", "STOCK", "MAGAZINE" };
        static readonly string[] slotNamesAR = { "BARREL", "CHARGING HANDLE", "HANDGUARD", "MUZZLE", "PISTOL GRIP", "STOCK", "MAGAZINE" };
        public static string SlotName(GunBuild b, int slot) => (b.platform == "AR" ? slotNamesAR : slotNamesAK)[slot];
        static readonly string[] modelSlot = { "Core", "Cover", "Hg", "Muzzle", "Grip", "Stock", "Mag" };

        static string plat = "AK";
        static GunPart P(PartSlot s, string id, string name, string desc, int cal = 0) { var p = new GunPart { platform = plat, slot = s, id = id, name = name, desc = desc, cal = cal, model = id }; parts.Add(p); return p; }

        static GunBuilder()
        {
            // ================================================================ Kalashnikov
            plat = "AK";
            // ---- barrels: velocity, accuracy, length (and the noise and kick of a short one)
            var b = P(PartSlot.Barrel, "206", "206 mm AKS-74U barrel", "The Krinkov's stub: handy indoors, loud, a big fireball, much less velocity.");
            b.length = 206; b.velocity = 0.82f; b.spread = 1.35f; b.recoil = 1.1f; b.loud = 1.2f; b.weight = 0.55f;
            b = P(PartSlot.Barrel, "314", "314 mm AK-105 barrel", "The short carbine barrel: a little slower, a little louder.");
            b.length = 314; b.velocity = 0.93f; b.spread = 1.12f; b.recoil = 1.04f; b.loud = 1.08f; b.weight = 0.7f;
            b = P(PartSlot.Barrel, "415", "415 mm standard barrel", "The issue rifle barrel.");
            b.length = 415; b.weight = 0.85f;
            b = P(PartSlot.Barrel, "590", "590 mm RPK heavy barrel + bipod", "Long, heavy and stiff: the most velocity and the tightest groups, at a price in weight.");
            b.length = 590; b.velocity = 1.06f; b.spread = 0.75f; b.recoil = 0.92f; b.weight = 1.6f;
            // ---- dust covers
            P(PartSlot.Cover, "ribbed", "AKM stamped dust cover", "The standard ribbed top cover.").weight = 0.2f;
            var c = P(PartSlot.Cover, "hinged", "AKS-74U hinged cover", "Hinged at the front, carries the rear sight: for the short barrel.");
            c.needsShort = true; c.weight = 0.25f;
            c = P(PartSlot.Cover, "railed", "AK-12 railed dust cover", "A rigid cover with a full-length Picatinny rail for optics.");
            c.railed = true; c.weight = 0.3f;
            // ---- handguards (only the railed ones take a foregrip)
            P(PartSlot.Handguard, "wood", "AKM wooden handguard", "Laminated birch, finger grooves.").weight = 0.3f;
            P(PartSlot.Handguard, "plum", "AK-74 bakelite handguard", "The plum-coloured Soviet furniture.").weight = 0.3f;
            P(PartSlot.Handguard, "poly", "AK-74M polymer handguard", "Black glass-filled polymer, ribbed.").weight = 0.25f;
            var h = P(PartSlot.Handguard, "ak12", "AK-12 railed handguard", "Picatinny on top, M-LOK slots: takes a foregrip.");
            h.railed = true; h.weight = 0.35f;
            h = P(PartSlot.Handguard, "zhukov", "Magpul Zhukov-U", "Polymer, M-LOK all round, covers the gas tube: takes a foregrip.");
            h.railed = true; h.weight = 0.3f;
            h = P(PartSlot.Handguard, "b10", "Zenit B-10 + B-13 rail", "Machined aluminium, rails at 3, 6, 9 and 12: takes a foregrip.");
            h.railed = true; h.weight = 0.45f; h.recoil = 0.98f;
            // ---- muzzle devices (the threads differ: 14x1 left-hand on 7.62, 24x1.5 on 5.45)
            var m = P(PartSlot.Muzzle, "slant", "AKM slant brake", "The cut compensator: pushes the muzzle down and right."); m.recoil = 0.9f; m.weight = 0.03f;
            m = P(PartSlot.Muzzle, "ak74", "AK-74 muzzle brake", "The big two-chamber brake: a lot less recoil, a lot more blast.", 545); m.recoil = 0.78f; m.loud = 1.15f; m.weight = 0.1f;
            m = P(PartSlot.Muzzle, "short", "AK-105 muzzle brake", "Short two-port brake for the carbines.", 545); m.recoil = 0.86f; m.loud = 1.1f; m.weight = 0.07f;
            m = P(PartSlot.Muzzle, "booster", "AKS-74U muzzle booster", "Flash hider and gas booster for the short barrel.", 545); m.recoil = 0.96f; m.weight = 0.09f;
            m = P(PartSlot.Muzzle, "ak12", "AK-12 muzzle brake", "Three ports each side.", 545); m.recoil = 0.8f; m.loud = 1.12f; m.weight = 0.1f;
            m = P(PartSlot.Muzzle, "dtk1", "Zenit DTK-1", "Three-chamber brake and flash hider: the best recoil reduction there is."); m.recoil = 0.74f; m.loud = 1.18f; m.weight = 0.15f;
            m = P(PartSlot.Muzzle, "pbs1", "PBS-1 suppressor", "The Soviet 7.62 can: quiet, little flash, long.", 762); m.suppressor = true; m.recoil = 0.86f; m.loud = 0.35f; m.weight = 0.55f;
            m = P(PartSlot.Muzzle, "pbs4", "PBS-4 suppressor", "The 5.45 can: quiet, little flash.", 545); m.suppressor = true; m.recoil = 0.86f; m.loud = 0.35f; m.weight = 0.45f;
            m = P(PartSlot.Muzzle, "thread", "Thread protector (bare)", "Nothing on the muzzle: all the kick and all the flash."); m.recoil = 1.12f; m.weight = 0.01f;
            // ---- pistol grips
            P(PartSlot.Grip, "wood", "AKM wooden grip", "Slab-sided birch.").weight = 0.06f;
            P(PartSlot.Grip, "plum", "AK-74 bakelite grip", "Plum Soviet bakelite.").weight = 0.06f;
            P(PartSlot.Grip, "poly", "AK-74M polymer grip", "Ribbed black polymer.").weight = 0.06f;
            var g = P(PartSlot.Grip, "ak12", "AK-12 grip", "Raked, a palm swell and a storage core."); g.recoil = 0.98f; g.weight = 0.08f;
            g = P(PartSlot.Grip, "moe", "Magpul MOE AK grip", "Chunky, textured, a better angle."); g.recoil = 0.97f; g.weight = 0.08f;
            g = P(PartSlot.Grip, "rk3", "Zenit RK-3", "Upright, finger grooves, a firm hold."); g.recoil = 0.96f; g.weight = 0.09f;
            // ---- stocks
            var s = P(PartSlot.Stock, "akm", "AKM wooden stock", "Fixed laminated stock."); s.weight = 0.6f;
            s = P(PartSlot.Stock, "ak74", "AK-74 laminated stock", "Fixed, with the lightening cuts."); s.weight = 0.55f;
            s = P(PartSlot.Stock, "fold", "AK-74M side-folding stock", "Solid polymer, folds to the left."); s.weight = 0.5f;
            s = P(PartSlot.Stock, "skel", "AKS-74U skeleton stock", "Triangle of steel strap: light, not much to hold."); s.recoil = 1.18f; s.weight = 0.3f;
            s = P(PartSlot.Stock, "ak12", "AK-12 telescoping stock", "Adjustable length, a raised cheek rest."); s.recoil = 0.92f; s.weight = 0.55f;
            s = P(PartSlot.Stock, "rpk", "RPK club stock", "The machine gunner's deep wooden stock."); s.recoil = 0.94f; s.weight = 0.75f;
            s = P(PartSlot.Stock, "zhukov", "Magpul Zhukov-S", "Folding skeleton stock, cheek riser, rubber pad."); s.recoil = 0.9f; s.weight = 0.4f;
            s = P(PartSlot.Stock, "none", "No stock", "Nothing at the shoulder: light, and very hard to hold on target."); s.model = null; s.recoil = 1.7f; s.spread = 1.6f;
            // ---- magazines
            var q = P(PartSlot.Mag, "762", "AKM 30-round steel", "The waffle magazine.", 762); q.rounds = 30; q.weight = 0.33f;
            q = P(PartSlot.Mag, "762x40", "RPK 40-round steel", "Longer and more curved.", 762); q.rounds = 40; q.reload = 1.1f; q.weight = 0.45f;
            q = P(PartSlot.Mag, "762x75", "RPK 75-round drum", "Seventy-five rounds, slow and heavy to change.", 762); q.rounds = 75; q.reload = 1.45f; q.weight = 0.9f;
            q = P(PartSlot.Mag, "545p", "AK-74 30-round bakelite", "The plum magazine.", 545); q.rounds = 30; q.weight = 0.23f;
            q = P(PartSlot.Mag, "545b", "AK-74M 30-round polymer", "Black polymer.", 545); q.rounds = 30; q.weight = 0.23f;
            q = P(PartSlot.Mag, "545x45", "RPK-74 45-round", "The light machine gun's magazine.", 545); q.rounds = 45; q.reload = 1.12f; q.weight = 0.32f;
            q = P(PartSlot.Mag, "545x60", "6L31 60-round quad-stack", "Four rows: sixty rounds in a fat magazine.", 545); q.rounds = 60; q.reload = 1.25f; q.weight = 0.5f;

            // ================================================================ AR-15
            plat = "AR";
            b = P(PartSlot.Barrel, "267", "10.5\" barrel", "SBR length: very handy, loud, a lot less velocity.");
            b.length = 267; b.velocity = 0.86f; b.spread = 1.25f; b.recoil = 1.06f; b.loud = 1.2f; b.weight = 0.6f;
            b = P(PartSlot.Barrel, "368", "14.5\" M4 barrel", "The M4 carbine's barrel.");
            b.length = 368; b.velocity = 0.97f; b.spread = 1.06f; b.loud = 1.05f; b.weight = 0.72f;
            b = P(PartSlot.Barrel, "406", "16\" barrel", "The civilian carbine length.");
            b.length = 406; b.weight = 0.78f;
            b = P(PartSlot.Barrel, "508", "20\" rifle barrel", "The full-length A2 barrel: most velocity, tightest groups, long.");
            b.length = 508; b.velocity = 1.06f; b.spread = 0.85f; b.recoil = 0.95f; b.weight = 0.95f;
            P(PartSlot.Cover, "milspec", "Mil-spec charging handle", "The standard T-handle.").weight = 0.03f;
            P(PartSlot.Cover, "raptor", "Radian Raptor ambi charging handle", "Ambidextrous latches either side.").weight = 0.04f;
            h = P(PartSlot.Handguard, "mlok13", "13\" free-float M-LOK handguard", "Long octagonal free-float, full top rail: takes a foregrip.");
            h.railed = true; h.minBarrel = 406; h.spread = 0.95f; h.weight = 0.3f;
            h = P(PartSlot.Handguard, "mlok9", "9.5\" free-float M-LOK handguard", "Short free-float: takes a foregrip.");
            h.railed = true; h.minBarrel = 368; h.spread = 0.96f; h.weight = 0.24f;
            h = P(PartSlot.Handguard, "m4", "M4 carbine handguard", "The ribbed plastic halves and the A-frame front sight.");
            h.weight = 0.22f;
            h = P(PartSlot.Handguard, "ras", "KAC RAS quad rail", "Rails all round and the A-frame front sight: takes a foregrip.");
            h.railed = true; h.weight = 0.35f;
            m = P(PartSlot.Muzzle, "a2", "A2 birdcage flash hider", "The standard flash hider."); m.weight = 0.05f;
            m = P(PartSlot.Muzzle, "sf3p", "SureFire SF3P", "Three-prong flash hider: almost no flash."); m.recoil = 0.98f; m.weight = 0.06f;
            m = P(PartSlot.Muzzle, "pa", "Precision Armament M4-72", "A severe-duty brake: much less recoil, a big blast."); m.recoil = 0.76f; m.loud = 1.2f; m.weight = 0.08f;
            m = P(PartSlot.Muzzle, "socom", "SureFire SOCOM556-RC2", "A quick-detach can: quiet, long."); m.suppressor = true; m.recoil = 0.85f; m.loud = 0.33f; m.weight = 0.5f;
            P(PartSlot.Grip, "a2", "A2 pistol grip", "The issue grip with the finger swell.").weight = 0.07f;
            g = P(PartSlot.Grip, "moe", "Magpul MOE+", "Fuller, rubber over-moulded."); g.recoil = 0.98f; g.weight = 0.08f;
            g = P(PartSlot.Grip, "hogue", "Hogue OverMolded", "Rubber all over, finger grooves."); g.recoil = 0.97f; g.weight = 0.09f;
            g = P(PartSlot.Grip, "k2", "Magpul K2", "Upright, for a squared-up stance."); g.recoil = 0.98f; g.weight = 0.08f;
            s = P(PartSlot.Stock, "ctr", "Magpul CTR", "Collapsible, friction-locked."); s.weight = 0.3f;
            s = P(PartSlot.Stock, "moe", "Magpul MOE carbine stock", "Slim collapsible stock."); s.recoil = 1.03f; s.weight = 0.25f;
            s = P(PartSlot.Stock, "m4", "Mil-spec M4 stock", "The issue sliding stock."); s.recoil = 1.04f; s.weight = 0.22f;
            s = P(PartSlot.Stock, "prs", "Magpul PRS Gen3", "Fixed precision stock with cheek and length adjustment."); s.recoil = 0.88f; s.spread = 0.9f; s.weight = 0.6f;
            s = P(PartSlot.Stock, "none", "Bare buffer tube", "No stock: light, and very hard to hold on target."); s.model = null; s.recoil = 1.6f; s.spread = 1.5f;
            q = P(PartSlot.Mag, "stanag20", "20-round STANAG", "The short aluminium magazine."); q.rounds = 20; q.reload = 0.95f; q.weight = 0.25f;
            q = P(PartSlot.Mag, "pmag30", "Magpul PMAG 30", "The polymer 30-rounder."); q.rounds = 30; q.weight = 0.33f;
            q = P(PartSlot.Mag, "pmag40", "Magpul PMAG 40", "Ten more rounds, a longer magazine."); q.rounds = 40; q.reload = 1.08f; q.weight = 0.43f;
            q = P(PartSlot.Mag, "sf60", "SureFire MAG5-60", "Sixty rounds in a quad-stack body."); q.rounds = 60; q.reload = 1.22f; q.weight = 0.6f;
            q = P(PartSlot.Mag, "d60", "Magpul D-60 drum", "Sixty rounds in a twin drum: slow and heavy."); q.rounds = 60; q.reload = 1.4f; q.weight = 0.9f;

            // ---- each gun as it comes from the factory: barrel, cover, handguard, muzzle, grip, stock, magazine
            void Gun(string name, string model, string platform, int cal, params string[] p) => guns[name] = (platform, cal, new GunBuild { platform = platform, parts = p }, model);
            Gun("AK-47", "Gun_AK47", "AK", 762, "415", "ribbed", "wood", "slant", "wood", "akm", "762");
            Gun("AK-74", "Gun_AK74", "AK", 545, "415", "ribbed", "plum", "ak74", "plum", "ak74", "545p");
            Gun("AK-74M", "Gun_AK74M", "AK", 545, "415", "ribbed", "poly", "ak74", "poly", "fold", "545b");
            Gun("AK-105", "Gun_AK105", "AK", 545, "314", "ribbed", "poly", "short", "poly", "fold", "545b");
            Gun("AKS-74U", "Gun_AKS74U", "AK", 545, "206", "hinged", "plum", "booster", "plum", "skel", "545p");
            Gun("AK-12", "Gun_AK12", "AK", 545, "415", "railed", "ak12", "ak12", "ak12", "ak12", "545b");
            Gun("RPK-74", "Gun_RPK74", "AK", 545, "590", "ribbed", "plum", "slant", "plum", "rpk", "545x45");
            Gun("AR-15", "Gun_AR15", "AR", 556, "406", "milspec", "mlok13", "a2", "a2", "ctr", "pmag30");
        }

        public static bool Supports(string gun) => gun != null && guns.ContainsKey(gun);
        public static GunBuild Stock(string gun) => guns[gun].stock.Clone();
        public static GunPart Part(string platform, PartSlot s, string id) { foreach (var p in parts) if (p.platform == platform && p.slot == s && p.id == id) return p; return null; }
        public static GunPart Part(GunBuild b, PartSlot s) => b == null ? null : Part(b.platform, s, b[s]);

        static int BarrelLength(GunBuild b) => Part(b, PartSlot.Barrel)?.length ?? 0;
        static bool Fits(GunPart p, int cal, GunBuild b)
        {
            if (p.cal != 0 && p.cal != cal) return false;
            if (p.needsShort && BarrelLength(b) > 250) return false;
            if (p.minBarrel > 0 && BarrelLength(b) < p.minBarrel) return false;
            return true;
        }

        /// <summary>The parts that fit this slot on this gun as it is built (calibre, barrel length).</summary>
        public static List<GunPart> Options(string gun, PartSlot slot, GunBuild build)
        {
            var l = new List<GunPart>();
            var gi = guns[gun];
            foreach (var p in parts) if (p.platform == gi.platform && p.slot == slot && Fits(p, gi.cal, build)) l.Add(p);
            return l;
        }

        /// <summary>Put a part on, and change whatever no longer fits with it for something that does.</summary>
        public static void Fit(string gun, GunBuild b, PartSlot slot, string id)
        {
            b[slot] = id;
            var gi = guns[gun];
            for (int i = 0; i < 7; i++)
            {
                var p = Part(b, (PartSlot)i);
                if (p != null && Fits(p, gi.cal, b)) continue;
                // the factory part if it fits, else the first that does
                var f = Part(gi.platform, (PartSlot)i, gi.stock.parts[i]);
                if (f != null && Fits(f, gi.cal, b)) { b.parts[i] = f.id; continue; }
                foreach (var o in parts) if (o.platform == gi.platform && o.slot == (PartSlot)i && Fits(o, gi.cal, b)) { b.parts[i] = o.id; break; }
            }
        }

        /// <summary>The model of a part as it would go on this build (for the gunsmith's pictures).</summary>
        public static string PartModel(GunBuild b, GunPart p)
        {
            if (p == null || p.model == null) return null;
            string pre = p.platform + "_";
            switch (p.slot)
            {
                case PartSlot.Barrel: return pre + "Core_" + p.id;
                case PartSlot.Cover: return pre + (p.platform == "AR" ? "Ch_" : "Cover_") + p.id;
                case PartSlot.Handguard: return pre + "Hg_" + p.id + (p.platform == "AK" ? "_" + b[PartSlot.Barrel] : "");
                default: return pre + modelSlot[(int)p.slot] + "_" + p.id;
            }
        }

        public static bool IsStock(string gun, GunBuild b) => b == null || b.Key == guns[gun].stock.Key;
        public static bool Railed(GunBuild b) => Part(b, PartSlot.Handguard)?.railed == true;
        public static bool Suppressed(GunBuild b) => Part(b, PartSlot.Muzzle)?.suppressor == true;
        public static int Rounds(GunBuild b) => Part(b, PartSlot.Mag)?.rounds ?? 30;

        /// <summary>
        /// The model for this gun as built: the factory model if nothing is changed, otherwise its parts put
        /// together (and kept: the same build is the same model).
        /// </summary>
        public static string Model(string gun, GunBuild b)
        {
            if (IsStock(gun, b)) return guns[gun].model;
            string key = "Build_" + b.Key;
            if (ModelLibrary.Has(key)) return key;
            string pre = b.platform + "_";
            var core = ModelLibrary.Get(pre + "Core_" + b[PartSlot.Barrel]);
            if (core == null) return guns[gun].model;
            Vector3 mb = core.points.TryGetValue("MuzzleBase", out var x) ? x : new Vector3(0f, 0f, 0.47f);
            var list = new List<(string, Vector3)>();
            for (int i = 0; i < 7; i++)
            {
                var slot = (PartSlot)i;
                if (slot == PartSlot.Mag) continue;
                var p = Part(b, slot);
                if (p == null || (p.model == null && slot != PartSlot.Barrel)) continue;
                string name = pre + modelSlot[i] + "_" + (slot == PartSlot.Cover && b.platform == "AR" ? "" : "") + b[slot];
                if (slot == PartSlot.Cover && b.platform == "AR") name = pre + "Ch_" + b[slot];
                if (slot == PartSlot.Handguard && b.platform == "AK") name += "_" + b[PartSlot.Barrel];
                list.Add((name, slot == PartSlot.Muzzle ? mb : Vector3.zero));
            }
            var m = ModelLibrary.Combine(key, list);
            var muz = ModelLibrary.Get(pre + "Muzzle_" + b[PartSlot.Muzzle]);
            m.points["Muzzle"] = mb + (muz != null && muz.points.TryGetValue("Front", out var f) ? f : Vector3.zero);
            var mag = ModelLibrary.Combine(key + "_Mag", new List<(string, Vector3)> { (pre + "Mag_" + b[PartSlot.Mag], Vector3.zero) });
            if (mag.points.TryGetValue("MagBottom", out var bot)) m.points["MagBottom"] = bot;
            return key;
        }

        static float Prod(GunBuild x, System.Func<GunPart, float> f) { float k = 1f; for (int i = 0; i < 7; i++) { var p = Part(x, (PartSlot)i); if (p != null) k *= f(p); } return k; }

        /// <summary>The gun's numbers as built: the factory gun's, scaled by what changed.</summary>
        public static void Stats(string gun, GunBuild b, WeaponDef factory, WeaponDef into)
        {
            var s = guns[gun].stock;
            into.recoil = factory.recoil * Prod(b, p => p.recoil) / Prod(s, p => p.recoil);
            into.spread = factory.spread * Prod(b, p => p.spread) / Prod(s, p => p.spread);
            into.muzzleVelocity = factory.muzzleVelocity * Prod(b, p => p.velocity) / Prod(s, p => p.velocity);
            into.loudness = factory.loudness * Prod(b, p => p.loud) / Prod(s, p => p.loud);
            into.reloadTime = factory.reloadTime * Prod(b, p => p.reload) / Prod(s, p => p.reload);
            into.magazine = Rounds(b);
        }

        public static float Weight(GunBuild b)
        {
            float w = b.platform == "AR" ? 1.9f : 2.1f;
            for (int i = 0; i < 7; i++) { var p = Part(b, (PartSlot)i); if (p != null) w += p.weight; }
            return w;
        }

        /// <summary>How a part changes the gun, against the factory part, in a few words (for the menu).</summary>
        public static string Effect(string gun, GunPart p)
        {
            var gi = guns[gun];
            var f = Part(gi.platform, p.slot, gi.stock[p.slot]);
            var sb = new StringBuilder();
            void Pct(string what, float a, float b0)
            {
                if (b0 <= 0f) return;
                float d = (a / b0 - 1f) * 100f;
                if (Mathf.Abs(d) < 1.5f) return;
                sb.Append(sb.Length > 0 ? "  " : "").Append(what).Append(d > 0 ? " +" : " ").Append(d.ToString("0")).Append('%');
            }
            if (f != null)
            {
                Pct("RECOIL", p.recoil, f.recoil);
                Pct("SPREAD", p.spread, f.spread);
                Pct("VELOCITY", p.velocity, f.velocity);
                Pct("NOISE", p.loud, f.loud);
                Pct("RELOAD", p.reload, f.reload);
            }
            if (p.rounds > 0) sb.Append(sb.Length > 0 ? "  " : "").Append(p.rounds).Append(" RDS");
            if (p.weight > 0f) sb.Append(sb.Length > 0 ? "  " : "").Append(p.weight.ToString("0.00")).Append(" KG");
            return sb.ToString();
        }

        /// <summary>
        /// A build an Earl has put together: mostly the factory parts, some of each swapped for what's about -
        /// Ukrainians more often Western furniture (Magpul), Russians Zenit; a can now and then, the odd drum.
        /// </summary>
        public static GunBuild Random(string gun, int nation = -1)
        {
            var b = Stock(gun);
            if (UnityEngine.Random.value < 0.25f) return b;
            for (int i = 0; i < 7; i++)
            {
                var slot = (PartSlot)i;
                if (UnityEngine.Random.value > (slot == PartSlot.Barrel ? 0.15f : slot == PartSlot.Cover ? 0.15f : 0.4f)) continue;
                var opts = Options(gun, slot, b);
                if (opts.Count == 0) continue;
                GunPart pick = opts[UnityEngine.Random.Range(0, opts.Count)];
                // their own side's kit, more often than not
                if (nation >= 0 && UnityEngine.Random.value < 0.5f)
                    foreach (var o in opts)
                        if ((nation == Nations.Ukraine && (o.id == "zhukov" || o.id == "moe")) || (nation == Nations.Russia && (o.id == "b10" || o.id == "rk3" || o.id == "dtk1"))) { pick = o; break; }
                if (slot == PartSlot.Stock && pick.id == "none") continue;
                if (slot == PartSlot.Mag && pick.rounds > 45 && UnityEngine.Random.value < 0.7f) continue;
                Fit(gun, b, slot, pick.id);
            }
            return b;
        }
    }
}
