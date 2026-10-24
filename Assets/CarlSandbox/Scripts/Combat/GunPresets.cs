using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>A saved build of one gun: its parts, attachments and ammunition, under a name.</summary>
    [System.Serializable]
    public class GunPreset
    {
        public string gun, name;
        public string platform;
        public string[] parts;
        public int sight, grip, ammo;
        public string optic, foregrip, lightId, laserId;
        public bool light, laser, suppressor, magnifier, extMag, autoSwitch;
        public int lightSide = -1, laserSide = -1;

        public static GunPreset From(string gun, string name, WeaponLoadout L) => new GunPreset
        {
            gun = gun, name = name, platform = L.build?.platform, parts = L.build != null ? (string[])L.build.parts.Clone() : null,
            sight = (int)L.sight, grip = (int)L.grip, ammo = L.ammo, optic = L.optic, foregrip = L.foregrip, lightId = L.lightId, laserId = L.laserId,
            light = L.light, laser = L.laser, suppressor = L.suppressor, magnifier = L.magnifier, extMag = L.extMag, autoSwitch = L.autoSwitch,
            lightSide = L.lightSide, laserSide = L.laserSide,
        };

        public void Apply(WeaponLoadout L)
        {
            L.build = parts != null && parts.Length == 7 ? new GunBuild { platform = platform, parts = (string[])parts.Clone() } : null;
            L.sight = (SightKind)sight; L.grip = (GripKind)grip; L.ammo = ammo; L.optic = optic; L.foregrip = foregrip; L.lightId = lightId; L.laserId = laserId;
            L.light = light; L.laser = laser; L.suppressor = suppressor; L.magnifier = magnifier; L.extMag = extMag; L.autoSwitch = autoSwitch;
            L.lightSide = lightSide; L.laserSide = laserSide;
        }
    }

    /// <summary>Your saved gun builds (the gunsmith's SAVE / LOAD), kept between games.</summary>
    public static class GunPresets
    {
        [System.Serializable] class Store { public List<GunPreset> list = new List<GunPreset>(); }
        const string Key = "earl.gunPresets";
        static Store store;

        static Store S
        {
            get
            {
                if (store != null) return store;
                try { store = JsonUtility.FromJson<Store>(PlayerPrefs.GetString(Key, "")); } catch { store = null; }
                return store ??= new Store();
            }
        }

        static void Write() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(S)); PlayerPrefs.Save(); }

        public static List<GunPreset> For(string gun) { var l = new List<GunPreset>(); foreach (var p in S.list) if (p.gun == gun) l.Add(p); return l; }

        /// <summary>Save (or overwrite one of the same name).</summary>
        public static void Save(GunPreset p)
        {
            S.list.RemoveAll(x => x.gun == p.gun && x.name == p.name);
            S.list.Add(p);
            Write();
        }

        public static void Delete(GunPreset p) { S.list.Remove(p); Write(); }
    }
}
