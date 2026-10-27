using System.Collections.Generic;
using UnityEngine;

namespace InkRunner.BlackInkPrototype
{
    /// <summary>
    /// A bounded stream of stationary platform art. The runner and camera advance through
    /// world coordinates; generated ink therefore stays attached to the place it was drawn.
    /// </summary>
    public sealed class EndlessInkWorld : MonoBehaviour
    {
        [Header("Original supplied artwork, in numeric filename order")]
        public Sprite[] platformSprites = new Sprite[6];
        public Sprite[] obstacleSprites = new Sprite[2];
        public Material spriteMaterial;

        [Header("Platform dimensions (100 screen pixels = 1 world unit)")]
        [Min(0.5f)] public float minimumPlatformWidth = 2.0f;
        [Min(0.5f)] public float maximumPlatformWidth = 3.6f;
        [Tooltip("Minimum clear distance between walking edges. 1.5 units = 150 pixels at the test scene's 100 PPU.")]
        [Min(0f)] public float minimumPlatformGap = 1.5f;
        [Tooltip("Maximum clear distance between walking edges. Each gap is sampled independently; 2.5 units = 250 pixels.")]
        [Min(0f)] public float maximumPlatformGap = 2.5f;
        [Min(1f)] public float generateAheadDistance = 6f;
        [Min(1f)] public float cleanupBehindDistance = 4f;
        [Min(8)] public int maximumActivePlatforms = 32;
        [Tooltip("A fixed seed makes Reset replay the same randomly shuffled route.")]
        public int randomSeed = 27102026;

        [Header("Decoration only: deliberately no obstacle physics")]
        [Range(1, 6)] public int minimumPlatformsBetweenObstacles = 1;
        [Range(1, 6)] public int maximumPlatformsBetweenObstacles = 3;
        [Min(0.1f)] public float minimumObstacleHeight = 0.55f;
        [Min(0.1f)] public float maximumObstacleHeight = 0.95f;

        public int ActivePlatformCount => platforms.Count;
        public int ActiveObstacleCount { get; private set; }
        public int TotalPlatformsCreated { get; private set; }
        /// <summary>Bit N records whether platform_N+1 has appeared since the last Reset.</summary>
        public int VisibleVariantsMask { get; private set; }
        /// <summary>Bit N records whether obstacle_N+1 has appeared since the last Reset.</summary>
        public int ObstacleVariantsMask { get; private set; }
        public float ActiveLeftmost => platforms.Count > 0 ? platforms[0].left : spawnPoint.x;
        public float Rightmost => platforms.Count > 0 ? platforms[platforms.Count - 1].right : spawnPoint.x;
        public float DistanceTravelled => runner ? Mathf.Max(0f, runner.position.x - spawnPoint.x) : 0f;

        // Full PNGs retain their original transparent margins. These source landmarks
        // identify the stone lip's left/right edges and walking plane, without changing pixels.
        // Each vector is (left / image width, right / image width, top / image height),
        // where top is measured from the BOTTOM as in Unity's Sprite coordinate system.
        private static readonly Vector3[] PlatformLandmarks =
        {
            new Vector3(42f / 768f, 734f / 768f, 1f - 232f / 2048f),
            new Vector3(59f / 768f, 710f / 768f, 1f - 260f / 2048f),
            new Vector3(74f / 992f, 913f / 992f, 1f - 284f / 1586f),
            new Vector3(82f / 992f, 930f / 992f, 1f - 203f / 1586f),
            new Vector3(59f / 1659f, 1600f / 1659f, 1f - 173f / 948f),
            new Vector3(70f / 1659f, 1613f / 1659f, 1f - 220f / 948f)
        };

        // (bottom, top) of the visible object, normalized from the bottom of its PNG.
        private static readonly Vector2[] ObstacleLandmarks =
        {
            new Vector2(1f - 1148f / 1215f, 1f - 80f / 1215f),
            new Vector2(1f - 1526f / 1536f, 1f - 20f / 1536f)
        };

        private sealed class Platform
        {
            public GameObject root;
            public float left, right;
            public bool hasObstacle;
        }

        private readonly List<Platform> platforms = new List<Platform>();
        private readonly List<int> variantBag = new List<int>();
        [SerializeField, HideInInspector] private Transform generatedRoot;
        private Camera worldCamera;
        private Rigidbody2D runner;
        private PhysicsMaterial2D groundMaterial;
        private System.Random random;
        private Vector2 spawnPoint;
        private float nextLeft, previousGap;
        private int platformsUntilObstacle, nextObstacleVariant, previousPlatformVariant = -1;

        public void Initialize(Camera camera, Rigidbody2D character, PhysicsMaterial2D material)
        {
            worldCamera = camera;
            runner = character;
            groundMaterial = material;
        }

