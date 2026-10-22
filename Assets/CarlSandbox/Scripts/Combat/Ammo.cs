using System.Collections.Generic;
using UnityEngine;

namespace Carl
{
    /// <summary>
    /// A cartridge. pen is the armour class it defeats (plates of a lower class are punched through, equal is a
    /// coin toss that gets worse as the plate wears, higher stops it). wall scales how much material it can
    /// shoot through; expand scales the wound it makes in flesh.
    /// </summary>
    public class AmmoType
    {
        public string name, desc;
        public float velocity, mass, diameter;
        public float pen = 3f, wall = 1f, expand = 1f;
        public int pellets = 1;
        public float spread;      // extra pellet spread (deg), shotguns
        public bool tracer;
        public bool incendiary;   // burning pellets (dragon's breath): a jet of fire, burns what it hits
        public float Energy => 0.5f * mass * velocity * velocity;
    }

    public static class AmmoTable
    {
        static readonly Dictionary<string, AmmoType[]> table = new Dictionary<string, AmmoType[]>
        {
            ["AK-47"] = new[]
            {
                new AmmoType { name = "7.62x39 PS", desc = "Steel-core FMJ. Standard issue.", velocity = 715, mass = 0.0079f, diameter = 0.0078f, pen = 4f, wall = 1f, tracer = true },
                new AmmoType { name = "7.62x39 HP", desc = "Hollow point. Huge wounds, poor penetration.", velocity = 730, mass = 0.008f, diameter = 0.0078f, pen = 2.2f, wall = 0.45f, expand = 1.8f },
                new AmmoType { name = "7.62x39 BP", desc = "Armour piercing hardened core.", velocity = 730, mass = 0.0079f, diameter = 0.0078f, pen = 5.5f, wall = 2.2f, expand = 0.85f, tracer = true },
                new AmmoType { name = "7.62x39 US", desc = "Subsonic. Quiet, weak.", velocity = 300, mass = 0.0125f, diameter = 0.0078f, pen = 2.5f, wall = 0.5f },
            },
            ["GLOCK 17"] = new[]
            {
                new AmmoType { name = "9x19 FMJ", desc = "Full metal jacket.", velocity = 375, mass = 0.0075f, diameter = 0.009f, pen = 2f, wall = 0.6f },
                new AmmoType { name = "9x19 JHP", desc = "Jacketed hollow point. Expands in flesh.", velocity = 360, mass = 0.0075f, diameter = 0.009f, pen = 1f, wall = 0.3f, expand = 1.9f },
                new AmmoType { name = "9x19 7N21", desc = "Armour piercing.", velocity = 460, mass = 0.0052f, diameter = 0.009f, pen = 3.5f, wall = 1.4f, expand = 0.8f },
            },
            ["BERETTA M9"] = new[]
            {
                new AmmoType { name = "9x19 M882", desc = "NATO ball, as issued with the M9.", velocity = 380, mass = 0.0080f, diameter = 0.009f, pen = 2f, wall = 0.6f },
                new AmmoType { name = "9x19 JHP", desc = "Jacketed hollow point. Expands in flesh.", velocity = 360, mass = 0.0075f, diameter = 0.009f, pen = 1f, wall = 0.3f, expand = 1.9f },
                new AmmoType { name = "9x19 +P+", desc = "Hot loaded: faster, harder hitting, wears the gun.", velocity = 420, mass = 0.0075f, diameter = 0.009f, pen = 2.4f, wall = 0.7f, expand = 1.1f },
            },
            ["AR-15"] = new[]
            {
                new AmmoType { name = "5.56 M855", desc = "Green tip. Steel penetrator, fragments when it yaws.", velocity = 940, mass = 0.004f, diameter = 0.0057f, pen = 4f, wall = 1.1f, expand = 1.2f, tracer = false },
                new AmmoType { name = "5.56 M193", desc = "Fast 55 gr ball. Violent fragmentation up close.", velocity = 990, mass = 0.00356f, diameter = 0.0057f, pen = 3.2f, wall = 0.8f, expand = 1.5f },
                new AmmoType { name = "5.56 Mk262", desc = "77 gr open tip match. Accurate, tumbles hard.", velocity = 840, mass = 0.005f, diameter = 0.0057f, pen = 3f, wall = 0.8f, expand = 1.6f },
                new AmmoType { name = "5.56 M995 AP", desc = "Tungsten core armour piercing.", velocity = 1000, mass = 0.0034f, diameter = 0.0057f, pen = 5.5f, wall = 2f, expand = 0.8f },
                new AmmoType { name = "5.56 M856 Tracer", desc = "Every round glows red.", velocity = 900, mass = 0.004f, diameter = 0.0057f, pen = 3.5f, wall = 1f, tracer = true },
            },
            ["AWP"] = new[]
            {
                new AmmoType { name = ".338 LM FMJ", desc = "Magnum rifle ball.", velocity = 900, mass = 0.0162f, diameter = 0.0086f, pen = 5.5f, wall = 2.5f, tracer = true },
                new AmmoType { name = ".338 LM AP", desc = "Tungsten core. Defeats almost any plate.", velocity = 880, mass = 0.0162f, diameter = 0.0086f, pen = 6.6f, wall = 3.5f, expand = 0.85f, tracer = true },
                new AmmoType { name = ".338 LM HP", desc = "Expanding hunting round.", velocity = 910, mass = 0.0162f, diameter = 0.0086f, pen = 3.5f, wall = 1f, expand = 1.7f },
            },
            [".50 CAL"] = new[]
            {
                new AmmoType { name = ".50 BMG M33", desc = "Ball. Tears people apart.", velocity = 890, mass = 0.0421f, diameter = 0.0127f, pen = 6.5f, wall = 4f, tracer = true },
                new AmmoType { name = ".50 BMG M8 API", desc = "Armour piercing incendiary.", velocity = 900, mass = 0.042f, diameter = 0.0127f, pen = 7.5f, wall = 6f, tracer = true },
            },
            ["MOSSBERG 590"] = new[]
            {
                new AmmoType { name = "12g 00 Buck", desc = "Nine .33 cal pellets.", velocity = 400, mass = 0.0035f, diameter = 0.0084f, pen = 2f, wall = 0.3f, pellets = 9, spread = 2.4f },
                new AmmoType { name = "12g #4 Buck", desc = "27 smaller pellets, wide pattern.", velocity = 400, mass = 0.0013f, diameter = 0.0061f, pen = 1.4f, wall = 0.15f, pellets = 27, spread = 3.2f },
                new AmmoType { name = "12g Birdshot", desc = "Clouds of tiny shot. Peppers, rarely kills.", velocity = 380, mass = 0.00012f, diameter = 0.0024f, pen = 0.6f, wall = 0.05f, pellets = 60, spread = 4.5f },
                new AmmoType { name = "12g Slug", desc = "One heavy lead slug.", velocity = 440, mass = 0.028f, diameter = 0.0184f, pen = 3f, wall = 1f, expand = 1.5f, spread = 0.3f },
                new AmmoType { name = "12g Flechette", desc = "20 steel darts. Penetrates.", velocity = 460, mass = 0.0005f, diameter = 0.002f, pen = 3.5f, wall = 0.6f, expand = 0.6f, pellets = 20, spread = 1.8f },
                new AmmoType { name = "12g Dragon's Breath", desc = "Burning magnesium shards: a jet of fire out to 30 m. Burns, rarely kills.", velocity = 330, mass = 0.0004f, diameter = 0.004f, pen = 0.4f, wall = 0.02f, expand = 0.8f, pellets = 18, spread = 5f, tracer = true, incendiary = true },
            },
        };

