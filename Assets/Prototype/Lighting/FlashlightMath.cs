using UnityEngine;

namespace DivingPrototype
{
    public static class FlashlightMath
    {
        public const int MaxBlockers = 32;

        public static Vector2 RetainDirection(Vector2 candidate, Vector2 previous) =>
            candidate.sqrMagnitude > .000001f ? candidate.normalized : previous;

        public static float Strength(float distance, float halfStrengthDistance) =>
            Mathf.Pow(2f, -Mathf.Max(0f, distance) / Mathf.Max(.001f, halfStrengthDistance));

        public const float EdgeFeather = 2.5f;

        public static float CircleMask(float distance, float radius)
        {
            if (radius <= 0f) return 0f;
            float shoulder = Mathf.Max(0f, distance - radius) / EdgeFeather;
            return Mathf.Pow(2f, -2f * Mathf.Pow(shoulder, 1.5f)) * (1f - Smooth(3f, 4f, shoulder));
        }

        // CPU reference for the saved broad, exponential light profile.
        public static float AngularMask(Vector2 displacement, Vector2 direction, float innerAngle, float outerAngle)
        {
            float cosine = displacement.sqrMagnitude < .0000001f ? 1f : Vector2.Dot(displacement.normalized, direction);
            float angle = Mathf.Acos(Mathf.Clamp(cosine, -1f, 1f)) * Mathf.Rad2Deg;
            float outer = outerAngle * .5f;
            float shoulder = angle / Mathf.Max(.5f, innerAngle * .5f);
            return Mathf.Pow(2f, -shoulder * shoulder) * (1f - Smooth(outer * .65f, outer, angle));
        }

        private static float Smooth(float start, float end, float value)
        {
            float t = Mathf.Clamp01((value - start) / Mathf.Max(.000001f, end - start));
            return t * t * (3f - 2f * t);
        }

        public static float Illumination(Vector2 displacement, Vector2 direction, float angle,
            float halfStrengthDistance, float radius)
        {
            float distance = displacement.magnitude;
            float beam = AngularMask(displacement, direction, 44f, angle) * Strength(distance, halfStrengthDistance)
                * (1f - Smooth(24f, 36f, distance));
            float spill = .18f * AngularMask(displacement, direction, 90f, 178f) * Strength(distance, halfStrengthDistance * 2f)
                * (1f - Smooth(22f, 34f, distance));
            float forward = 1f - (1f - beam) * (1f - spill);
            return 1f - (1f - CircleMask(distance, radius)) * (1f - forward);
        }

        // Each row transforms a world point into the collider's normalized local box [-1, 1].
        public static void PackBox(BoxCollider2D box, out Vector4 rowX, out Vector4 rowY)
        {
            rowX = rowY = Vector4.zero;
            if (box == null || !box.isActiveAndEnabled || box.size.x <= 0f || box.size.y <= 0f
                || Mathf.Abs(box.transform.localToWorldMatrix.determinant) < .000001f) return;
            var inverse = box.transform.worldToLocalMatrix;
            var half = box.size * .5f;
            rowX = new Vector4(inverse.m00 / half.x, inverse.m01 / half.x,
                (inverse.m03 - box.offset.x) / half.x, 1f);
            rowY = new Vector4(inverse.m10 / half.y, inverse.m11 / half.y,
                (inverse.m13 - box.offset.y) / half.y, 0f);
        }

        public static bool SegmentBlocked(Vector2 origin, Vector2 endpoint, Vector4 rowX, Vector4 rowY)
        {
            if (rowX.w == 0f) return false;
            Vector2 start = new Vector2(rowX.x * origin.x + rowX.y * origin.y + rowX.z,
                rowY.x * origin.x + rowY.y * origin.y + rowY.z);
            Vector2 delta = endpoint - origin;
            Vector2 step = new Vector2(rowX.x * delta.x + rowX.y * delta.y,
                rowY.x * delta.x + rowY.y * delta.y);
            float enter = 0f, exit = 1f;
            return Slab(start.x, step.x, ref enter, ref exit)
                && Slab(start.y, step.y, ref enter, ref exit)
                && exit > .00001f && enter < 1f;
        }

        private static bool Slab(float start, float step, ref float enter, ref float exit)
        {
            if (Mathf.Abs(step) < .000001f) return Mathf.Abs(start) <= 1f;
            float a = (-1f - start) / step, b = (1f - start) / step;
            enter = Mathf.Max(enter, Mathf.Min(a, b));
            exit = Mathf.Min(exit, Mathf.Max(a, b));
            return enter <= exit;
        }
    }
}
