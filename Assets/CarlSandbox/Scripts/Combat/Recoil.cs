using UnityEngine;

namespace Carl
{
    /// <summary>
    /// How each gun kicks, scaled to the real thing (the cartridge, the gun's weight and how it's built): how far a
    /// shot throws the muzzle up and sideways (degrees, held at the hip - aiming braced takes some of it off), how
    /// much the muzzle flips in the hands, how the climb builds over a burst, and how wild the sideways dance is.
    ///
    /// The pattern of a burst is not random noise and not a fixed track either: the sideways kick follows smooth
    /// noise along the burst (seeded fresh for every burst, leaning one way or the other), with a little true
    /// randomness on top. So it has a shape you can learn to fight, but never quite the same shape twice.
    /// </summary>
    public struct Recoil
    {
        public float up, side;      // degrees per shot
        public float build;         // how much the climb grows over the first rounds of a burst (x)
        public float wander;        // how far the sideways dance swings (x side)
        public float first;         // the first shot of a burst kicks this much harder (x)
        public float flip;          // how hard the muzzle flips in your hands (x the view kick)
        public float jitter;        // pure randomness on every shot (x)

        public static Recoil For(string gun)
        {
            switch (gun)
            {
                // 7.62x39 out of a 3.6 kg rifle with a plain slant brake: a lot of climb, and it jumps about -
                // first up and to the right, then dancing left and right as the burst goes on
                case "AK-47": return new Recoil { up = 2.1f, side = 1.15f, build = 0.6f, wander = 1.6f, first = 1.3f, flip = 1.25f, jitter = 0.35f };
                // 5.56 out of a 3 kg carbine with a buffer and a straight stock: much flatter, a smaller wander
                case "AR-15": return new Recoil { up = 1.25f, side = 0.5f, build = 0.35f, wander = 1.1f, first = 1.2f, flip = 0.9f, jitter = 0.25f };
                // 9 mm pistols: a sharp snap up that comes back fast; light in the hand, so a lot of flip
                case "GLOCK 17": return new Recoil { up = 2.8f, side = 0.85f, build = 0.25f, wander = 1.3f, first = 1.1f, flip = 1.6f, jitter = 0.4f };
                case "BERETTA M9": return new Recoil { up = 2.5f, side = 0.75f, build = 0.2f, wander = 1.2f, first = 1.1f, flip = 1.5f, jitter = 0.35f };
                // .338 magnum bolt gun: one big heavy shove
                case "AWP": return new Recoil { up = 6.5f, side = 1.4f, build = 0f, wander = 0.6f, first = 1f, flip = 1.2f, jitter = 0.3f };
                // .50 BMG: enormous, even with the brake and 13 kg of rifle
                case ".50 CAL": return new Recoil { up = 9f, side = 2.2f, build = 0.1f, wander = 0.8f, first = 1f, flip = 1.1f, jitter = 0.35f };
                // 12 gauge pump: a hard punch into the shoulder and a big rise
                case "MOSSBERG 590": return new Recoil { up = 11f, side = 2.6f, build = 0f, wander = 0.8f, first = 1f, flip = 1.7f, jitter = 0.4f };
                case "TASER": return new Recoil { up = 0.4f, side = 0.1f, build = 0f, wander = 0.2f, first = 1f, flip = 0.4f, jitter = 0.2f };
                default: return new Recoil { up = 2f, side = 0.7f, build = 0.3f, wander = 1f, first = 1.1f, flip = 1f, jitter = 0.3f };
            }
        }

        /// <summary>The kick of shot n of a burst (x side, y up, degrees).</summary>
        public Vector2 Shot(int n, float seed, float lean)
        {
            // the climb builds over the first few rounds, then holds
            float grow = 1f + build * Mathf.Clamp01(n / 5f);
            float v = up * grow * (n == 0 ? first : 1f) * (1f + (Random.value - 0.5f) * jitter);
            // sideways: early rounds lean one way, then the gun dances back and forth along smooth noise
            float early = lean * Mathf.Lerp(0.55f, 0f, Mathf.Clamp01(n / 6f));
            float dance = (Mathf.PerlinNoise(n * 0.38f + seed, seed * 0.5f) - 0.5f) * 2f * wander * Mathf.Clamp01(0.35f + n / 6f);
            float h = side * (early + dance + (Random.value - 0.5f) * jitter);
            return new Vector2(h, v);
        }
    }
}
