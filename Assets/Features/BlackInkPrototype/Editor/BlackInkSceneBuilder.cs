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
            EnvironmentAssetImporter.ConfigureAll();
            Material brushMaterial = LoadOrCreateMaterial("InkBrush.mat", "InkRunner/Black Ink Brush", Color.black);
            Material spriteMaterial = LoadOrCreateMaterial("EnvironmentSprite.mat", "Universal Render Pipeline/2D/Sprite-Unlit-Default", Color.white);
            Sprite[] platformSprites = LoadEnvironmentSprites("platform", 6);
            Sprite[] obstacleSprites = LoadEnvironmentSprites("obstacle", 2);
            PhysicsMaterial2D physicsMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(Root + "/Materials/InkSurface.physicsMaterial2D");
            if (!physicsMaterial)
            {
                physicsMaterial = new PhysicsMaterial2D("Ink surface - no friction / no bounce") { friction = 0f, bounciness = 0f };
                AssetDatabase.CreateAsset(physicsMaterial, Root + "/Materials/InkSurface.physicsMaterial2D");
            }
            Scene previousScene = UnitySceneManager.GetActiveScene();
            // Unity cannot open an additive scene beside an untitled scene. A truly empty
            // startup scene is safe to replace; a user's unsaved scene must be saved first.
            bool replaceEmptyScene = UnitySceneManager.sceneCount == 1 && previousScene.IsValid() &&
                                     string.IsNullOrEmpty(previousScene.path) && !previousScene.isDirty &&
                                     previousScene.rootCount == 0;
            for (int i = 0; i < UnitySceneManager.sceneCount; i++)
            {
                Scene loaded = UnitySceneManager.GetSceneAt(i);
                if (string.IsNullOrEmpty(loaded.path) && !replaceEmptyScene)
                    throw new System.InvalidOperationException("Save the untitled scene before rebuilding BlackInkTestScene. No unsaved scene has been changed.");
            }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                replaceEmptyScene ? NewSceneMode.Single : NewSceneMode.Additive);
            UnitySceneManager.SetActiveScene(scene);
            try
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                // 100 world pixels per unit: the 1080-high reference view spans 10.8 units.
                // Runtime tracks the current Game view height to keep 40 px / 15 px literal.
                camera.orthographicSize = 5.4f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.95f, 0.935f, 0.89f);
                camera.transform.position = new Vector3(8.1523f, 2.484f, -10f);
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
                var lightObject = new GameObject("Global Light 2D (original sprite material)");
                var light = lightObject.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Global;
                light.color = Color.white;
                light.intensity = 1f;

                var terrain = new GameObject("Endless World - random platforms + visual obstacles");
                var world = terrain.AddComponent<EndlessInkWorld>();
                world.platformSprites = platformSprites;
                world.obstacleSprites = obstacleSprites;
                world.spriteMaterial = spriteMaterial;
                world.minimumPlatformGap = 1.5f;
                world.maximumPlatformGap = 2.5f;
                var inkRoot = new GameObject("Generated Black Ink (runtime only)");

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
                // Only the prototype variant is resized. Sprite, animation frames and the
                // original Inkwarrior prefab remain untouched (210 px reference body).
                sourceInstance.transform.localScale = Vector3.one * (40f / 210f);
                GameObject variant = PrefabUtility.SaveAsPrefabAsset(sourceInstance, VariantPath);
                Object.DestroyImmediate(sourceInstance);
                GameObject runner = (GameObject)PrefabUtility.InstantiatePrefab(variant, scene);
                runner.name = "Inkwarrior (original sprite + Animator, test variant)";
                CapsuleCollider2D runnerCapsule = runner.GetComponent<CapsuleCollider2D>();
                float footOffset = (runnerCapsule.offset.y - runnerCapsule.size.y * 0.5f) * runner.transform.localScale.y;
                runner.transform.position = new Vector3(0f, -footOffset + 0.003f, 0f);

                GameObject systems = new GameObject("Black Ink Endless Runner - select for tuning");
                var controller = systems.AddComponent<BlackInkTestController>();
                controller.character = runner.GetComponent<Rigidbody2D>();
                controller.characterBody = runner.GetComponent<CapsuleCollider2D>();
                controller.characterAnimator = runner.GetComponent<Animator>();
                controller.drawingCamera = camera;
                controller.inkRoot = inkRoot.transform;
                controller.endlessMode = true;
                controller.endlessWorld = world;
                controller.lockToScreenPixels = true;
                controller.pixelsPerWorldUnit = 100f;
                controller.referencePixelHeight = 1080f;
                controller.characterHeightPixels = 40f;
                controller.inkThicknessPixels = 15f;
                controller.originalCharacterBodyHeight = 2.1f;
                controller.characterBodyHeight = 0.4f;
                controller.thicknessRatio = 0.375f;
                controller.runnerViewportX = 0.08f;
                controller.textureScale = 0.15f;
                controller.platformGap = 0f;
                controller.drawingBounds = new Rect(-100000f, -3f, 200000f, 12f);
                controller.inkMaterial = brushMaterial;
                controller.surfaceMaterial = physicsMaterial;
                controller.gaugeSource = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/InkGaugeSource.png");
                systems.AddComponent<BlackInkHud>().gaugeShader = Shader.Find("InkRunner/BlackInkPrototype/InkGaugeUI");
                world.Initialize(camera, controller.character, physicsMaterial);
                world.ResetWorld(Vector2.zero);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Unable to save the generated test scene: " + ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("Created endless runner test scene (40 px character / 15 px ink / random 150-250 px gaps): " + ScenePath);
            }
            finally
            {
                if (!replaceEmptyScene)
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (previousScene.IsValid() && previousScene.isLoaded) UnitySceneManager.SetActiveScene(previousScene);
                }
            }
        }

        private static Sprite[] LoadEnvironmentSprites(string stem, int count)
        {
            var sprites = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                string path = Root + "/Textures/Environment/" + stem + "_" + (i + 1) + ".png";
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (!sprites[i]) throw new FileNotFoundException("Environment sprite is missing or not imported as a Sprite: " + path);
            }
            return sprites;
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

    }
}
