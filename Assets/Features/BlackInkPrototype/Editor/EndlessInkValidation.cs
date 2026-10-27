using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace InkRunner.BlackInkPrototype.Editor
{
    /// <summary>Runs the endless scene through actual Unity rendering and Physics2D.</summary>
    [InitializeOnLoad]
    public static class EndlessInkValidation
    {
        private const string PendingKey = "InkRunner.BlackInkPrototype.EndlessValidationPending";
        private static readonly List<string> passed = new List<string>();
        private static readonly List<string> errors = new List<string>();
        private static readonly List<string> environmentNotes = new List<string>();
        private static IEnumerator routine;
        private static double deadline;
        private static bool batch;
        private static string Output => Path.GetFullPath("Logs/EndlessInkValidationResults");

        static EndlessInkValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        // CLI: Unity -batchmode -screen-width 1920 -screen-height 1080 -projectPath <copy>
        // -executeMethod InkRunner.BlackInkPrototype.Editor.EndlessInkValidation.BuildAndValidateBatch
        // Keep graphics enabled: screenshots render the real URP camera, sprites, ink and uGUI.
        public static void BuildAndValidateBatch()
        {
            batch = true;
            EditorSceneManager.OpenScene("Assets/Scenes/PlayScene.unity");
            BlackInkSceneBuilder.BuildScene();
            StartValidation();
        }

        [MenuItem("Tools/InkRunner/Validate Endless Ink Runner (Play Mode)")]
        public static void StartValidation()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before starting validation.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Output);
            passed.Clear();
            errors.Clear();
            environmentNotes.Clear();
            SessionState.SetBool(PendingKey, true);
            Application.logMessageReceived += OnLog;
            EditorSceneManager.OpenScene(BlackInkSceneBuilder.ScenePath);
            deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
                routine = RunChecks();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (deadline > 0 && EditorApplication.timeSinceStartup > deadline)
            {
                Complete("Validation timed out.");
                return;
            }
            if (routine == null || !EditorApplication.isPlaying) return;
            try
            {
                if (!routine.MoveNext()) Complete(null);
            }
            catch (Exception exception) { Complete(exception.ToString()); }
        }

        private static void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (stack.Contains("UnityEditor.Search."))
            {
                environmentNotes.Add("Unity Editor search indexing: " + condition);
                return;
            }
            errors.Add(condition + "\n" + stack);
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("FAILED: " + description);
            passed.Add(description);
            Debug.Log("ENDLESS_INK_PASS: " + description);
        }

        private static bool Near(float a, float b, float epsilon = 0.003f) => Mathf.Abs(a - b) <= epsilon;

        private static IEnumerator RunChecks()
        {
            yield return null;
            yield return null;
            BlackInkTestController test = UnityEngine.Object.FindFirstObjectByType<BlackInkTestController>();
            Check(test && test.enabled && test.endlessMode && test.endlessWorld,
                "Dedicated scene starts in endless mode with all required references");
            test.manualPointerForValidation = true;
            // A hidden batch editor has no OS focus; deterministic input owns this seam.
            test.SendMessage("OnApplicationFocus", true);
            test.preparationSeconds = 999f;
            test.ResetTrial();
            Release(test);
            yield return null;
            yield return null;
            Vector2 spawn = test.character.position;
            Vector3 initialCameraPosition = test.drawingCamera.transform.position;
            var world = test.endlessWorld;

            Check(test.lockToScreenPixels && Near(test.pixelsPerWorldUnit, 100f) &&
                Near(test.characterBodyHeight, 0.4f) && Near(test.Thickness, 0.25f),
                "40 px character body and 25 px ink map to 0.40 / 0.25 world units at 100 px per unit");
            Check(Near(test.character.transform.localScale.y, 40f / 210f, 0.0001f),
                "Only the prototype variant uses the 40 / 210 character scale");
            Check(Near(ProjectedHeight(test.drawingCamera, test.characterBodyHeight), 40f, 0.05f) &&
                Near(ProjectedHeight(test.drawingCamera, test.Thickness), 25f, 0.05f),
                "Current Game view actually projects the body to 40 px and the ink to 25 px");
            Check(Near(test.drawingCamera.WorldToViewportPoint(test.characterBody.bounds.center).x,
                test.runnerViewportX, 0.005f) && test.runnerViewportX <= 0.1f,
                "Runner begins near the left screen edge (8 percent viewport X)");
            Check(world.platformSprites.Length == 6 && world.platformSprites.All(s => s) &&
                world.obstacleSprites.Length == 2 && world.obstacleSprites.All(s => s),
                "All six supplied platforms and both supplied obstacle sprites are linked");
            Check(world.ActivePlatformCount > 1 && world.ActivePlatformCount <= world.maximumActivePlatforms,
                "Initial random platforms cover the visible area with bounded live objects");
            Check(test.inkRoot.parent == null || !test.inkRoot.IsChildOf(test.character.transform),
                "Ink is stored in stationary world space independently of the runner");

            Vector2 first = new Vector2(spawn.x + 2f, 1f);
            Vector2 last = first + Vector2.right * 3.6f;
            Vector2 pointer = Screen(test, first);
            test.ProcessPointer(pointer, true, true, false);
            for (int i = 0; i < 20; i++) test.ProcessPointer(pointer, false, true, false);
            Release(test);
            Check(test.StrokeCount == 0 && Near(test.CurrentInk, 100f),
                "A simple click or stationary pointer creates no ink and has no cost");
            Draw(test, new[] { first, last }, 1);
            float fastCost = 100f - test.CurrentInk;
            Check(test.StrokeCount == 1 && Near(fastCost, 36f),
                "A fast 3.6-unit stroke creates continuous ink for exactly 36 ink");
            InkStrokeGeometry geometry = test.inkRoot.GetComponentInChildren<InkStrokeGeometry>();
            PolygonCollider2D inkCollider = geometry.GetComponent<PolygonCollider2D>();
            Check(inkCollider.enabled && Near(inkCollider.bounds.max.y, first.y) &&
                Near(inkCollider.bounds.size.y, 0.25f),
                "Ink has a continuous 25 px collision body extending down from the pointer path");
            Check(geometry.GetComponent<MeshRenderer>().sharedMaterial != null &&
                geometry.GetComponent<MeshFilter>().sharedMesh.vertexCount > geometry.PointCount * 2,
                "Generated ink keeps the brush material and feathered mesh");
            test.ResetTrial();
            Release(test);
            Draw(test, new[] { first, last }, 60);
            Check(Near(100f - test.CurrentInk, fastCost),
                "Fast and slow sampling charge the same completed world path length");

            test.maximumInk = 7f;
            test.ResetTrial();
            Release(test);
            Draw(test, new[] { first, last }, 1);
            Check(Near(test.CurrentInk, 0f) && Near(test.CreatedLength, 0.7f),
                "The final ink segment is clipped to the affordable 0.7 units");
            Vector2 beforeRefill = test.character.position;
            int strokesBeforeRefill = test.StrokeCount;
            test.Refill();
            Check(Near(test.CurrentInk, 7f) && test.character.position == beforeRefill &&
                test.StrokeCount == strokesBeforeRefill,
                "Refill restores ink while preserving existing platforms and the runner pose");
            test.maximumInk = 100f;
            test.ResetTrial();
            Release(test);
            yield return null;
            Check(test.StrokeCount == 0 && test.inkRoot.childCount == 0 && Near(test.CurrentInk, 100f),
                "Reset removes generated ink and restores the ink budget");

            // This upper stroke does not touch the runner. Its world mesh/collision must
            // remain fixed while the camera follows the actual simulated run.
            test.preparationSeconds = 0f;
            test.ResetTrial();
            Release(test);
            Draw(test, new[] { first, last }, 12);
            geometry = test.inkRoot.GetComponentInChildren<InkStrokeGeometry>();
            Mesh mesh = geometry.GetComponent<MeshFilter>().sharedMesh;
            Vector2[] initialUvs = mesh.uv;
            Vector3[] initialVertices = mesh.vertices;
            Vector2[] initialCollision = geometry.GetComponent<PolygonCollider2D>().GetPath(0);
            Vector3 inkWorldPosition = geometry.transform.position;
            CaptureFrame(test, Path.Combine(Output, "EndlessInk_Drawing25px.png"));
            test.SendMessage("OnApplicationFocus", true);
            Time.timeScale = 6f;
            // FixedUpdate precedes Update. The first frame can spend its entire physics
            // budget in Preparing and only enter Running afterwards. Measure once a real
            // running physics step has happened, rather than timing that transition frame.
            float readyLimit = Time.realtimeSinceStartup + 15f;
            while ((test.State != BlackInkTestController.TrialState.Running || test.character.linearVelocity.x < 0.1f) &&
                   Time.realtimeSinceStartup < readyLimit) yield return null;
            float motionCameraX = test.drawingCamera.transform.position.x;
            float motionCharacterX = test.character.position.x;
            float runStart = Time.time;
            float realLimit = Time.realtimeSinceStartup + 90f;
            pointer = Screen(test, new Vector2(spawn.x + 8f, 1.6f));
            test.ProcessPointer(pointer, true, true, false);
            float inkBeforeHold = test.CurrentInk;
            float lengthBeforeHold = test.CreatedLength;
            while (Time.time - runStart < 0.8f && Time.realtimeSinceStartup < realLimit)
            {
                test.ProcessPointer(pointer, false, true, false);
                yield return null;
            }
            Release(test);
            Check(test.State == BlackInkTestController.TrialState.Running &&
                test.character.position.x > motionCharacterX + 0.5f &&
                test.drawingCamera.transform.position.x > motionCameraX + 0.5f,
                $"Actual runner motion advances the following camera (state={test.State}, body={test.character.position}, velocity={test.character.linearVelocity}, cameraX={test.drawingCamera.transform.position.x:0.000}, initialX={initialCameraPosition.x:0.000}, simulated={Time.time - runStart:0.000}s)");
            Check(Near(test.CurrentInk, inkBeforeHold) && Near(test.CreatedLength, lengthBeforeHold),
                "Holding the pointer still while the camera scrolls creates no ink and has no cost");
            Check(geometry.transform.position == inkWorldPosition && mesh.uv.SequenceEqual(initialUvs) &&
                mesh.vertices.SequenceEqual(initialVertices) &&
                geometry.GetComponent<PolygonCollider2D>().GetPath(0).SequenceEqual(initialCollision),
                "Camera scrolling leaves painted ink position, texture UVs and collider geometry unchanged");

            int platformMask = 0, obstacleMask = 0, maximumLivePlatforms = 0, maximumLiveObstacles = 0;
            bool obstacleCollisionFree = true, runnerStayedLeft = true;
            bool captured10 = false, captured60 = false;
            int initialTotal = world.TotalPlatformsCreated;
            while (Time.time - runStart < 120f && Time.realtimeSinceStartup < realLimit)
            {
                if (test.State == BlackInkTestController.TrialState.Failed)
                    throw new InvalidOperationException("Runner fell during the continuous 120-second seam test at " +
                        test.character.position + ", elapsed=" + (Time.time - runStart));
                platformMask |= world.VisibleVariantsMask;
                obstacleMask |= world.ObstacleVariantsMask;
                maximumLivePlatforms = Mathf.Max(maximumLivePlatforms, world.ActivePlatformCount);
                maximumLiveObstacles = Mathf.Max(maximumLiveObstacles, world.ActiveObstacleCount);
                runnerStayedLeft &= Near(test.drawingCamera.WorldToViewportPoint(test.characterBody.bounds.center).x,
                    test.runnerViewportX, 0.015f);
                foreach (SpriteRenderer sprite in world.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (!sprite.sprite || !sprite.sprite.name.StartsWith("obstacle_", StringComparison.Ordinal)) continue;
                    obstacleCollisionFree &= sprite.GetComponentsInChildren<Collider2D>().Length == 0 &&
                        sprite.GetComponentsInChildren<Rigidbody2D>().Length == 0;
                }
                if (!captured10 && Time.time - runStart >= 10f)
                {
                    CaptureFrame(test, Path.Combine(Output, "EndlessInk_10seconds.png"));
                    captured10 = true;
                }
                if (!captured60 && Time.time - runStart >= 60f)
                {
                    CaptureFrame(test, Path.Combine(Output, "EndlessInk_60seconds.png"));
                    captured60 = true;
                }
                yield return null;
            }
            Check(Time.time - runStart >= 120f && test.State == BlackInkTestController.TrialState.Running,
                "Actual Physics2D runner crosses random platform seams for 120 simulated seconds without completing or falling");
            Check(test.character.position.x > spawn.x + 200f && world.TotalPlatformsCreated > initialTotal + 30,
                "The map continuously generates ahead beyond 200 world units of actual travel");
            Check(platformMask == 0x3f && obstacleMask == 0x3,
                "The long run visibly generates every platform_1 through platform_6 and both obstacle variants");
            Check(maximumLivePlatforms <= world.maximumActivePlatforms && maximumLiveObstacles <= maximumLivePlatforms,
                $"Offscreen world recycling bounds live platforms / obstacles (observed {maximumLivePlatforms} / {maximumLiveObstacles})");
            Check(obstacleCollisionFree && maximumLiveObstacles > 0,
                "Intermittent obstacle sprites have no Collider2D or Rigidbody2D hit detection");
            Check(runnerStayedLeft, "Runner remains near the left screen edge while the world scrolls");
            Check(captured10 && captured60 && File.Exists(Path.Combine(Output, "EndlessInk_60seconds.png")),
                "Saved real 1920 x 1080 Unity URP renders at 10 and 60 simulated seconds");
            Check(test.StrokeCount == 0, "Painted ink entirely behind the camera is cleaned up during the long run");

            test.preparationSeconds = 3f;
            test.ResetTrial();
            Release(test);
            Time.timeScale = 1f;
            yield return null;
            yield return null;
            Check(Near(test.character.position.x, spawn.x) && Near(test.character.position.y, spawn.y, 0.03f) &&
                Near(test.drawingCamera.transform.position.x, initialCameraPosition.x, 0.03f),
                "Reset after a long run restores the starting character and camera positions");
            Check(test.State == BlackInkTestController.TrialState.Preparing && Near(test.CurrentInk, 100f) &&
                test.StrokeCount == 0 && world.ActivePlatformCount > 1 &&
                world.TotalPlatformsCreated < initialTotal + 10,
                "Reset restarts preparation and rebuilds only the initial visible world");

            test.character.position = new Vector2(spawn.x, test.failureY - 1f);
            test.character.transform.position = new Vector3(spawn.x, test.failureY - 1f, 0f);
            Physics2D.SyncTransforms();
            yield return null;
            yield return null;
            Check(test.State == BlackInkTestController.TrialState.Failed,
                "Falling still enters the recoverable failed state in endless mode");
            test.ResetTrial();
            Release(test);
            yield return null;
            Check(test.character.simulated && test.State == BlackInkTestController.TrialState.Preparing &&
                Near(test.characterBody.bounds.center.x, spawn.x + test.characterBody.offset.x * test.character.transform.localScale.x),
                "Reset from failure restores the live physics body and its matching rendered collider");
            Check(errors.Count == 0, "No runtime errors, missing references, shader errors or exceptions during validation");
        }

        private static float ProjectedHeight(Camera camera, float worldHeight)
        {
            Vector3 origin = camera.transform.position + Vector3.forward * 10f;
            return Mathf.Abs(camera.WorldToScreenPoint(origin + Vector3.up * worldHeight).y -
                camera.WorldToScreenPoint(origin).y);
        }

        private static Vector2 Screen(BlackInkTestController test, Vector2 world) => test.drawingCamera.WorldToScreenPoint(world);

        // Fixture release must not invent cursor travel from the real pointer to (0,0).
        private static void Release(BlackInkTestController test) => test.ProcessPointer(Vector2.zero, false, false, false);

        private static void Draw(BlackInkTestController test, Vector2[] points, int subdivisions)
        {
            test.ProcessPointer(Screen(test, points[0]), true, true, false);
            for (int i = 1; i < points.Length; i++)
                for (int step = 1; step <= subdivisions; step++)
                    test.ProcessPointer(Screen(test, Vector2.Lerp(points[i - 1], points[i], step / (float)subdivisions)), false, true, false);
            test.ProcessPointer(Screen(test, points[points.Length - 1]), false, false, true);
        }

        private static void CaptureFrame(BlackInkTestController test, string path)
        {
            Camera camera = test.drawingCamera;
            Canvas canvas = test.GetComponentInChildren<Canvas>();
            RenderMode oldMode = canvas.renderMode;
            Camera oldUiCamera = canvas.worldCamera;
            float oldPlane = canvas.planeDistance, oldAspect = camera.aspect, oldSize = camera.orthographicSize;
            Vector3 oldPosition = camera.transform.position;
            RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
            Texture2D readback = null;
            try
            {
                camera.targetTexture = target;
                camera.aspect = 1920f / 1080f;
                camera.orthographicSize = 5.4f;
                camera.transform.position = new Vector3(
                    test.characterBody.bounds.center.x + (0.5f - test.runnerViewportX) * 19.2f,
                    2.484f, oldPosition.z);
                test.endlessWorld.Tick();
                test.SendMessage("UpdateHud");
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                readback = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                readback.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                readback.Apply();
                File.WriteAllBytes(path, readback.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = oldActive;
                camera.targetTexture = oldTarget;
                camera.aspect = oldAspect;
                camera.orthographicSize = oldSize;
                camera.transform.position = oldPosition;
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldUiCamera;
                canvas.planeDistance = oldPlane;
                Canvas.ForceUpdateCanvases();
                RenderTexture.ReleaseTemporary(target);
                if (readback) UnityEngine.Object.Destroy(readback);
            }
        }

        private static void Complete(string failure)
        {
            routine = null;
            SessionState.SetBool(PendingKey, false);
            Application.logMessageReceived -= OnLog;
            Time.timeScale = 1f;
            if (!string.IsNullOrEmpty(failure)) errors.Add(failure);
            Directory.CreateDirectory(Output);
            string report = "# Endless Ink Runner — Unity validation\n\nUnity: " + Application.unityVersion +
                "\nResult: " + (errors.Count == 0 ? "PASS" : "FAIL") + "\n\n" +
                string.Join("\n", passed.Select(s => "- PASS: " + s)) + "\n\n" +
                string.Join("\n\n", errors.Select(s => "ERROR: " + s)) +
                (environmentNotes.Count > 0 ? "\n\nEditor environment observations (outside prototype runtime):\n" +
                    string.Join("\n", environmentNotes.Distinct()) : "");
            File.WriteAllText(Path.Combine(Output, "ValidationReport.md"), report);
            if (!string.IsNullOrEmpty(failure)) Debug.Log("ENDLESS_INK_VALIDATION_FAILURE_DETAILS: " + failure);
            Debug.Log("ENDLESS_INK_VALIDATION_" + (errors.Count == 0 ? "PASS" : "FAIL") +
                " (" + passed.Count + " checks). " + Output);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            if (batch || Application.isBatchMode)
            {
                int exitCode = errors.Count == 0 ? 0 : 1;
                EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
            }
        }
    }
}
