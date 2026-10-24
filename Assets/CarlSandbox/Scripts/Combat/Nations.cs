using UnityEngine;

namespace Carl
{
    /// <summary>
    /// The trenches as a war between two real armies: you choose Ukraine or Russia in the menu, the other side is
    /// the enemy. Each army has its own uniform camo (Ukraine's MM-14 pixel pattern, Russia's EMR "tsifra"), the
    /// coloured tape round the arm that tells friend from foe up close (Ukraine blue or yellow, Russia white or
    /// red - one colour for each side, chosen for the battle), and its own guns: Ukraine mostly AK-74s and old
    /// AKMs with some AK-74Ms and Western kit, Russia the AK-74M and AK-12 with the odd AK-105. One man in eight or
    /// so carries a machine gun.
    /// </summary>
    public static class Nations
    {
        public const int Ukraine = 0, Russia = 1;

        /// <summary>The map is the trenches: armies, camo and tape (elsewhere, the plain green/red armbands).</summary>
        public static bool Active;
        static readonly Color[] band = { new Color(0.95f, 0.8f, 0.1f), new Color(0.92f, 0.92f, 0.9f) };

        public static int Of(int team) => team == Teams.Friendly ? GameSettings.Side : 1 - GameSettings.Side;
        public static string Name(int nation) => nation == Ukraine ? "UKRAINE" : "RUSSIA";
        /// <summary>Uniform camo for HumanBody.SetUniform: 1 EMR (Russia), 2 MM-14 (Ukraine).</summary>
        public static int Uniform(int nation) => nation == Russia ? 1 : 2;

        /// <summary>A new battle: each side picks its tape colour for it.</summary>
        public static void NewBattle()
        {
            band[Ukraine] = Random.value < 0.5f ? new Color(0.12f, 0.32f, 0.85f) : new Color(0.95f, 0.8f, 0.1f);
            band[Russia] = Random.value < 0.5f ? new Color(0.92f, 0.92f, 0.9f) : new Color(0.82f, 0.08f, 0.06f);
        }

        public static Color Band(int nation) => band[Mathf.Clamp(nation, 0, 1)];

        /// <summary>What a rifleman of that army carries (weighted by what they field; machine guns rare).</summary>
        public static EarlWeapon RandomWeapon(int nation)
        {
            float r = Random.value;
            if (nation == Russia)
            {
                if (r < 0.46f) return EarlWeapon.AK74M;
                if (r < 0.68f) return EarlWeapon.AK12;
                if (r < 0.77f) return EarlWeapon.AK105;
                if (r < 0.84f) return EarlWeapon.AKS74U;
                if (r < 0.92f) return EarlWeapon.RPK74;
                return EarlWeapon.PKM;
            }
            if (r < 0.40f) return EarlWeapon.AK74;
            if (r < 0.58f) return EarlWeapon.AK47;
            if (r < 0.70f) return EarlWeapon.AK74M;
            if (r < 0.77f) return EarlWeapon.AKS74U;
            if (r < 0.82f) return EarlWeapon.AR15;
            if (r < 0.88f) return EarlWeapon.RPK74;
            if (r < 0.94f) return EarlWeapon.PKM;
            return EarlWeapon.M249;
        }

        /// <summary>The rifle you start with in that army.</summary>
        public static string IssueRifle(int nation) => nation == Russia ? "AK-74M" : "AK-74";
    }
}
