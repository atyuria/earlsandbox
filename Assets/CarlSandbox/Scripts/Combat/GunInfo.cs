using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// What each gun is, in one place: its model, the cartridge it shares with others (ammo table), how it sounds,
    /// the case it throws, its weight, fire modes, and how an Earl uses it (rate, kick, magazine). The newer
    /// Kalashnikovs and the machine guns lean on this; the older guns keep their own tables and are listed here too
    /// so the Earls and the menus can treat every gun the same way.
    /// </summary>
    public static class GunInfo
    {
        public class Info
        {
            public string name, model, ammo, sound;
            public EarlWeapon earl;
            public CaseKind casing;
            public float mass;
            public string[] modes;
            public float earlRpm, earlKick;
            public int earlMag;
            public bool lmg;
            public float heat;              // barrel heat per round (BarrelHeat)
        }

        static readonly Dictionary<string, Info> byName = new Dictionary<string, Info>();
        static readonly Dictionary<EarlWeapon, Info> byEarl = new Dictionary<EarlWeapon, Info>();
        static readonly Dictionary<string, Info> byModel = new Dictionary<string, Info>();

        static void Add(Info i)
        {
            byName[i.name] = i; byModel[i.model] = i;
            if (i.earl != EarlWeapon.None) byEarl[i.earl] = i;
        }

        static GunInfo()
        {
            var auto = new[] { "AUTO", "SEMI" };
            Add(new Info { name = "AK-47", model = "Gun_AK47", ammo = "AK-47", sound = "gun_ak", earl = EarlWeapon.AK47, casing = CaseKind.SevenSixTwo, mass = 3.6f, modes = auto, earlRpm = 600, earlKick = 3.5f, earlMag = 30, heat = 0.0085f });
            Add(new Info { name = "AR-15", model = "Gun_AR15", ammo = "AR-15", sound = "gun_556", earl = EarlWeapon.AR15, casing = CaseKind.FiveFiveSix, mass = 3.1f, modes = new[] { "AUTO", "BURST", "SEMI" }, earlRpm = 750, earlKick = 2.5f, earlMag = 30, heat = 0.0075f });
            // 5.45x39: the AK-74 and everything since
            Add(new Info { name = "AK-74", model = "Gun_AK74", ammo = "AK-74", sound = "gun_545", earl = EarlWeapon.AK74, casing = CaseKind.FiveFiveSix, mass = 3.3f, modes = auto, earlRpm = 650, earlKick = 2.6f, earlMag = 30, heat = 0.0075f });
            Add(new Info { name = "AK-74M", model = "Gun_AK74M", ammo = "AK-74", sound = "gun_545", earl = EarlWeapon.AK74M, casing = CaseKind.FiveFiveSix, mass = 3.4f, modes = auto, earlRpm = 650, earlKick = 2.5f, earlMag = 30, heat = 0.0075f });
            Add(new Info { name = "AK-105", model = "Gun_AK105", ammo = "AK-74", sound = "gun_545", earl = EarlWeapon.AK105, casing = CaseKind.FiveFiveSix, mass = 3.2f, modes = auto, earlRpm = 650, earlKick = 2.7f, earlMag = 30, heat = 0.0085f });
            Add(new Info { name = "AKS-74U", model = "Gun_AKS74U", ammo = "AK-74", sound = "gun_545", earl = EarlWeapon.AKS74U, casing = CaseKind.FiveFiveSix, mass = 2.7f, modes = auto, earlRpm = 700, earlKick = 2.9f, earlMag = 30, heat = 0.0095f });
            Add(new Info { name = "AK-12", model = "Gun_AK12", ammo = "AK-74", sound = "gun_545", earl = EarlWeapon.AK12, casing = CaseKind.FiveFiveSix, mass = 3.5f, modes = auto, earlRpm = 700, earlKick = 2.4f, earlMag = 30, heat = 0.0072f });
            // the squad's machine guns: heavy barrels made for long bursts
            Add(new Info { name = "RPK-74", model = "Gun_RPK74", ammo = "AK-74", sound = "gun_545", earl = EarlWeapon.RPK74, casing = CaseKind.FiveFiveSix, mass = 5f, modes = auto, earlRpm = 600, earlKick = 2.3f, earlMag = 45, lmg = true, heat = 0.0058f });
            Add(new Info { name = "PKM", model = "Gun_PKM", ammo = "PKM", sound = "gun_pkm", earl = EarlWeapon.PKM, casing = CaseKind.SevenSixTwo, mass = 8.4f, modes = new[] { "AUTO" }, earlRpm = 650, earlKick = 3.6f, earlMag = 100, lmg = true, heat = 0.0042f });
            Add(new Info { name = "M249", model = "Gun_M249", ammo = "AR-15", sound = "gun_556", earl = EarlWeapon.M249, casing = CaseKind.FiveFiveSix, mass = 8.2f, modes = new[] { "AUTO" }, earlRpm = 800, earlKick = 2.6f, earlMag = 200, lmg = true, heat = 0.0045f });
        }

        public static Info Of(string name) => name != null && byName.TryGetValue(name, out var i) ? i : null;
        public static Info Of(EarlWeapon w) => byEarl.TryGetValue(w, out var i) ? i : null;
        public static Info OfModel(string model) => model != null && byModel.TryGetValue(model, out var i) ? i : null;
        /// <summary>Is it a Kalashnikov or a machine gun built on the AK model's layout (hands, sights, light)?</summary>
        public static bool AkPattern(string name) => name != null && (name.StartsWith("AK") || name == "RPK-74");
        public static bool Lmg(string name) => Of(name) is Info i && i.lmg;
    }
}