        /// <param name="bodyFoot">World position of the runner's feet at the start.</param>
        public void ResetWorld(Vector2 bodyFoot)
        {
            ClearGeneratedPlatforms();
            spawnPoint = bodyFoot;
            random = new System.Random(randomSeed);
            variantBag.Clear();
            previousPlatformVariant = -1;
            TotalPlatformsCreated = ActiveObstacleCount = 0;
            VisibleVariantsMask = ObstacleVariantsMask = 0;
            platformsUntilObstacle = 2;
            previousGap = 0f;
            nextObstacleVariant = random.Next(Mathf.Max(1, obstacleSprites.Length));
            float left = worldCamera ? CameraEdge(0f) : bodyFoot.x - 2f;
            nextLeft = Mathf.Min(left - 1f, bodyFoot.x - 0.75f);
            var container = new GameObject("Generated platforms (bounded stream)");
            generatedRoot = container.transform;
            generatedRoot.SetParent(transform, false);
            Tick();
            Physics2D.SyncTransforms();
        }

        /// <summary>Call after advancing the camera; generates ahead and releases offscreen art.</summary>
        public void Tick()
        {
            if (random == null || !generatedRoot) return;
            float cameraLeft = worldCamera ? CameraEdge(0f) : spawnPoint.x - 2f;
            float cameraRight = worldCamera ? CameraEdge(1f) : spawnPoint.x + 20f;
            float safeBehind = Mathf.Min(cameraLeft - cleanupBehindDistance,
                runner ? runner.position.x - cleanupBehindDistance : cameraLeft - cleanupBehindDistance);
            while (platforms.Count > 0 && platforms[0].right < safeBehind)
                ReleaseFirst();

            // A reset, test teleport, or large camera jump should populate the new view
            // immediately instead of spending frames allocating already invisible tiles.
            if (platforms.Count == 0)
            {
                float visibleStart = Mathf.Min(cameraLeft - 1f,
                    runner ? runner.position.x - 0.75f : cameraLeft - 1f);
                nextLeft = Mathf.Max(nextLeft, visibleStart);
            }

            float targetRight = cameraRight + generateAheadDistance;
            int limit = Mathf.Max(8, maximumActivePlatforms);
            while (nextLeft < targetRight && platforms.Count < limit)
                CreatePlatform();
        }

        private float CameraEdge(float viewportX)
        {
            float distance = Mathf.Abs(worldCamera.transform.position.z - transform.position.z);
            return worldCamera.ViewportToWorldPoint(new Vector3(viewportX, 0.5f, distance)).x;
        }

        private void CreatePlatform()
        {
            int variant = TakePlatformVariant();
            Sprite sprite = variant >= 0 ? platformSprites[variant] : null;
            float width = RandomRange(Mathf.Max(0.5f, minimumPlatformWidth),
                Mathf.Max(minimumPlatformWidth, maximumPlatformWidth));
            // Give the spawn area a generous uninterrupted first platform.
            if (TotalPlatformsCreated == 0) width = Mathf.Max(width, spawnPoint.x - nextLeft + 1.5f);
            float gapAfter = RandomRange(Mathf.Max(0f, minimumPlatformGap),
                Mathf.Max(Mathf.Max(0f, minimumPlatformGap), maximumPlatformGap));
            GameObject platformObject = new GameObject($"Platform {TotalPlatformsCreated + 1:0000} - platform_{variant + 1}");
            platformObject.transform.SetParent(generatedRoot, false);
            platformObject.transform.position = new Vector3(nextLeft, spawnPoint.y, transform.position.z);

            // Horizontal edges have no upright seams to snag the small running capsule.
            // Adjacent edge hints keep contacts smooth across zero-gap tile boundaries.
            EdgeCollider2D surface = platformObject.AddComponent<EdgeCollider2D>();
            surface.points = new[] { Vector2.zero, new Vector2(width, 0f) };
            surface.sharedMaterial = groundMaterial;
            surface.edgeRadius = 0f;
            surface.useAdjacentStartPoint = TotalPlatformsCreated > 0 && previousGap <= 0.0001f;
            surface.useAdjacentEndPoint = gapAfter <= 0.0001f;
            surface.adjacentStartPoint = new Vector2(-1f, 0f);
            surface.adjacentEndPoint = new Vector2(width + 1f, 0f);

            if (sprite)
            {
                Vector3 landmark = variant < PlatformLandmarks.Length ? PlatformLandmarks[variant] : new Vector3(0f, 1f, 1f);
                Bounds bounds = sprite.bounds;
                float scale = width / Mathf.Max(0.001f, bounds.size.x * (landmark.y - landmark.x));
                var visual = new GameObject(sprite.name + " (original aspect ratio)");
                visual.transform.SetParent(platformObject.transform, false);
                visual.transform.localScale = new Vector3(scale, scale, 1f);
                visual.transform.localPosition = new Vector3(
                    -(bounds.min.x + bounds.size.x * landmark.x) * scale,
                    -(bounds.min.y + bounds.size.y * landmark.z) * scale, 0f);
                AddSpriteRenderer(visual, sprite, 0);
                VisibleVariantsMask |= 1 << variant;
            }

            var platform = new Platform { root = platformObject, left = nextLeft, right = nextLeft + width };
            // The first platform is clear, then decorations occur every 1-3 platforms.
            if (TotalPlatformsCreated > 0 && --platformsUntilObstacle <= 0 && obstacleSprites.Length > 0)
            {
                platform.hasObstacle = CreateObstacle(platformObject.transform, width);
                if (platform.hasObstacle) ActiveObstacleCount++;
                platformsUntilObstacle = random.Next(Mathf.Max(1, minimumPlatformsBetweenObstacles),
                    Mathf.Max(minimumPlatformsBetweenObstacles, maximumPlatformsBetweenObstacles) + 1);
            }
            platforms.Add(platform);
            TotalPlatformsCreated++;
            nextLeft = platform.right + gapAfter;
            previousGap = gapAfter;
        }

