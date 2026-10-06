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

        public static float ConeMask(Vector2 displacement, Vector2 direction, float angle)
        {
            if (displacement.sqrMagnitude < .000001f) return 1f;
            float halfAngle = Mathf.Clamp(angle, 2f, 180f) * .5f;
            float outer = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
            float inner = Mathf.Cos((halfAngle - 1f) * Mathf.Deg2Rad);
            float t = Mathf.Clamp01((Vector2.Dot(displacement.normalized, direction) - outer) / (inner - outer));
            return t * t * (3f - 2f * t);
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
