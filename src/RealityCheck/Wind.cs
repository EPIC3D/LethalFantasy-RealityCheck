using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealityCheck
{
    /// <summary>
    /// Wind you feel near a giantess: air pushed by her moving body parts (measured from the game's
    /// animation) and the jet of her breath.
    /// </summary>
    internal static class Wind
    {
        // Body parts tracked, with their thickness as a share of her height (bigger parts stir air farther).
        private static readonly (HumanBodyBones bone, double radius, string name)[] Parts =
        {
            (HumanBodyBones.LeftHand, 0.03, "hand"), (HumanBodyBones.RightHand, 0.03, "hand"),
            (HumanBodyBones.LeftLowerArm, 0.025, "arm"), (HumanBodyBones.RightLowerArm, 0.025, "arm"),
            (HumanBodyBones.LeftFoot, 0.035, "foot"), (HumanBodyBones.RightFoot, 0.035, "foot"),
            (HumanBodyBones.LeftLowerLeg, 0.04, "leg"), (HumanBodyBones.RightLowerLeg, 0.04, "leg"),
            (HumanBodyBones.LeftUpperLeg, 0.06, "thigh"), (HumanBodyBones.RightUpperLeg, 0.06, "thigh"),
            (HumanBodyBones.Hips, 0.1, "hips"), (HumanBodyBones.Chest, 0.1, "body"), (HumanBodyBones.Head, 0.07, "head"),
        };

        private static readonly Dictionary<Transform, Vector3> last = new Dictionary<Transform, Vector3>();

        /// <summary>
        /// Strongest air movement at the player from her moving body (m/s) and which part caused it.
        /// Air around a moving body moves almost as fast close by and falls off like (r / (r + d))².
        /// </summary>
        public static (double speed, string part) FromMotion(Giantess g, Vector3 player, double metresPerUnit, double height, double dt)
        {
            double best = 0;
            string bestPart = null;
            var animator = g.animator;
            if (animator == null || dt <= 0) return (0, null);
            foreach (var (bone, radius, name) in Parts)
            {
                Transform t = animator.GetBoneTransform(bone);
                if (t == null) continue;
                Vector3 now = t.position - player;   // relative to you: if she carries you, the air moves with you
                if (last.TryGetValue(t, out var before))
                {
                    double v = Vector3.Distance(now, before) * metresPerUnit / dt;
                    double r = radius * height;
                    double d = Math.Max(0, now.magnitude * metresPerUnit - r);
                    double f = r / (r + d);
                    double here = v * f * f;
                    if (here > best) { best = here; bestPart = name; }
                }
                last[t] = now;
            }
            return (best, bestPart);
        }

        private static double peak;
        private static string peakPart;

        /// <summary>Strongest recent wind from her motion (m/s) and the part causing it; decays over ~1 s.</summary>
        public static (double speed, string part) Peak => (peak, peakPart);

        /// <summary>Called every frame: instantaneous body-part speeds, kept as a decaying peak.</summary>
        private static double lastHeight;

        public static void SampleFrame(Giantess g, Vector3 player, double metresPerUnit, double height, double dt)
        {
            // While she grows every bone moves outward – that is not wind. Skip and restart the samples.
            if (Math.Abs(height - lastHeight) > 0.001 * height)
            {
                lastHeight = height;
                last.Clear();
                return;
            }
            var (now, part) = FromMotion(g, player, metresPerUnit, height, dt);
            peak *= Math.Exp(-dt / 1.0);
            if (now > peak) { peak = now; peakPart = part; }
        }

        public static void Reset()
        {
            last.Clear();
            peak = 0;
            peakPart = null;
        }

        // Body shape for warmth and touch: joints plus mid-points of the legs, each a warm sphere.
        private static readonly (HumanBodyBones a, HumanBodyBones b, double radius, string name)[] Shape =
        {
            (HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, 0.035, "foot"), (HumanBodyBones.RightFoot, HumanBodyBones.RightToes, 0.035, "foot"),
            (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.04, "shin"), (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.04, "shin"),
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 0.06, "thigh"), (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.06, "thigh"),
            (HumanBodyBones.Hips, HumanBodyBones.Hips, 0.1, "hips"), (HumanBodyBones.Chest, HumanBodyBones.Chest, 0.1, "body"),
            (HumanBodyBones.LeftHand, HumanBodyBones.LeftHand, 0.03, "hand"), (HumanBodyBones.RightHand, HumanBodyBones.RightHand, 0.03, "hand"),
            (HumanBodyBones.Head, HumanBodyBones.Head, 0.07, "head"),
        };

        /// <summary>
        /// Her radiant warmth on you (W/m², extra over open air), the closest body surface (m) and that part's name.
        /// Each part is a sphere at 34 °C; heat on a surface facing it ≈ 0.5·(r/x)² of a full warm wall (same as RadiantWm2).
        /// </summary>
        public static (double radiant, double surface, string part) Warmth(Giantess g, Vector3 player, double mpu, double height)
        {
            var animator = g.animator;
            if (animator == null) return (0, double.PositiveInfinity, null);
            double full = 0.97 * SizeStats.Sigma * (Math.Pow(SizeStats.SkinK, 4) - Math.Pow(SizeStats.AmbientK, 4));
            double view = 0, closest = double.PositiveInfinity;
            string part = null;
            foreach (var (a, b, radius, name) in Shape)
            {
                Transform ta = animator.GetBoneTransform(a), tb = animator.GetBoneTransform(b);
                if (ta == null) continue;
                Vector3 centre = tb != null ? (ta.position + tb.position) * 0.5f : ta.position;
                double r = radius * height;
                double x = Math.Max(r * 1.0001, Vector3.Distance(player, centre) * mpu);
                double q = r / x;
                view += 0.5 * q * q;   // facing-surface share, same as RadiantWm2
                double surface = x - r;
                if (surface < closest) { closest = surface; part = name; }
            }
            return (full * Math.Min(0.5, view), Math.Max(0, closest), part);
        }

        /// <summary>
        /// Her calm exhale at your distance (m/s). Real: breath volume ∝ k³ through nostrils ∝ k², over a breath
        /// ∝ √k long, so the jet speed at her nose ≈ 1.2 m/s × √k; it weakens as 6 × opening / distance.
        /// </summary>
        public static double FromBreath(SizeStats s, double mouthDistance)
        {
            double v0 = 1.2 * Math.Sqrt(s.K);
            double opening = 0.012 * s.Height;
            return v0 * Math.Min(1, 6 * opening / Math.Max(mouthDistance, 0.01));
        }
    }
}