        private bool CreateObstacle(Transform platform, float platformWidth)
        {
            int index = nextObstacleVariant++ % obstacleSprites.Length;
            Sprite sprite = obstacleSprites[index];
            if (!sprite) return false;
            Bounds bounds = sprite.bounds;
            Vector2 landmark = index < ObstacleLandmarks.Length ? ObstacleLandmarks[index] : new Vector2(0f, 1f);
            float height = RandomRange(minimumObstacleHeight, Mathf.Max(minimumObstacleHeight, maximumObstacleHeight));
            float scale = height / Mathf.Max(0.001f, bounds.size.y * (landmark.y - landmark.x));
            var obstacle = new GameObject($"obstacle_{index + 1} (visual only - no collision)");
            obstacle.transform.SetParent(platform, false);
            obstacle.transform.localScale = new Vector3(scale, scale, 1f);
            obstacle.transform.localPosition = new Vector3(RandomRange(platformWidth * 0.3f, platformWidth * 0.7f),
                -(bounds.min.y + bounds.size.y * landmark.x) * scale, 0f);
            // Behind the runner so passing through this intentionally non-colliding art is clear.
            AddSpriteRenderer(obstacle, sprite, 4);
            ObstacleVariantsMask |= 1 << index;
            return true;
        }

        private void AddSpriteRenderer(GameObject target, Sprite sprite, int sortingOrder)
        {
            SpriteRenderer renderer = target.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            if (spriteMaterial) renderer.sharedMaterial = spriteMaterial;
        }

        private int TakePlatformVariant()
        {
            if (variantBag.Count == 0)
            {
                for (int i = 0; i < platformSprites.Length; i++)
                    if (platformSprites[i]) variantBag.Add(i);
                for (int i = variantBag.Count - 1; i > 0; i--)
                {
                    int other = random.Next(i + 1);
                    int value = variantBag[i]; variantBag[i] = variantBag[other]; variantBag[other] = value;
                }
                // Avoid repeating the same appearance at the shuffle-bag boundary.
                int last = variantBag.Count - 1;
                if (last > 0 && variantBag[last] == previousPlatformVariant)
                {
                    int value = variantBag[last]; variantBag[last] = variantBag[0]; variantBag[0] = value;
                }
            }
            if (variantBag.Count == 0) return -1;
            int end = variantBag.Count - 1;
            int selected = variantBag[end];
            variantBag.RemoveAt(end);
            previousPlatformVariant = selected;
            return selected;
        }

        private float RandomRange(float minimum, float maximum) =>
            Mathf.Lerp(minimum, maximum, (float)random.NextDouble());

        private void ReleaseFirst()
        {
            Platform platform = platforms[0];
            if (platform.hasObstacle) ActiveObstacleCount--;
            RemoveOwnedObject(platform.root);
            platforms.RemoveAt(0);
        }

        private void ClearGeneratedPlatforms()
        {
            if (!generatedRoot) generatedRoot = transform.Find("Generated platforms (bounded stream)");
            if (generatedRoot) RemoveOwnedObject(generatedRoot.gameObject);
            generatedRoot = null;
            platforms.Clear();
        }

        private static void RemoveOwnedObject(GameObject target)
        {
            if (!target) return;
            target.SetActive(false);
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private void OnValidate()
        {
            minimumPlatformWidth = Mathf.Max(0.5f, minimumPlatformWidth);
            maximumPlatformWidth = Mathf.Max(minimumPlatformWidth, maximumPlatformWidth);
            minimumPlatformGap = Mathf.Max(0f, minimumPlatformGap);
            maximumPlatformGap = Mathf.Max(minimumPlatformGap, maximumPlatformGap);
            maximumActivePlatforms = Mathf.Max(8, maximumActivePlatforms);
            maximumPlatformsBetweenObstacles = Mathf.Max(minimumPlatformsBetweenObstacles, maximumPlatformsBetweenObstacles);
            maximumObstacleHeight = Mathf.Max(minimumObstacleHeight, maximumObstacleHeight);
        }
    }
}
