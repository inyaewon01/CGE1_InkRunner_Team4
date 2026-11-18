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

        [Header("Obstacle spawning settings")]
        [Range(1, 6)] public int minimumPlatformsBetweenObstacles = 1;
        [Range(1, 6)] public int maximumPlatformsBetweenObstacles = 3;
        [Min(0.1f)] public float minimumObstacleHeight = 0.55f;
        [Min(0.1f)] public float maximumObstacleHeight = 0.95f;

        [Tooltip("씬의 obstacle_1, obstacle_2 … 를 프리팹으로 만든 뒤 여기에 연결하세요.\n" +
                 "SpriteRenderer · BoxCollider2D · Obstacle 컴포넌트가 포함된 오브젝트를 사용합니다.\n" +
                 "비워 두면 기존 비주얼 전용(충돌 없음) 방식으로 생성됩니다.")]
        public GameObject[] obstaclePrefabs = new GameObject[0];
        [Tooltip("Unique painted cells required out of ten cells across an obstacle's base.")]
        [Range(0.1f, 1f)] public float whiteInkCoverageThreshold = 0.6f;

        private int ObstacleVariantCount => Mathf.Max(obstacleSprites.Length, obstaclePrefabs != null ? obstaclePrefabs.Length : 0);

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
            public GameObject obstacle;
            public int paintedCells;
            public int paintedCellCount;
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
            nextObstacleVariant = random.Next(Mathf.Max(1, ObstacleVariantCount));
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
                AddSpriteRenderer(visual, sprite, 1);
                VisibleVariantsMask |= 1 << variant;
            }

            var platform = new Platform { root = platformObject, left = nextLeft, right = nextLeft + width };
            // The first platform is clear, then decorations occur every 1-3 platforms.
            if (TotalPlatformsCreated > 0 && --platformsUntilObstacle <= 0 && ObstacleVariantCount > 0)
            {
                platform.obstacle = CreateObstacle(platformObject.transform, width);
                platform.hasObstacle = platform.obstacle;
                if (platform.hasObstacle) ActiveObstacleCount++;
                platformsUntilObstacle = random.Next(Mathf.Max(1, minimumPlatformsBetweenObstacles),
                    Mathf.Max(minimumPlatformsBetweenObstacles, maximumPlatformsBetweenObstacles) + 1);
            }
            platforms.Add(platform);
            TotalPlatformsCreated++;
            nextLeft = platform.right + gapAfter;
            previousGap = gapAfter;
        }

        private GameObject CreateObstacle(Transform platform, float platformWidth)
        {
            int index = nextObstacleVariant++ % ObstacleVariantCount;

            // ── 프리팹 경로: obstaclePrefabs[index]가 연결되어 있으면 인스턴스화 ──
            GameObject prefab = (obstaclePrefabs != null && index < obstaclePrefabs.Length)
                ? obstaclePrefabs[index] : null;
            if (prefab != null)
                return CreateObstacleFromPrefab(prefab, platform, platformWidth, index);

            // ── 레거시 경로: 비주얼 전용 (충돌 없음) ──
            Sprite sprite = index < obstacleSprites.Length ? obstacleSprites[index] : null;
            if (!sprite) return null;
            Bounds bounds = sprite.bounds;
            Vector2 landmark = index < ObstacleLandmarks.Length ? ObstacleLandmarks[index] : new Vector2(0f, 1f);
            float height = RandomRange(minimumObstacleHeight, Mathf.Max(minimumObstacleHeight, maximumObstacleHeight));
            float scale = height / Mathf.Max(0.001f, bounds.size.y * (landmark.y - landmark.x));
            var obstacle = new GameObject($"obstacle_{index + 1} (visual only - no collision)");
            obstacle.transform.SetParent(platform, false);
            obstacle.transform.localScale = new Vector3(scale, scale, 1f);
            obstacle.transform.localPosition = new Vector3(RandomRange(platformWidth * 0.3f, platformWidth * 0.7f),
                -(bounds.min.y + bounds.size.y * landmark.x) * scale, 0f);
            AddSpriteRenderer(obstacle, sprite, 4);
            ObstacleVariantsMask |= 1 << index;
            return obstacle;
        }

        /// <summary>
        /// obstaclePrefabs 슬롯에 연결된 프리팹을 인스턴스화하여 플랫폼 위에 배치합니다.
        /// 프리팹에는 SpriteRenderer, BoxCollider2D, Obstacle 컴포넌트가 포함되어야 합니다.
        /// </summary>
        private GameObject CreateObstacleFromPrefab(GameObject prefab, Transform platform, float platformWidth, int index)
        {
            // 스프라이트 크기 정보는 프리팹의 SpriteRenderer에서 가져옵니다.
            var sr = prefab.GetComponent<SpriteRenderer>();
            if (!sr || !sr.sprite)
            {
                Debug.LogWarning($"[EndlessInkWorld] obstaclePrefabs[{index}] 에 SpriteRenderer 또는 Sprite가 없습니다. " +
                                 "레거시(비주얼 전용) 방식으로 대체합니다.", prefab);
                // 레거시 경로로 폴백
                Sprite fallback = index < obstacleSprites.Length ? obstacleSprites[index] : null;
                if (!fallback) return null;
                Bounds fb = fallback.bounds;
                Vector2 flm = index < ObstacleLandmarks.Length ? ObstacleLandmarks[index] : new Vector2(0f, 1f);
                float fh = RandomRange(minimumObstacleHeight, Mathf.Max(minimumObstacleHeight, maximumObstacleHeight));
                float fs = fh / Mathf.Max(0.001f, fb.size.y * (flm.y - flm.x));
                var fo = new GameObject($"obstacle_{index + 1} (visual only - no collision)");
                fo.transform.SetParent(platform, false);
                fo.transform.localScale = new Vector3(fs, fs, 1f);
                fo.transform.localPosition = new Vector3(RandomRange(platformWidth * 0.3f, platformWidth * 0.7f),
                    -(fb.min.y + fb.size.y * flm.x) * fs, 0f);
                AddSpriteRenderer(fo, fallback, 4);
                ObstacleVariantsMask |= 1 << index;
                return fo;
            }

            Sprite sprite = sr.sprite;
            Bounds bounds = sprite.bounds;
            Vector2 landmark = index < ObstacleLandmarks.Length ? ObstacleLandmarks[index] : new Vector2(0f, 1f);

            // 가시 영역 높이에 맞춰 균등 스케일을 계산합니다.
            float height = RandomRange(minimumObstacleHeight, Mathf.Max(minimumObstacleHeight, maximumObstacleHeight));
            float scale  = height / Mathf.Max(0.001f, bounds.size.y * (landmark.y - landmark.x));

            // 프리팹을 인스턴스화하고 플랫폼 자식으로 배치합니다.
            GameObject instance = Object.Instantiate(prefab, platform);
            instance.name = $"obstacle_{index + 1}";
            instance.transform.localScale    = new Vector3(scale, scale, 1f);
            instance.transform.localPosition = new Vector3(
                RandomRange(platformWidth * 0.3f, platformWidth * 0.7f),
                -(bounds.min.y + bounds.size.y * landmark.x) * scale,
                0f);

            // SpriteRenderer 정렬 순서를 플랫폼 레이어와 맞춥니다.
            var instSr = instance.GetComponent<SpriteRenderer>();
            if (instSr) instSr.sortingOrder = 4;

            // Rigidbody2D가 없으면 Static으로 추가합니다 (Obstacle.cs 콜백 수신에 필요).
            if (!instance.GetComponent<Rigidbody2D>())
            {
                var rb = instance.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Static;
            }

            instance.SetActive(true);
            ObstacleVariantsMask |= 1 << index;
            return instance;
        }

        public void PaintWhiteInk(Vector2 start, Vector2 end, float thickness)
        {
            if (thickness <= 0f) return;
            int required = Mathf.Clamp(Mathf.CeilToInt(whiteInkCoverageThreshold * 10f - 0.00001f), 1, 10);
            foreach (Platform platform in platforms)
            {
                if (!platform.hasObstacle || !platform.obstacle || !platform.obstacle.activeInHierarchy) continue;
                Collider2D collider = platform.obstacle.GetComponent<Collider2D>();
                SpriteRenderer renderer = platform.obstacle.GetComponent<SpriteRenderer>();
                if (!collider && !renderer) continue;
                Bounds bounds = collider ? collider.bounds : renderer.bounds;
                float cellWidth = bounds.size.x / 10f;
                if (cellWidth <= 0f) continue;
                if (!InkPathMath.BodyIntersectsBounds(start, end, thickness, bounds)) continue;

                for (int cell = 0; cell < 10; cell++)
                {
                    int bit = 1 << cell;
                    if ((platform.paintedCells & bit) != 0) continue;
                    Bounds area = new Bounds(new Vector3(bounds.min.x + (cell + 0.5f) * cellWidth,
                        bounds.center.y), new Vector3(cellWidth * 0.999f, bounds.size.y, 1f));
                    if (!InkPathMath.BodyIntersectsBounds(start, end, thickness, area)) continue;
                    platform.paintedCells |= bit;
                    platform.paintedCellCount++;
                }
                if (platform.paintedCellCount < required) continue;
                platform.obstacle.SetActive(false);
                platform.hasObstacle = false;
                ActiveObstacleCount--;
            }
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
