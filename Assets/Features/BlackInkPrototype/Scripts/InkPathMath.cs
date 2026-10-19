using UnityEngine;

namespace InkRunner.BlackInkPrototype
{
    /// <summary>Shared generation rules. All distances are in world units, never screen pixels.</summary>
    public static class InkPathMath
    {
        public static float AffordableLength(float available, float costPerUnit)
        {
            return costPerUnit > 0f ? Mathf.Max(0f, available) / costPerUnit : 0f;
        }

        public static Vector2 ClipToBudget(Vector2 start, Vector2 end, float available, float costPerUnit)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            return length > 0f ? start + delta * Mathf.Min(1f, AffordableLength(available, costPerUnit) / length) : start;
        }

        public static bool GentleSegment(Vector2 a, Vector2 b, float maximumSlope)
        {
            Vector2 delta = b - a;
            return Mathf.Abs(delta.x) > 0.00001f &&
                   Mathf.Abs(delta.y) <= Mathf.Abs(delta.x) * Mathf.Tan(maximumSlope * Mathf.Deg2Rad);
        }

        // SAT against the whole convex platform quad, not only its sampled endpoints.
        // Character AABBs are deliberately conservative around the actual capsule.
        public static bool BodyIntersectsBounds(Vector2 a, Vector2 b, float thickness, Bounds bounds)
        {
            Vector2[] quad = { a, b, b + Vector2.down * thickness, a + Vector2.down * thickness };
            Vector2 center = bounds.center;
            Vector2 extents = bounds.extents;
            if (Separated(quad, center, extents, Vector2.right) || Separated(quad, center, extents, Vector2.up))
                return false;
            for (int i = 0; i < 4; i++)
            {
                Vector2 edge = quad[(i + 1) % 4] - quad[i];
                if (Separated(quad, center, extents, new Vector2(-edge.y, edge.x))) return false;
            }
            return true;
        }

        private static bool Separated(Vector2[] polygon, Vector2 center, Vector2 extents, Vector2 axis)
        {
            float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
            foreach (Vector2 point in polygon)
            {
                float projection = Vector2.Dot(point, axis);
                minimum = Mathf.Min(minimum, projection);
                maximum = Mathf.Max(maximum, projection);
            }
            float middle = Vector2.Dot(center, axis);
            float radius = Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y;
            return maximum < middle - radius || minimum > middle + radius;
        }
    }
}
