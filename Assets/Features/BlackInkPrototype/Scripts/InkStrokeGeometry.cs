using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace InkRunner.BlackInkPrototype
{
    /// <summary>
    /// A single brush stroke. All points are the walkable top surface in the
    /// scrolling terrain root's local space; thickness is measured vertically down.
    /// The collision polygon contains the opaque body only. Small bristle tips and
    /// the feathered lower edge belong to the visual mesh and have no colliders.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(PolygonCollider2D))]
    public sealed class InkStrokeGeometry : MonoBehaviour
    {
        private const float CoordinateEpsilon = 0.00001f;
        private const float TipLengthRatio = 0.18f;
        private const float FeatherDepthRatio = 0.24f;
        private static readonly float[] RibbonRows = { 0f, 0.28f, 0.72f, 1f, 1f + FeatherDepthRatio };

        private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        private static readonly int TextureScaleId = Shader.PropertyToID("_TextureScale");
        private static readonly int StrokeLengthId = Shader.PropertyToID("_StrokeLength");
        private static readonly int ArcOffsetId = Shader.PropertyToID("_ArcOffset");
        private static readonly int NoiseSeedId = Shader.PropertyToID("_NoiseSeed");
        private static readonly int EdgeRoughnessId = Shader.PropertyToID("_EdgeRoughness");

        private readonly List<Vector2> points = new List<Vector2>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> textureCoordinates = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<float> arcLengths = new List<float>();
        private readonly List<Vector2> candidatePoints = new List<Vector2>();
        private IReadOnlyList<Vector2> readOnlyPoints;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private PolygonCollider2D polygonCollider;
        private Mesh mesh;
        private Material ownedFallbackMaterial;
        private MaterialPropertyBlock materialProperties;
        private float thickness = 1f;
        private float textureScale = 1f;
        private float edgeRoughness = 0.65f;
        private bool reversedTextureAnchor;
        private Vector2 originalTextureOrigin;

        public IReadOnlyList<Vector2> Points => readOnlyPoints ?? (readOnlyPoints = points.AsReadOnly());
        public float RightmostX => points.Count == 0 ? float.NegativeInfinity : points[points.Count - 1].x;
        public int PointCount => points.Count;
        public float WorldLength { get; private set; }

        private void Awake()
        {
            EnsureComponents();
        }

        public void Initialize(Material material, float thickness, float textureScale,
            float edgeRoughness, PhysicsMaterial2D physicsMaterial)
        {
            EnsureComponents();
            this.thickness = Mathf.Max(0.001f, thickness);
            this.textureScale = Mathf.Max(0.001f, textureScale);
            this.edgeRoughness = Mathf.Clamp01(edgeRoughness);
            polygonCollider.sharedMaterial = physicsMaterial;

            if (material == null)
            {
                Shader brushShader = Shader.Find("InkRunner/Black Ink Brush");
                if (brushShader != null)
                {
                    if (ownedFallbackMaterial == null)
                    {
                        ownedFallbackMaterial = new Material(brushShader)
                        {
                            name = "Black Ink Brush (runtime fallback)",
                            hideFlags = HideFlags.DontSave
                        };
                    }
                    material = ownedFallbackMaterial;
                }
            }

            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = 5;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.enabled = material != null && points.Count >= 2;

            if (points.Count >= 2)
                Rebuild();
        }

        /// <summary>
        /// Replaces the current path. Consecutive duplicate points are removed;
        /// right-to-left paths are reversed internally. A non-monotonic or invalid
        /// replacement is refused, preserving the last valid platform.
        /// </summary>
        public void SetPoints(IReadOnlyList<Vector2> sourcePoints)
        {
            EnsureComponents();
            candidatePoints.Clear();
            if (sourcePoints != null)
            {
                for (int i = 0; i < sourcePoints.Count; i++)
                {
                    Vector2 point = sourcePoints[i];
                    if (!IsFinite(point))
                        return;
                    if (candidatePoints.Count == 0 ||
                        (point - candidatePoints[candidatePoints.Count - 1]).sqrMagnitude >
                        CoordinateEpsilon * CoordinateEpsilon)
                        candidatePoints.Add(point);
                }
            }

            if (candidatePoints.Count >= 2 &&
                !TryMeasureMonotonicPath(candidatePoints, out _, out _))
                return;

            Vector2 originalStart = candidatePoints.Count > 0 ? candidatePoints[0] : Vector2.zero;
            bool reversed = candidatePoints.Count >= 2 &&
                candidatePoints[candidatePoints.Count - 1].x < candidatePoints[0].x;
            reversedTextureAnchor = reversed;
            originalTextureOrigin = originalStart;
            points.Clear();
            if (reversed)
            {
                for (int i = candidatePoints.Count - 1; i >= 0; i--)
                    points.Add(candidatePoints[i]);
            }
            else
            {
                points.AddRange(candidatePoints);
            }

            if (points.Count < 2)
            {
                WorldLength = 0f;
                mesh.Clear();
                polygonCollider.enabled = false;
                meshRenderer.enabled = false;
                return;
            }

            Rebuild();
        }

        /// <summary>
        /// Pure geometry check used by editor validation: only finite paths with a
        /// consistent, nonzero X direction can create a single-valued top surface.
        /// </summary>
        public static bool TryMeasureMonotonicPath(IReadOnlyList<Vector2> path,
            out float worldLength, out float rightmostX)
        {
            worldLength = 0f;
            rightmostX = float.NegativeInfinity;
            if (path == null || path.Count < 2 || !IsFinite(path[0]))
                return false;

            float direction = Mathf.Sign(path[path.Count - 1].x - path[0].x);
            rightmostX = path[0].x;
            for (int i = 1; i < path.Count; i++)
            {
                if (!IsFinite(path[i]))
                    return false;
                Vector2 delta = path[i] - path[i - 1];
                if (delta.x * direction <= CoordinateEpsilon)
                    return false;
                worldLength += delta.magnitude;
                rightmostX = Mathf.Max(rightmostX, path[i].x);
            }
            return IsFinite(worldLength) && worldLength > CoordinateEpsilon;
        }

        private void EnsureComponents()
        {
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();
            if (polygonCollider == null)
                polygonCollider = GetComponent<PolygonCollider2D>();
            if (materialProperties == null)
                materialProperties = new MaterialPropertyBlock();
            if (mesh == null)
            {
                mesh = new Mesh { name = "Black ink continuous brush ribbon" };
                mesh.MarkDynamic();
                meshFilter.sharedMesh = mesh;
                polygonCollider.enabled = false;
                meshRenderer.enabled = false;
            }
        }

        private void Rebuild()
        {
            arcLengths.Clear();
            arcLengths.Add(0f);
            WorldLength = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                WorldLength += Vector2.Distance(points[i - 1], points[i]);
                arcLengths.Add(WorldLength);
            }

            vertices.Clear();
            textureCoordinates.Clear();
            triangles.Clear();
            float tipLength = thickness * TipLengthRatio;
            Vector2 startTangent = (points[1] - points[0]).normalized;
            Vector2 endTangent = (points[points.Count - 1] - points[points.Count - 2]).normalized;

            AddColumn(points[0] - startTangent * tipLength, -tipLength);
            for (int i = 0; i < points.Count; i++)
                AddColumn(points[i], arcLengths[i]);
            AddColumn(points[points.Count - 1] + endTangent * tipLength, WorldLength + tipLength);

            int rows = RibbonRows.Length;
            int columnCount = points.Count + 2;
            for (int column = 0; column < columnCount - 1; column++)
            {
                for (int row = 0; row < rows - 1; row++)
                {
                    int leftTop = column * rows + row;
                    int rightTop = (column + 1) * rows + row;
                    triangles.Add(leftTop);
                    triangles.Add(rightTop);
                    triangles.Add(rightTop + 1);
                    triangles.Add(leftTop);
                    triangles.Add(rightTop + 1);
                    triangles.Add(leftTop + 1);
                }
            }

            mesh.Clear();
            mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, textureCoordinates);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();

            Vector2[] collisionPath = new Vector2[points.Count * 2];
            for (int i = 0; i < points.Count; i++)
            {
                collisionPath[i] = points[i];
                collisionPath[points.Count * 2 - 1 - i] = points[i] + Vector2.down * thickness;
            }
            polygonCollider.pathCount = 1;
            polygonCollider.SetPath(0, collisionPath);
            polygonCollider.enabled = true;

            materialProperties.SetFloat(ThicknessId, thickness);
            materialProperties.SetFloat(TextureScaleId, textureScale);
            materialProperties.SetFloat(StrokeLengthId, WorldLength / textureScale);
            // Keep already painted texture fixed when a stroke grows to the left.
            materialProperties.SetFloat(ArcOffsetId, reversedTextureAnchor ? -WorldLength / textureScale : 0f);
            materialProperties.SetFloat(NoiseSeedId, originalTextureOrigin.x * 0.731f + originalTextureOrigin.y * 0.337f);
            materialProperties.SetFloat(EdgeRoughnessId, edgeRoughness);
            meshRenderer.SetPropertyBlock(materialProperties);
            meshRenderer.enabled = meshRenderer.sharedMaterial != null;
        }

        private void AddColumn(Vector2 top, float arcLength)
        {
            for (int row = 0; row < RibbonRows.Length; row++)
            {
                vertices.Add(new Vector3(top.x, top.y - RibbonRows[row] * thickness, 0f));
                textureCoordinates.Add(new Vector2(arcLength / textureScale, RibbonRows[row]));
            }
        }

        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void OnDestroy()
        {
            if (mesh != null)
            {
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
            if (ownedFallbackMaterial != null)
            {
                if (Application.isPlaying) Destroy(ownedFallbackMaterial);
                else DestroyImmediate(ownedFallbackMaterial);
            }
        }
    }
}
