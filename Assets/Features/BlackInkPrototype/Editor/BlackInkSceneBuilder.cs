using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace InkRunner.BlackInkPrototype.Editor
{
    public static class BlackInkSceneBuilder
    {
        public const string Root = "Assets/Features/BlackInkPrototype";
        public const string ScenePath = Root + "/Scenes/BlackInkTestScene.unity";
        public const string VariantPath = Root + "/Prefabs/Inkwarrior_BlackInkTest.prefab";

        [MenuItem("Tools/InkRunner/Open Black Ink Test Scene")]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath)) BuildScene();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/InkRunner/Rebuild Black Ink Test Scene")]
        public static void BuildScene()
        {
            if (EditorApplication.isPlaying) return;
            foreach (string folder in new[] { "Scenes", "Prefabs", "Materials", "Textures" })
                Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Inkwarrior.prefab");
            if (!original) throw new FileNotFoundException("Original PlayScene Inkwarrior prefab is required.");
            Material brushMaterial = LoadOrCreateMaterial("InkBrush.mat", "InkRunner/Black Ink Brush", Color.black);
            Material groundMaterial = LoadOrCreateMaterial("TestGround.mat", "Universal Render Pipeline/2D/Sprite-Unlit-Default", new Color(0.18f, 0.19f, 0.19f));
            Material guideMaterial = LoadOrCreateMaterial("DrawingGuide.mat", "Universal Render Pipeline/2D/Sprite-Unlit-Default", new Color(0.67f, 0.22f, 0.16f, 0.45f));
            PhysicsMaterial2D physicsMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(Root + "/Materials/InkSurface.physicsMaterial2D");
            if (!physicsMaterial)
            {
                physicsMaterial = new PhysicsMaterial2D("Ink surface - no friction / no bounce") { friction = 0f, bounciness = 0f };
                AssetDatabase.CreateAsset(physicsMaterial, Root + "/Materials/InkSurface.physicsMaterial2D");
            }
            Mesh quad = LoadOrCreateQuad();
            Scene previousScene = UnitySceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnitySceneManager.SetActiveScene(scene);
            try
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5.5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.95f, 0.935f, 0.89f);
                camera.transform.position = new Vector3(1f, 1.4f, -10f);
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
                var lightObject = new GameObject("Global Light 2D (original sprite material)");
                var light = lightObject.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Global;
                light.color = Color.white;
                light.intensity = 1f;

                var terrain = new GameObject("Test Terrain (static - no scroll controller exists in project)");
                BoxCollider2D start = Ground("Start platform", new Vector2(-6f, -1.25f), new Vector2(12f, 2.5f), terrain.transform, quad, groundMaterial, physicsMaterial);
                BoxCollider2D arrival = Ground("Arrival platform", new Vector2(9f, -1.25f), new Vector2(12f, 2.5f), terrain.transform, quad, groundMaterial, physicsMaterial);
                var inkRoot = new GameObject("Generated Black Ink (runtime only)");
                inkRoot.transform.SetParent(terrain.transform, false);

                var guides = new GameObject("Drawing guide - visual only, no collision");
                guides.transform.SetParent(terrain.transform, false);
                for (int i = 0; i < 12; i++)
                {
                    var dash = new GameObject("Top surface guide");
                    dash.transform.SetParent(guides.transform, false);
                    dash.transform.localPosition = new Vector3(i * 0.25f + 0.11f, -0.015f, 0f);
                    dash.transform.localScale = new Vector3(0.13f, 0.018f, 1f);
                    dash.AddComponent<MeshFilter>().sharedMesh = quad;
                    var renderer = dash.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = guideMaterial;
                    renderer.sortingOrder = -1;
                }

                // Saving an instantiated source prefab creates a variant and preserves sprite/controller links.
                GameObject sourceInstance = (GameObject)PrefabUtility.InstantiatePrefab(original, scene);
                sourceInstance.name = "Inkwarrior_BlackInkTest";
                Rigidbody2D body = sourceInstance.GetComponent<Rigidbody2D>();
                body.gravityScale = 2.5f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.sharedMaterial = physicsMaterial;
                CapsuleCollider2D capsule = sourceInstance.GetComponent<CapsuleCollider2D>();
                capsule.isTrigger = false;
                capsule.sharedMaterial = physicsMaterial;
                sourceInstance.GetComponent<SpriteRenderer>().sortingOrder = 10;
                InkWarriorCollision collision = sourceInstance.GetComponent<InkWarriorCollision>();
                // Unity sends some physics callbacks even to disabled MonoBehaviours.
                // Remove this component only from the variant so undefined shared tags cannot run here.
                if (collision) Object.DestroyImmediate(collision);
                sourceInstance.transform.position = Vector3.zero;
                GameObject variant = PrefabUtility.SaveAsPrefabAsset(sourceInstance, VariantPath);
                Object.DestroyImmediate(sourceInstance);
                GameObject runner = (GameObject)PrefabUtility.InstantiatePrefab(variant, scene);
                runner.name = "Inkwarrior (original sprite + Animator, test variant)";
                runner.transform.position = new Vector3(-5f, 1.50f, 0f);

                GameObject systems = new GameObject("Black Ink Test - select for tuning");
                var controller = systems.AddComponent<BlackInkTestController>();
                controller.character = runner.GetComponent<Rigidbody2D>();
                controller.characterBody = runner.GetComponent<CapsuleCollider2D>();
                controller.characterAnimator = runner.GetComponent<Animator>();
                controller.drawingCamera = camera;
                controller.inkRoot = inkRoot.transform;
                controller.arrivalPlatform = arrival;
                controller.topSurfaceGuide = guides.transform;
                controller.inkMaterial = brushMaterial;
                controller.surfaceMaterial = physicsMaterial;
                controller.gaugeSource = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/InkGaugeSource.png");
                systems.AddComponent<BlackInkHud>().gaugeShader = Shader.Find("InkRunner/BlackInkPrototype/InkGaugeUI");
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("Created isolated test scene: " + ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previousScene.IsValid() && previousScene.isLoaded) UnitySceneManager.SetActiveScene(previousScene);
            }
        }

        private static BoxCollider2D Ground(string name, Vector2 position, Vector2 size, Transform parent,
            Mesh quad, Material material, PhysicsMaterial2D physicsMaterial)
        {
            GameObject ground = new GameObject(name);
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = position;
            var collider = ground.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.sharedMaterial = physicsMaterial;
            GameObject visual = new GameObject("Solid platform visual");
            visual.transform.SetParent(ground.transform, false);
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);
            visual.AddComponent<MeshFilter>().sharedMesh = quad;
            visual.AddComponent<MeshRenderer>().sharedMaterial = material;
            return collider;
        }

        private static Material LoadOrCreateMaterial(string filename, string shaderName, Color color)
        {
            string path = Root + "/Materials/" + filename;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                Shader shader = Shader.Find(shaderName);
                if (!shader) throw new System.InvalidOperationException("Required shader not found: " + shaderName);
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(filename) };
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_InkColor")) material.SetColor("_InkColor", new Color(0.012f, 0.013f, 0.016f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh LoadOrCreateQuad()
        {
            string path = Root + "/Materials/TestPlatformQuad.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh) return mesh;
            mesh = new Mesh { name = "Unit platform quad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f), new Vector3(0.5f, -0.5f) };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