        public static AmmoType[] For(string weapon) => table.TryGetValue(weapon, out var a) ? a : null;
    }

    public enum SightKind { Iron, RedDot, Holo, Scope4x, Scope8x }
    public enum GripKind { None, Vertical, Angled }

    /// <summary>What the player has fitted to each weapon (G menu).</summary>
    public class WeaponLoadout
    {
        public int ammo;
        public SightKind sight;
        public GripKind grip;
        // grenade
        public int fuse = 2;              // 0 impact, 1 = 2 s, 2 = 3 s, 3 = 4 s, 4 = 5 s
        public int shrapnel = 260;
        // glock
        public bool extMag;               // 33-round extended magazine
        public int fireMode;              // which of the gun's fire modes (Z): WeaponController.ModesFor
        public bool autoSwitch;           // "glock switch": full auto
        // rails and muzzle
        public bool suppressor;           // quieter, little flash, a longer gun
        public bool light;                // weapon light (X300) under the rail
        public bool laser;                // PEQ-15 style aiming laser: a red dot where the barrel points
        public bool magnifier;            // 3x flip magnifier behind a red dot / holo
        public float FuseSeconds => fuse == 0 ? -1f : fuse + 1f;
        public string FuseName => fuse == 0 ? "IMPACT" : $"{fuse + 1} SEC";
    }
}
