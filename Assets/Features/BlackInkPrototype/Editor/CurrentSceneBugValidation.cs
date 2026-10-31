using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace InkRunner.BlackInkPrototype.Editor
{
    /// <summary>
    /// Regression probe for the SAVED current scene. Never invokes the scene builder,
    /// writes scene assets, changes the renderer pipeline, or creates a new game system.
    /// Baseline and strict runs execute exactly the same checks and retain failures.
    /// </summary>
    [InitializeOnLoad]
    public static class CurrentSceneBugValidation
    {
        private const string ScenePath = "Assets/Features/BlackInkPrototype/Scenes/BlackInkTestScene.unity";
        private const string Pending = "InkRunner.CurrentSceneBugValidation.Pending";
        private const string Baseline = "InkRunner.CurrentSceneBugValidation.Baseline";
        private const string PointerOnly = "InkRunner.CurrentSceneBugValidation.PointerOnly";
        private const string SlopeLimits = "InkRunner.CurrentSceneBugValidation.SlopeLimits";
        private const string IncludeSlopeLimits = "InkRunner.CurrentSceneBugValidation.IncludeSlopeLimits";
        private const string OriginalScale = "InkRunner.CurrentSceneBugValidation.TimeScale";
        private static readonly List<string> results = new List<string>();
        private static readonly List<string> failures = new List<string>();
        private static readonly List<string> observations = new List<string>();
        private static readonly List<string> physicsRows = new List<string>();
        private static readonly Queue<Func<IEnumerator>> stages = new Queue<Func<IEnumerator>>();
        private static IEnumerator current;
        private static BlackInkTestController test;
        private static double deadline;
        private static int lastFrame = -1;
        private static float configuredPreparation;
        private static bool finishing;
        private static string Output => Path.GetFullPath("Logs/CurrentSceneBugValidationResults");

        static CurrentSceneBugValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
        }

        // Keep graphics enabled. Do not use -quit: this entry exits AFTER Play Mode.
        // Unity -batchmode -screen-width 1920 -screen-height 1080 -projectPath <project>
        // -executeMethod InkRunner.BlackInkPrototype.Editor.CurrentSceneBugValidation.ProbeBaseline
        public static void ProbeBaseline() => Begin(true);
        public static void ProbeBaselineBatch() => Begin(true);
        public static void ValidateBatch() => Begin(false);
        public static void ProbePointerBaseline() => Begin(true, true);
        public static void ValidatePointerBatch() => Begin(false, true);
        public static void ProbeSlopeLimitsBatch() => Begin(true, true, true);
        public static void ValidateWithSlopeLimitsBatch() => Begin(false, false, false, true);

        [MenuItem("Tools/InkRunner/Validate Saved Current Scene (A B C)")]
        public static void ValidateCurrentScene() => Begin(false);

        private static void Begin(bool baseline, bool pointerOnly = false, bool slopeLimits = false, bool includeSlopeLimits = false)
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before validating the saved scene.");
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("The existing test scene is required; no scene will be rebuilt.", ScenePath);
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Baseline, baseline);
            SessionState.SetBool(PointerOnly, pointerOnly);
            SessionState.SetBool(SlopeLimits, slopeLimits);
            SessionState.SetBool(IncludeSlopeLimits, includeSlopeLimits);
            SessionState.SetFloat(OriginalScale, Time.timeScale);
            SessionState.SetBool(Pending, true);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                results.Clear(); failures.Clear(); observations.Clear(); physicsRows.Clear(); stages.Clear();
                current = null;
                finishing = false;
                deadline = EditorApplication.timeSinceStartup + 180;
                lastFrame = -1;
                Time.timeScale = 1f;
                Application.logMessageReceived += OnLog;
                stages.Enqueue(InspectSavedRuntime);
                if (!SessionState.GetBool(PointerOnly, false))
                {
                    stages.Enqueue(RenderVisibility);
                    stages.Enqueue(Preparation);
                    stages.Enqueue(ResetDuringPreparation);
                    stages.Enqueue(ResetDuringRunning);
                    stages.Enqueue(RapidResets);
                }
                stages.Enqueue(PhysicsFixtures);
                if (SessionState.GetBool(IncludeSlopeLimits, false)) stages.Enqueue(AdditionalSlopeLimitFixtures);
                if (!SessionState.GetBool(Baseline, false) && !SessionState.GetBool(PointerOnly, false)) stages.Enqueue(TitleStartEntry);
                stages.Enqueue(FinalFrame);
            }
            else if (state == PlayModeStateChange.EnteredEditMode && !finishing)
                Complete("Play Mode ended before all checks completed.");
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false) || finishing) return;
            if (deadline > 0 && EditorApplication.timeSinceStartup > deadline)
            {
                Complete("180 s wall-clock timeout; earlier observations remain in the report.");
                return;
            }
            if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (current == null)
            {
                if (stages.Count == 0) { Complete(null); return; }
                current = stages.Dequeue()();
            }
            try
            {
                if (!current.MoveNext()) { current = null; WriteReport(false); }
            }
            catch (Exception exception)
            {
                Fail("Stage exception (remaining stages continue): " + exception);
                current = null;
                WriteReport(false);
            }
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (stack != null && stack.Contains("UnityEditor.Search."))
                observations.Add("Editor indexing environment: " + message);
            else failures.Add("Unity runtime log: " + message + "\n" + stack);
        }

        private static void Check(bool condition, string description)
        {
            results.Add((condition ? "PASS: " : "FAIL: ") + description);
            if (!condition) failures.Add(description);
            Debug.Log("CURRENT_SCENE_" + (condition ? "PASS: " : "FAIL: ") + description);
        }

        private static void Fail(string description) => Check(false, description);
        private static string F(float number) => number.ToString("0.0000", CultureInfo.InvariantCulture);
        private static bool Near(float a, float b, float epsilon = 0.006f) => Mathf.Abs(a - b) <= epsilon;

        private static IEnumerator InspectSavedRuntime()
        {
            yield return null;
            yield return null;
            test = UnityEngine.Object.FindFirstObjectByType<BlackInkTestController>();
            Check(test && test.enabled && test.character && test.characterBody && test.endlessWorld,
                "Existing saved BlackInkTestScene enters actual Play Mode with its original references.");
            if (!test) yield break;
            configuredPreparation = test.preparationSeconds;
            test.manualPointerForValidation = true;
            test.SendMessage("OnApplicationFocus", true);
            Release();
            test.ResetTrial();
            Release();
            observations.Add("Scene: " + test.gameObject.scene.path + "; Unity " + Application.unityVersion +
                "; graphics=" + SystemInfo.graphicsDeviceType + "; screen=" + Screen.width + "x" + Screen.height +
                "; render pipeline=" + (GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "built-in"));
            observations.Add("Original preparationSeconds=" + F(configuredPreparation) +
                "; body shape=" + test.characterBody.direction + "; world size=" + test.characterBody.bounds.size +
                "; gravity=" + F(test.character.gravityScale) + "; CCD=" + test.character.collisionDetectionMode +
                "; material friction=" + (test.surfaceMaterial ? F(test.surfaceMaterial.friction) : "none") +
                "; fixedDeltaTime=" + F(Time.fixedDeltaTime) + "; runSpeed=" + F(test.runSpeed));
            var backgrounds = Backgrounds();
            Check(backgrounds.Length > 0, "Saved user background survives scene opening and runtime Reset.");
            foreach (SpriteRenderer renderer in backgrounds) observations.Add("BACKGROUND " + RendererInfo(renderer));
            SpriteRenderer[] platforms = Platforms();
            Check(platforms.Length > 0, "A: runtime generated platforms have SpriteRenderers.");
            foreach (SpriteRenderer renderer in platforms)
            {
                observations.Add("PLATFORM " + RendererInfo(renderer));
                bool cameraLayer = (test.drawingCamera.cullingMask & (1 << renderer.gameObject.layer)) != 0;
                Check(renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sprite &&
                    renderer.color.a > 0.01f && renderer.sharedMaterial && cameraLayer && renderer.bounds.size.sqrMagnitude > 0f,
                    "A: " + renderer.transform.parent.name + " sprite/alpha/material/culling/bounds are renderable.");
                foreach (SpriteRenderer background in backgrounds)
                {
                    Bounds foregroundBounds = renderer.bounds, backgroundBounds = background.bounds;
                    if (foregroundBounds.max.x < backgroundBounds.min.x || foregroundBounds.min.x > backgroundBounds.max.x ||
                        foregroundBounds.max.y < backgroundBounds.min.y || foregroundBounds.min.y > backgroundBounds.max.y) continue;
                    int platformLayer = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID);
                    int backgroundLayer = SortingLayer.GetLayerValueFromID(background.sortingLayerID);
                    Check(platformLayer > backgroundLayer || (platformLayer == backgroundLayer &&
                        renderer.sortingOrder > background.sortingOrder),
                        "A: " + renderer.transform.parent.name + " is explicitly above overlapping background " +
                        background.name + " (platform=" + renderer.sortingLayerName + "/" + renderer.sortingOrder +
                        ", background=" + background.sortingLayerName + "/" + background.sortingOrder + ").");
                }
            }
            Physics2D.SyncTransforms();
            RaycastHit2D[] support = Physics2D.RaycastAll(new Vector2(test.characterBody.bounds.center.x,
                test.characterBody.bounds.min.y + 0.04f), Vector2.down, 0.15f);
            Check(support.Any(hit => hit.collider && hit.collider != test.characterBody &&
                hit.collider.transform.IsChildOf(test.endlessWorld.transform)),
                "A: actual generated preparation platform supports the spawn feet (downward physics ray).");
        }

        private static string RendererInfo(SpriteRenderer renderer) =>
            renderer.name + "; active=" + renderer.gameObject.activeInHierarchy + "; enabled=" + renderer.enabled +
            "; sprite=" + (renderer.sprite ? renderer.sprite.name : "NULL") + "; alpha=" + F(renderer.color.a) +
            "; material=" + (renderer.sharedMaterial ? renderer.sharedMaterial.name : "NULL") +
            "; shader=" + (renderer.sharedMaterial && renderer.sharedMaterial.shader ? renderer.sharedMaterial.shader.name : "NULL") +
            "; sorting=" + renderer.sortingLayerName + "/" + renderer.sortingOrder + "; objectLayer=" + renderer.gameObject.layer +
            "; cameraMask=" + test.drawingCamera.cullingMask + "; bounds=" + renderer.bounds +
            "; inCameraFrustum=" + GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(test.drawingCamera), renderer.bounds);

        private static SpriteRenderer[] Backgrounds() => UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .Where(renderer => renderer.name.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        private static SpriteRenderer[] Platforms() => test.endlessWorld.GetComponentsInChildren<SpriteRenderer>()
            .Where(renderer => renderer.sprite && renderer.sprite.name.StartsWith("platform_", StringComparison.OrdinalIgnoreCase)).ToArray();

        private static IEnumerator RenderVisibility()
        {
            if (!test) yield break;
            test.ResetTrial(); Release();
            SpriteRenderer[] backgrounds = Backgrounds(), platforms = Platforms();
            var state = backgrounds.Concat(platforms).Distinct().ToDictionary(renderer => renderer, renderer => renderer.enabled);
            Texture2D normal = null, noBackground = null, noPlatforms = null, neither = null;
            try
            {
                normal = Capture("A_Preparing_Actual.png");
                foreach (SpriteRenderer background in backgrounds) background.enabled = false;
                noBackground = Capture("A_Preparing_BackgroundHidden.png");
                foreach (SpriteRenderer platform in platforms) platform.enabled = false;
                neither = Capture("A_Preparing_BothHidden.png");
                foreach (SpriteRenderer background in backgrounds) background.enabled = state[background];
                noPlatforms = Capture("A_Preparing_PlatformsHidden.png");
                int visible = ChangedPixels(normal, noPlatforms), available = ChangedPixels(noBackground, neither);
                float ratio = available > 0 ? visible / (float)available : 0f;
                observations.Add("A rendered differential (1920x1080 real URP + existing HUD): platform-contributing pixels with background=" +
                    visible + ", without background=" + available + ", visibility ratio=" + F(ratio) +
                    ". Four immediate captures share one physics pose; only the named renderers are temporarily toggled.");
                Check(available >= 100, "A: runtime platform art produces actual camera pixels when background is temporarily hidden.");
                Check(visible >= 100 && ratio >= 0.10f,
                    "A: actual preparation render visibly contains runtime platforms with user background enabled (pixel ratio=" + F(ratio) + ").");
                if (available >= 100 && ratio < 0.10f)
                    observations.Add("A BASELINE ROOT CAUSE: platform pixels appear after hiding background, confirming renderer occlusion rather than missing platform physics/art.");
            }
            finally
            {
                foreach (var pair in state) if (pair.Key) pair.Key.enabled = pair.Value;
                foreach (Texture2D texture in new[] { normal, noBackground, noPlatforms, neither })
                    if (texture) UnityEngine.Object.Destroy(texture);
            }
        }

        private static IEnumerator Preparation()
        {
            if (!HasExistingPreparation()) yield break;
            test.ResetTrial(); Release();
            float started = Time.time;
            float originalX = test.character.position.x, originalY = test.character.position.y;
            float cameraX = test.drawingCamera.transform.position.x;
            string signature = PlatformSignature();
            float maxX = 0f, maxY = 0f, maxDistance = 0f, maxMapMotion = 0f;
            Vector2 a = new Vector2(test.characterBody.bounds.center.x + 1f, 0.45f);
            Draw(new[] { a, a + Vector2.right * 0.35f });
            Check(test.StrokeCount > 0 && test.CreatedLength > 0.25f && test.CurrentInk < test.maximumInk,
                "B: existing preparation permits actual pointer drawing and charges ink.");
            string[] labels = test.GetComponentsInChildren<TextMeshProUGUI>().Select(label => label.text).ToArray();
            Check(labels.Any(label => label.Contains("READY") && label.Contains("s")),
                "B: existing READY decimal-seconds HUD is present; no new 3/2/1 interface is created.");
            int transitions = 0;
            var previous = test.State;
            while (test.State == BlackInkTestController.TrialState.Preparing && Time.time - started < 5f)
            {
                maxX = Mathf.Max(maxX, Mathf.Abs(test.character.position.x - originalX));
                maxY = Mathf.Max(maxY, Mathf.Abs(test.character.position.y - originalY));
                maxDistance = Mathf.Max(maxDistance, test.DistanceTravelled);
                maxMapMotion = Mathf.Max(maxMapMotion, Mathf.Abs(test.drawingCamera.transform.position.x - cameraX));
                if (test.State != previous) transitions++;
                previous = test.State;
                yield return null;
            }
            if (test.State != previous) transitions++;
            float elapsed = Time.time - started;
            Check(maxX < 0.006f && maxY < 0.04f && maxDistance < 0.006f && maxMapMotion < 0.006f && signature == PlatformSignature(),
                "B: preparation keeps runner/map/distance still: dx=" + F(maxX) + ", dy=" + F(maxY) +
                ", distance=" + F(maxDistance) + ", cameraDx=" + F(maxMapMotion) + ".");
            Check(test.State == BlackInkTestController.TrialState.Running && transitions == 1 &&
                elapsed >= configuredPreparation - 0.06f && elapsed <= configuredPreparation + 0.35f,
                "B: existing preparation enters Running exactly once after " + F(elapsed) + " scaled seconds (configured=" + F(configuredPreparation) + ").");
            Texture2D image = Capture("B_ExistingPreparationFinished.png"); UnityEngine.Object.Destroy(image);
        }

        private static bool HasExistingPreparation()
        {
            if (!test) return false;
            if (configuredPreparation <= 0f)
            {
                observations.Add("B SKIPPED: saved scene has no existing preparation delay. Per user instruction this harness adds no countdown or UI.");
                return false;
            }
            Check(Near(configuredPreparation, 3f), "B: saved preparation is the existing three-second setting (" + F(configuredPreparation) + " s).");
            return true;
        }

        private static IEnumerator ResetDuringPreparation()
        {
            if (!HasExistingPreparation()) yield break;
            test.ResetTrial(); Release();
            float start = Time.time;
            while (Time.time - start < 0.65f) yield return null;
            test.ResetTrial(); Release();
            IEnumerator check = ObserveReset("Reset during preparation");
            while (check.MoveNext()) yield return check.Current;
        }

        private static IEnumerator ResetDuringRunning()
        {
            if (!HasExistingPreparation()) yield break;
            test.ResetTrial(); Release();
            float until = Time.time + configuredPreparation + 0.35f;
            while (Time.time < until) yield return null;
            Check(test.State == BlackInkTestController.TrialState.Running && test.DistanceTravelled > 0.1f,
                "B: running reset fixture first observes real runner motion.");
            test.ResetTrial(); Release();
            IEnumerator check = ObserveReset("Reset during running");
            while (check.MoveNext()) yield return check.Current;
        }

        private static IEnumerator RapidResets()
        {
            if (!HasExistingPreparation()) yield break;
            // Nine brief resets plus the final observed reset = exactly ten calls.
            for (int i = 0; i < 9; i++)
            {
                test.ResetTrial(); Release();
                float until = Time.time + 0.045f;
                while (Time.time < until) yield return null;
            }
            test.ResetTrial(); Release();
            IEnumerator check = ObserveReset("Ten rapid Resets (old countdown cancellation)");
            while (check.MoveNext()) yield return check.Current;
        }

        private static IEnumerator ObserveReset(string name)
        {
            float start = Time.time, initialX = test.character.position.x;
            int starts = 0;
            var previous = test.State;
            bool early = false;
            float runAt = -1f;
            while (Time.time - start < configuredPreparation + 0.25f)
            {
                float elapsed = Time.time - start;
                if (test.State == BlackInkTestController.TrialState.Running && previous != test.State)
                { starts++; runAt = elapsed; }
                if (elapsed < configuredPreparation - 0.06f &&
                    (test.State != BlackInkTestController.TrialState.Preparing || Mathf.Abs(test.character.position.x - initialX) > 0.006f)) early = true;
                previous = test.State;
                yield return null;
            }
            Check(!early && starts == 1 && runAt >= configuredPreparation - 0.06f &&
                runAt <= configuredPreparation + 0.35f && test.State == BlackInkTestController.TrialState.Running,
                "B: " + name + " starts one fresh delay; early=" + early + ", starts=" + starts + ", runAt=" + F(runAt) + " s.");
        }

        private static string PlatformSignature() => string.Join("|", test.endlessWorld.GetComponentsInChildren<EdgeCollider2D>()
            .OrderBy(edge => edge.transform.position.x).Select(edge => edge.name + edge.transform.position.ToString("F5")));

        private static IEnumerator PhysicsFixtures()
        {
            if (!test) yield break;
            observations.Add("C method: SAME saved scene, SAME Rigidbody2D/CapsuleCollider2D, SAME production InkStrokeGeometry, material and FixedUpdate motor. " +
                "Only Editor fixture terrain is added. Physics2D simulationMode=Script; every 0.02 s step calls production FixedUpdate then Physics2D.Simulate. " +
                "The controller Update/LateUpdate are disabled during this isolated measurement; original colliders/configuration are restored in finally.");
            bool wasEnabled = test.enabled, worldEnabled = test.endlessWorld.enabled;
            SimulationMode2D oldMode = Physics2D.simulationMode;
            RigidbodyInterpolation2D oldInterpolation = test.character.interpolation;
            float oldAnimationSpeed = test.characterAnimator ? test.characterAnimator.speed : 0f;
            var originalColliders = UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None)
                .Where(collider => collider != test.characterBody && !collider.transform.IsChildOf(test.character.transform))
                .ToDictionary(collider => collider, collider => collider.enabled);
            try
            {
                test.enabled = false;
                test.endlessWorld.enabled = false;
                if (test.characterAnimator) test.characterAnimator.speed = 0f;
                test.character.interpolation = RigidbodyInterpolation2D.None;
                foreach (var pair in originalColliders) pair.Key.enabled = false;
                Physics2D.simulationMode = SimulationMode2D.Script;
                if (!SessionState.GetBool(PointerOnly, false))
                {
                foreach (float slope in new[] { 0f, 10f, -10f, 20f, -20f, 35f, -35f })
                {
                    RunFixtureSafely("slope_" + slope.ToString("+0;-0;0", CultureInfo.InvariantCulture),
                        SlopePath(slope, 1), false, false);
                    yield return null;
                }
                RunFixtureSafely("connected_flat_up10_down10_flat", TransitionPath(10f), false, false); yield return null;
                RunFixtureSafely("connected_flat_up20_down20_flat", TransitionPath(20f), false, false); yield return null;
                RunFixtureSafely("connected_flat_up35_down35_flat", TransitionPath(35f), false, false); yield return null;
                RunFixtureSafely("dense_collinear_20_300points", SlopePath(20f, 300), false, false); yield return null;
                RunFixtureSafely("dense_collinear_20_600points", SlopePath(20f, 600), false, false); yield return null;
                RunFixtureSafely("dense_small_jitter_10", JitterPath(10f), false, false); yield return null;
                RunFixtureSafely("growing_stroke_actor_on_existing_valid_part", SlopePath(10f, 80), true, false); yield return null;
                RunFixtureSafely("raised_endpoint_007_control", RaisedPath(0.07f), false, true); yield return null;
                RunFixtureSafely("raised_endpoint_015_control", RaisedPath(0.15f), false, true); yield return null;
                }
                bool slopeLimitProbe = SessionState.GetBool(SlopeLimits, false);
                foreach (float angle in slopeLimitProbe ? new[] { 34f, 35f, 40f, 45f } : new[] { 10f, 20f, 30f })
                    foreach (int pixelStep in new[] { 1, 2 })
                        foreach (bool reverse in new[] { false, true })
                        {
                            RunPointerFixtureSafely(angle, pixelStep, reverse);
                            yield return null;
                        }
                if (slopeLimitProbe)
                {
                    RunFixtureSafely("slope_limit_vertical_wall_015_control", RaisedPath(0.15f), false, true);
                    yield return null;
                }
            }
            finally
            {
                Physics2D.simulationMode = oldMode;
                test.character.interpolation = oldInterpolation;
                foreach (var pair in originalColliders) if (pair.Key) pair.Key.enabled = pair.Value;
                if (test.characterAnimator) test.characterAnimator.speed = oldAnimationSpeed;
                test.endlessWorld.enabled = worldEnabled;
                test.enabled = wasEnabled;
                test.ResetTrial(); Release();
                Physics2D.SyncTransforms();
            }
        }

        private static IEnumerator AdditionalSlopeLimitFixtures()
        {
            bool previousPointerOnly = SessionState.GetBool(PointerOnly, false);
            bool previousSlopeLimits = SessionState.GetBool(SlopeLimits, false);
            SessionState.SetBool(PointerOnly, true);
            SessionState.SetBool(SlopeLimits, true);
            observations.Add("Combined strict run: normal C fixtures completed; now append the dedicated 34/35/40/45 degree boundary diagnostics and separate upright-wall control to the same report/CSV. Normal strict assertions remain unchanged.");
            try
            {
                IEnumerator boundary = PhysicsFixtures();
                try { while (boundary.MoveNext()) yield return boundary.Current; }
                finally { (boundary as IDisposable)?.Dispose(); }
            }
            finally
            {
                SessionState.SetBool(PointerOnly, previousPointerOnly);
                SessionState.SetBool(SlopeLimits, previousSlopeLimits);
            }
        }

        private static void RunFixtureSafely(string name, List<Vector2> path, bool growing, bool endpointControl)
        {
            try { RunFixture(name, path, growing, endpointControl); }
            catch (Exception exception) { Fail("C fixture " + name + " threw; later fixtures continue: " + exception); }
            WriteReport(false);
        }

        private static void RunPointerFixtureSafely(float degrees, int pixelStep, bool reverse)
        {
            string name = "pointer_integer_" + degrees.ToString("0", CultureInfo.InvariantCulture) + "deg_" +
                pixelStep + "px_" + (reverse ? "right_to_left" : "left_to_right");
            Camera camera = test.drawingCamera;
            Vector3 oldCameraPosition = camera.transform.position;
            float oldAspect = camera.aspect, oldSize = camera.orthographicSize;
            try { RunPointerFixture(name, degrees, pixelStep, reverse); }
            catch (Exception exception) { Fail("C pointer fixture " + name + " threw; later fixtures continue: " + exception); }
            finally
            {
                test.SendMessage("ClearStrokes");
                camera.transform.position = oldCameraPosition; camera.aspect = oldAspect; camera.orthographicSize = oldSize;
                Physics2D.SyncTransforms();
                WriteReport(false);
            }
        }

        private static void PutFeetAt(float x, float y)
        {
            Bounds bounds = test.characterBody.bounds;
            Vector3 position = test.character.transform.position;
            position.x += x - bounds.center.x;
            position.y += y - bounds.min.y;
            test.character.simulated = false;
            test.character.transform.position = position;
            test.character.position = position;
            test.character.rotation = 0f;
            test.character.linearVelocity = Vector2.zero;
            test.character.angularVelocity = 0f;
            test.character.simulated = true;
            Physics2D.SyncTransforms();
        }

        private static bool TryGeneratedTopAt(InkStrokeGeometry[] strokes, float x, out float y)
        {
            y = float.NegativeInfinity;
            bool found = false;
            foreach (InkStrokeGeometry stroke in strokes)
                for (int i = 1; i < stroke.Points.Count; i++)
                {
                    Vector2 a = stroke.transform.TransformPoint(stroke.Points[i - 1]);
                    Vector2 b = stroke.transform.TransformPoint(stroke.Points[i]);
                    if (Mathf.Abs(b.x - a.x) <= 0.00001f || x < Mathf.Min(a.x, b.x) - 0.00001f ||
                        x > Mathf.Max(a.x, b.x) + 0.00001f) continue;
                    float top = Mathf.Lerp(a.y, b.y, Mathf.InverseLerp(a.x, b.x, x));
                    y = Mathf.Max(y, top);
                    found = true;
                }
            return found;
        }

        private static void RunPointerFixture(string name, float degrees, int pixelStep, bool reverse)
        {
            bool slopeLimitProbe = SessionState.GetBool(SlopeLimits, false);
            test.SendMessage("ClearStrokes");
            test.Refill();
            SetState(BlackInkTestController.TrialState.Preparing);
            PutFeetAt(0f, 0f); // Draw above, not through, the real actor.
            Camera camera = test.drawingCamera;
            camera.orthographicSize = Screen.height / 200f;
            camera.aspect = Screen.width / (float)Screen.height;
            float tangent = Mathf.Tan(degrees * Mathf.Deg2Rad);
            // Boundary diagnostics retain 100 PPU and reserve 120 px at each end
            // for the existing HUD. Only their length changes to fit a short Game view.
            float span = slopeLimitProbe ? Mathf.Min(4.8f,
                Mathf.Max(0.8f, (Screen.height - 240f) / 100f) / Mathf.Max(tangent, 0.001f)) : 4.8f;
            camera.transform.position = slopeLimitProbe ?
                new Vector3(span * 0.5f, 2f + tangent * span * 0.5f, camera.transform.position.z) :
                new Vector3(2.5f, 3.3f, camera.transform.position.z);
            test.manualPointerForValidation = true;
            test.SendMessage("OnApplicationFocus", true); Release();
            float beforeLength = test.CreatedLength, beforeInk = test.CurrentInk;
            Vector2 screenA = camera.WorldToScreenPoint(new Vector2(0f, 2f));
            Vector2 screenB = camera.WorldToScreenPoint(new Vector2(span, 2f + tangent * span));
            int leftPixel = Mathf.RoundToInt(screenA.x), rightPixel = Mathf.RoundToInt(screenB.x);
            int begin = reverse ? rightPixel : leftPixel, end = reverse ? leftPixel : rightPixel;
            int direction = reverse ? -1 : 1;
            int events = 0, overLimitSamples = 0;
            Vector2 previous = Vector2.zero, last = Vector2.zero;
            for (int x = begin; reverse ? x >= end : x <= end; x += direction * pixelStep)
            {
                float interpolation = Mathf.InverseLerp(screenA.x, screenB.x, x);
                Vector2 screen = new Vector2(x, Mathf.Round(Mathf.Lerp(screenA.y, screenB.y, interpolation)));
                if (events > 0 && Mathf.Abs(Mathf.Atan2(screen.y - previous.y, Mathf.Abs(screen.x - previous.x)) * Mathf.Rad2Deg) > test.maximumSlope + 0.001f)
                    overLimitSamples++;
                test.ProcessPointer(screen, events == 0, true, false);
                previous = last = screen;
                events++;
            }
            test.ProcessPointer(last, false, false, true);
            InkStrokeGeometry[] strokes = test.inkRoot.GetComponentsInChildren<InkStrokeGeometry>();
            PolygonCollider2D[] colliders = strokes.Select(stroke => stroke.GetComponent<PolygonCollider2D>()).ToArray();
            float created = test.CreatedLength - beforeLength, charged = beforeInk - test.CurrentInk;
            int shapes = colliders.Sum(collider => collider.shapeCount), points = strokes.Sum(stroke => stroke.PointCount);
            float maximumGeneratedSlope = 0f;
            int generatedSegments = 0, generatedOverLimit = 0;
            foreach (InkStrokeGeometry stroke in strokes)
                for (int i = 1; i < stroke.Points.Count; i++)
                {
                    Vector2 delta = stroke.Points[i] - stroke.Points[i - 1];
                    float angle = Mathf.Abs(Mathf.Atan2(delta.y, Mathf.Abs(delta.x)) * Mathf.Rad2Deg);
                    maximumGeneratedSlope = Mathf.Max(maximumGeneratedSlope, angle);
                    generatedSegments++;
                    if (angle > test.maximumSlope + 0.005f) generatedOverLimit++;
                }
            observations.Add("C INPUT GENERATION " + name + ": events=" + events + ", nominalPPU=100, actualProjectedPPU=" +
                F(camera.pixelHeight / (2f * camera.orthographicSize)) + ", localPointerSamplesOverSlopeLimit=" + overLimitSamples +
                ", generatedStrokes=" + test.StrokeCount + ", points=" + points + ", colliderShapes=" + shapes +
                ", createdLength=" + F(created) + ", inkCharge=" + F(charged) + ", maximumSlope=" + F(test.maximumSlope) +
                ", minimumSpacing=" + F(test.MinimumPointSpacing) + ", requestedHorizontalSpan=" + F(span) +
                ", generatedSegments=" + generatedSegments + ", maximumGeneratedSegmentSlope=" + F(maximumGeneratedSlope) +
                ", generatedSegmentsOverCap=" + generatedOverLimit + ". Actual public ProcessPointer/AppendTo path; no direct SetPoints fixture is used.");
            if (!slopeLimitProbe)
                Check(strokes.Length == 1 && created > 4.6f,
                    "C: " + name + " generates one continuous drawable gentle uphill (strokes=" + strokes.Length + ", length=" + F(created) + ").");
            else
            {
                Check(generatedOverLimit == 0,
                    "LIMIT: " + name + " keeps every actual generated top segment at or below the unchanged " + F(test.maximumSlope) +
                    " degree cap (max=" + F(maximumGeneratedSlope) + ", segments=" + generatedSegments + ").");
                observations.Add("LIMIT diagnostic only: " + degrees + " degree nominal pointer intent does not imply acceptance or one continuous stroke; actual geometry and traversal determine supported behavior.");
            }
            Check(Near(charged, created * test.costPerUnit, 0.04f),
                "C: " + name + " charges only actual generated ink length.");
            if (strokes.Length == 0 && !slopeLimitProbe) { Fail("C: " + name + " generated no collider to simulate."); return; }
            if (name == "pointer_integer_20deg_1px_left_to_right")
            {
                Texture2D image = Capture("C_Pointer_Integer20deg_1px_Generated.png"); UnityEngine.Object.Destroy(image);
            }
            const float step = 0.02f;
            // Start on an interior, actually generated top segment. The pointer can
            // leave a subpixel endpoint pending; starting at x=0 would then test the
            // polygon's leading end cap rather than the requested middle-slope case.
            const float physicsStartX = 0.30f;
            bool supportFound = TryGeneratedTopAt(strokes, physicsStartX, out float generatedTopY);
            float physicsStartTop = supportFound ? generatedTopY : 2f + tangent * physicsStartX;
            PutFeetAt(physicsStartX, physicsStartTop + 0.025f);
            SetState(BlackInkTestController.TrialState.Preparing);
            for (int i = 0; i < 20; i++) { test.SendMessage("FixedUpdate"); Physics2D.Simulate(step); }
            float startX = test.characterBody.bounds.center.x;
            var initialContacts = new ContactPoint2D[32];
            int initialContactCount = test.characterBody.GetContacts(ContactFilter2D.noFilter, initialContacts);
            observations.Add("C INPUT PHYSICS SETUP " + name + ": drawing actor feet remained at y=0; physics-only start now uses x=" +
                F(physicsStartX) + ", actualGeneratedTopFound=" + supportFound + ", setupTopY=" + F(physicsStartTop) +
                ", initialFootY=" + F(physicsStartTop + 0.025f) + ", settledCenterX=" + F(startX) +
                ", settledFootY=" + F(test.characterBody.bounds.min.y) + ", settledContacts=" + initialContactCount +
                ". This fixture-only correction avoids the leading polygon end cap; production input generation, motor, material and physics stepping are unchanged. " +
                (supportFound ? "Height was sampled from the actual generated top segments." :
                    "No generated segment exists at this x; nominal-height placement is reported and actual missing-support/fall behavior remains measurable."));
            float minimumVx = float.PositiveInfinity, maximumVx = float.NegativeInfinity, minimumSpeed = float.PositiveInfinity, maximumSpeed = 0f;
            float maximumStall = 0f, stall = 0f, maximumPenetration = 0f, grounded = 0f, air = 0f, maximumAir = 0f;
            int samples = 0, contactCount = 0;
            bool sunk = false;
            string firstStall = "none";
            var contacts = new ContactPoint2D[64];
            SetState(BlackInkTestController.TrialState.Running);
            float targetX = span - 0.15f;
            while (samples < 300 && test.characterBody.bounds.center.x < targetX)
            {
                float before = test.character.position.x;
                test.SendMessage("FixedUpdate"); Physics2D.Simulate(step);
                Vector2 velocity = test.character.linearVelocity;
                minimumVx = Mathf.Min(minimumVx, velocity.x); maximumVx = Mathf.Max(maximumVx, velocity.x);
                minimumSpeed = Mathf.Min(minimumSpeed, velocity.magnitude); maximumSpeed = Mathf.Max(maximumSpeed, velocity.magnitude);
                int count = test.characterBody.GetContacts(ContactFilter2D.noFilter, contacts);
                contactCount += count;
                bool touchingGround = false;
                for (int i = 0; i < count; i++)
                {
                    if (contacts[i].normal.y > 0.25f) touchingGround = true;
                    maximumPenetration = Mathf.Max(maximumPenetration, -contacts[i].separation);
                }
                if (touchingGround) { grounded += step; air = 0f; }
                else { air += step; maximumAir = Mathf.Max(maximumAir, air); }
                if (test.character.position.x - before < step * test.runSpeed * 0.15f)
                {
                    stall += step; maximumStall = Mathf.Max(maximumStall, stall);
                    if (stall >= 0.10f && firstStall == "none")
                        firstStall = "t=" + F(samples * step) + ", position=" + test.character.position.ToString("F5") +
                            ", velocity=" + velocity.ToString("F5") + ", contacts=" + string.Join(";", contacts.Take(count).Select(contact =>
                                "p=" + contact.point.ToString("F5") + "/n=" + contact.normal.ToString("F5") + "/separation=" + F(contact.separation)));
                }
                else stall = 0f;
                if (test.characterBody.bounds.min.y < 2f + tangent * test.characterBody.bounds.center.x - 0.12f) sunk = true;
                samples++;
                if (test.characterBody.bounds.min.y < -5f) break;
            }
            float duration = samples * step, progress = test.characterBody.bounds.center.x - startX;
            bool reached = test.characterBody.bounds.center.x >= targetX;
            float average = duration > 0f ? progress / duration : 0f;
            physicsRows.Add(string.Join(",", new[] { name, F(duration), F(progress), F(average), F(minimumVx), F(maximumVx),
                F(minimumSpeed), F(maximumSpeed), F(grounded), F(maximumAir), F(maximumStall), F(maximumPenetration),
                contactCount.ToString(), "0", reached.ToString(), sunk.ToString() }));
            observations.Add("C INPUT PHYSICS " + name + ": generatedStrokes=" + strokes.Length + ", reached=" + reached +
                ", progress=" + F(progress) + ", seconds=" + F(duration) + ", avgVx=" + F(average) + ", minVx=" + F(minimumVx) +
                ", maxStall=" + F(maximumStall) + ", grounded=" + F(grounded) + ", maxAir=" + F(maximumAir) +
                ", maxPenetration=" + F(maximumPenetration) + ", firstStall=" + firstStall + ".");
            if (!slopeLimitProbe)
                Check(reached && maximumStall < 0.3f && !sunk,
                    "C: " + name + " actual pointer-created terrain traverses without sustained middle-slope stall (progress=" +
                    F(progress) + ", maxStall=" + F(maximumStall) + ").");
            else observations.Add("LIMIT traversal observation " + name + ": traversed=" + (reached && maximumStall < 0.3f && !sunk) +
                ", nominalAngle=" + F(degrees) + ", targetX=" + F(targetX) + ", finalFoot=" + test.characterBody.bounds.min.ToString("F5") +
                ". No continuity/traversal assertion is imposed by this boundary-only probe.");
        }

        private static void RunFixture(string name, List<Vector2> path, bool growing, bool endpointControl)
        {
            GameObject fixture = new GameObject("EDITOR ONLY terrain fixture " + name);
            InkStrokeGeometry geometry = fixture.AddComponent<InkStrokeGeometry>();
            geometry.Initialize(test.inkMaterial, test.Thickness, test.textureScale, test.edgeRoughness, test.surfaceMaterial);
            GameObject second = null;
            const float step = 0.02f;
            try
            {
                List<Vector2> initial = growing ? path.Where(point => point.x <= 2.1f).ToList() : path;
                geometry.SetPoints(initial);
                if (endpointControl)
                {
                    // This deliberate vertical wall is a separate control, NOT a connected gentle slope.
                    geometry.SetPoints(new[] { new Vector2(-1f, 2f), new Vector2(1.3f, 2f) });
                    second = new GameObject("EDITOR ONLY raised separate stroke");
                    var raised = second.AddComponent<InkStrokeGeometry>();
                    raised.Initialize(test.inkMaterial, test.Thickness, test.textureScale, test.edgeRoughness, test.surfaceMaterial);
                    raised.SetPoints(new[] { path[1], path[2] });
                }
                PolygonCollider2D terrain = geometry.GetComponent<PolygonCollider2D>();
                Bounds previousBounds = test.characterBody.bounds;
                Vector3 pose = test.character.transform.position;
                pose.x -= previousBounds.center.x;
                pose.y += 2.025f - previousBounds.min.y;
                test.character.simulated = false;
                test.character.transform.position = pose;
                test.character.position = pose;
                test.character.rotation = 0f;
                test.character.linearVelocity = Vector2.zero;
                test.character.angularVelocity = 0f;
                test.character.simulated = true;
                SetState(BlackInkTestController.TrialState.Preparing);
                Physics2D.SyncTransforms();
                for (int i = 0; i < 20; i++) { test.SendMessage("FixedUpdate"); Physics2D.Simulate(step); }
                float startX = test.characterBody.bounds.center.x;
                float targetX = endpointControl ? 4.8f : 4.8f;
                float minVx = float.PositiveInfinity, maxVx = float.NegativeInfinity, minSpeed = float.PositiveInfinity, maxSpeed = 0f;
                float maxPenetration = 0f, maxStall = 0f, stall = 0f, maxAir = 0f, air = 0f, groundedSeconds = 0f;
                int samples = 0, contacts = 0, growthEdits = 0;
                string stallDetail = "none";
                bool sunk = false;
                var contactBuffer = new ContactPoint2D[32];
                var filter = ContactFilter2D.noFilter;
                SetState(BlackInkTestController.TrialState.Running);
                while (samples < 300 && test.characterBody.bounds.center.x < targetX)
                {
                    float before = test.character.position.x;
                    if (growing && samples % 2 == 0)
                    {
                        float end = Mathf.Min(6f, 2.1f + samples * 0.05f);
                        var extended = path.Where(point => point.x <= end).ToList();
                        geometry.SetPoints(extended);
                        growthEdits++;
                    }
                    test.SendMessage("FixedUpdate");
                    Physics2D.Simulate(step);
                    Vector2 velocity = test.character.linearVelocity;
                    minVx = Mathf.Min(minVx, velocity.x); maxVx = Mathf.Max(maxVx, velocity.x);
                    minSpeed = Mathf.Min(minSpeed, velocity.magnitude); maxSpeed = Mathf.Max(maxSpeed, velocity.magnitude);
                    int count = test.characterBody.GetContacts(filter, contactBuffer);
                    bool ground = false;
                    for (int contact = 0; contact < count; contact++)
                    {
                        if (contactBuffer[contact].normal.y > 0.25f) ground = true;
                        maxPenetration = Mathf.Max(maxPenetration, -contactBuffer[contact].separation);
                    }
                    contacts += count;
                    if (ground) { groundedSeconds += step; air = 0f; }
                    else { air += step; maxAir = Mathf.Max(maxAir, air); }
                    if (test.character.position.x - before < step * test.runSpeed * 0.15f)
                    {
                        stall += step; maxStall = Mathf.Max(maxStall, stall);
                        if (stall >= 0.10f && stallDetail == "none")
                            stallDetail = "at t=" + F(samples * step) + ", body=" + test.character.position.ToString("F5") +
                                ", foot=" + test.characterBody.bounds.min.ToString("F5") + ", velocity=" + velocity.ToString("F5") +
                                ", contacts=" + string.Join(";", contactBuffer.Take(count).Select(contact =>
                                    "p=" + contact.point.ToString("F5") + "/n=" + contact.normal.ToString("F5") + "/separation=" + F(contact.separation)));
                    }
                    else stall = 0f;
                    float top = PathY(path, test.characterBody.bounds.center.x);
                    if (!endpointControl && test.characterBody.bounds.min.y < top - 0.12f) sunk = true;
                    samples++;
                    if (test.characterBody.bounds.min.y < -5f) break;
                }
                float duration = samples * step, progress = test.characterBody.bounds.center.x - startX;
                float average = duration > 0f ? progress / duration : 0f;
                bool reached = test.characterBody.bounds.center.x >= targetX;
                physicsRows.Add(string.Join(",", new[] { name, F(duration), F(progress), F(average), F(minVx), F(maxVx),
                    F(minSpeed), F(maxSpeed), F(groundedSeconds), F(maxAir), F(maxStall), F(maxPenetration), contacts.ToString(),
                    growthEdits.ToString(), reached.ToString(), sunk.ToString() }));
                string metric = "C: " + name + "; reached=" + reached + "; dx=" + F(progress) + "; time=" + F(duration) +
                    "; avgVx=" + F(average) + "; vx=[" + F(minVx) + "," + F(maxVx) + "]; speed=[" + F(minSpeed) + "," + F(maxSpeed) +
                    "]; grounded=" + F(groundedSeconds) + "; maxAir=" + F(maxAir) + "; maxStall=" + F(maxStall) +
                    "; maxPenetration=" + F(maxPenetration) + "; contactSamples=" + contacts + "; growthEdits=" + growthEdits +
                    "; sourcePoints=" + geometry.PointCount + "; colliderShapeCount=" + terrain.shapeCount +
                    "; useDelaunayMesh=" + terrain.useDelaunayMesh + "; firstStall=" + stallDetail + ".";
                observations.Add(metric);
                if (endpointControl)
                {
                    observations.Add("DIAGNOSTIC ONLY: " + name + " deliberately has an upright " +
                        F(path[1].y - 2f) + " unit leading edge. Its result is kept separate from continuous slope regression checks.");
                    if (SessionState.GetBool(SlopeLimits, false))
                        Check(!reached && maxStall >= 0.3f,
                            "LIMIT: existing motor does not automatically climb a separate 0.15-unit upright wall (reached=" + reached +
                            ", maxStall=" + F(maxStall) + ").");
                }
                else
                {
                    Check(reached && maxStall < 0.3f && !sunk && maxPenetration < 0.08f,
                        "C: " + name + " traverses without sustained stalls, falling through or deep penetration (dx=" + F(progress) +
                        ", maxStall=" + F(maxStall) + ", penetration=" + F(maxPenetration) + ").");
                    Check(average >= test.runSpeed * 0.75f,
                        "C: " + name + " retains useful forward speed (avg=" + F(average) + ", configured=" + F(test.runSpeed) + ").");
                }
                if (name == "connected_flat_up20_down20_flat" || name == "dense_small_jitter_10" || growing)
                {
                    Texture2D image = Capture("C_" + name + ".png"); UnityEngine.Object.Destroy(image);
                }
            }
            finally
            {
                fixture.SetActive(false); UnityEngine.Object.Destroy(fixture);
                if (second) { second.SetActive(false); UnityEngine.Object.Destroy(second); }
                Physics2D.SyncTransforms();
            }
        }

        private static void SetState(BlackInkTestController.TrialState state)
        {
            var property = typeof(BlackInkTestController).GetProperty(nameof(BlackInkTestController.State));
            property.GetSetMethod(true).Invoke(test, new object[] { state });
        }

        private static List<Vector2> SlopePath(float degrees, int subdivisions)
        {
            float tangent = Mathf.Tan(degrees * Mathf.Deg2Rad);
            var path = new List<Vector2>();
            int count = Mathf.Max(2, subdivisions);
            // Keep the initial capsule away from both polygon end caps.
            for (int i = 0; i <= count; i++)
            {
                float x = Mathf.Lerp(-1f, 6f, i / (float)count);
                path.Add(new Vector2(x, 2f + tangent * x));
            }
            return path;
        }

        private static List<Vector2> TransitionPath(float degrees)
        {
            float rise = Mathf.Tan(degrees * Mathf.Deg2Rad) * 1.2f;
            return new List<Vector2> { new Vector2(-1f, 2f), new Vector2(0.8f, 2f), new Vector2(2f, 2f + rise),
                new Vector2(3.2f, 2f), new Vector2(6f, 2f) };
        }

        private static List<Vector2> JitterPath(float degrees)
        {
            var path = SlopePath(degrees, 280);
            for (int i = 1; i < path.Count - 1; i++)
                path[i] += Vector2.up * Mathf.Sin(i * 1.17f) * 0.0008f;
            return path;
        }

        private static List<Vector2> RaisedPath(float height) => new List<Vector2>
        { new Vector2(-1f, 2f), new Vector2(1.29f, 2f + height), new Vector2(6f, 2f + height) };

        private static float PathY(List<Vector2> path, float x)
        {
            for (int i = 1; i < path.Count; i++)
                if (x <= path[i].x) return Mathf.Lerp(path[i - 1].y, path[i].y,
                    Mathf.InverseLerp(path[i - 1].x, path[i].x, x));
            return path[path.Count - 1].y;
        }

        private static IEnumerator TitleStartEntry()
        {
            observations.Add("ENTRY method: strict-after only. Load the existing TitleScene, invoke its actual serialized Start button UnityEvent twice, and observe one transition to the existing BlackInkTestScene. No new scene or builder is involved.");
            var titleLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Assets/Scenes/TitleScene.unity",
                UnityEngine.SceneManagement.LoadSceneMode.Single);
            float limit = Time.realtimeSinceStartup + 20f;
            while (titleLoad != null && !titleLoad.isDone && Time.realtimeSinceStartup < limit) yield return null;
            yield return null;
            yield return null;
            Check(titleLoad != null && titleLoad.isDone &&
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "TitleScene", "ENTRY: existing TitleScene loads in actual Play Mode.");
            var title = UnityEngine.Object.FindFirstObjectByType<TitleUIManager>();
            var buttonField = typeof(TitleUIManager).GetField("startButton",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var transitionField = typeof(TitleUIManager).GetField("isTransitioning",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var button = title && buttonField != null ? buttonField.GetValue(title) as UnityEngine.UI.Button : null;
            Check(title && button && button.interactable, "ENTRY: existing TitleUIManager exposes its wired, interactable Start button.");
            if (!title || !button) yield break;
            int currentSceneLoads = 0;
            UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene, UnityEngine.SceneManagement.LoadSceneMode> loaded =
                (scene, mode) => { if (scene.path == ScenePath) currentSceneLoads++; };
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += loaded;
            try
            {
                button.onClick.Invoke();
                bool firstLocked = transitionField != null && (bool)transitionField.GetValue(title) && !button.interactable;
                button.onClick.Invoke();
                Check(firstLocked && transitionField != null && (bool)transitionField.GetValue(title),
                    "ENTRY: first actual Start event locks interactions; immediate second event is guarded.");
                limit = Time.realtimeSinceStartup + 20f;
                while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath &&
                    Time.realtimeSinceStartup < limit) yield return null;
                yield return null;
                yield return null;
                test = UnityEngine.Object.FindFirstObjectByType<BlackInkTestController>();
                Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == ScenePath && currentSceneLoads == 1,
                    "ENTRY: double Start event reaches the saved BlackInkTestScene exactly once (loads=" + currentSceneLoads + ").");
                Check(test && test.enabled && test.endlessWorld && test.endlessWorld.ActivePlatformCount > 0 &&
                    UnityEngine.Object.FindObjectsByType<BlackInkTestController>(FindObjectsSortMode.None).Length == 1,
                    "ENTRY: destination has one initialized current controller and a generated runtime world.");
                if (!test) yield break;
                test.manualPointerForValidation = true;
                test.SendMessage("OnApplicationFocus", true); Release();
                Check(Backgrounds().Length > 0 && Backgrounds().All(renderer => renderer.enabled),
                    "ENTRY: user's saved background is present after Title Start.");
                Check(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length == 1,
                    "ENTRY: destination has exactly one active EventSystem.");
                Check(test.State == BlackInkTestController.TrialState.Preparing && Near(test.preparationSeconds, 3f) &&
                    test.PreparationRemaining > 2f,
                    "ENTRY: normal Title Start begins the existing three-second preparation (remaining=" + F(test.PreparationRemaining) + ").");
                configuredPreparation = test.preparationSeconds;
                test.ResetTrial(); Release();
                IEnumerator reset = ObserveReset("Title-entry destination Reset");
                while (reset.MoveNext()) yield return reset.Current;
                test.ResetTrial(); Release();
                Texture2D image = Capture("Entry_TitleStart_CurrentScene.png"); UnityEngine.Object.Destroy(image);
            }
            finally { UnityEngine.SceneManagement.SceneManager.sceneLoaded -= loaded; }
        }

        private static IEnumerator FinalFrame()
        {
            if (!test) yield break;
            test.ResetTrial(); Release();
            yield return null;
            Texture2D image = Capture("After_AllChecks_Preparing.png"); UnityEngine.Object.Destroy(image);
            Check(Backgrounds().All(renderer => renderer.enabled), "A: temporary rendering probes restore all user background renderers.");
            Check(Near(Time.timeScale, 1f), "B: controlled validation keeps Time.timeScale at 1 during real preparation checks.");
        }

        private static void Release() => test.ProcessPointer(Vector2.zero, false, false, false);
        private static void Draw(Vector2[] points)
        {
            Release();
            test.ProcessPointer(test.drawingCamera.WorldToScreenPoint(points[0]), true, true, false);
            for (int point = 1; point < points.Length; point++)
                for (int i = 1; i <= 12; i++)
                    test.ProcessPointer(test.drawingCamera.WorldToScreenPoint(Vector2.Lerp(points[point - 1], points[point], i / 12f)), false, true, false);
            test.ProcessPointer(test.drawingCamera.WorldToScreenPoint(points[points.Length - 1]), false, false, true);
        }

        private static int ChangedPixels(Texture2D first, Texture2D second)
        {
            Color32[] a = first.GetPixels32(), b = second.GetPixels32();
            int count = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) >= 18) count++;
            return count;
        }

        // Real URP camera and existing HUD at a controlled review resolution.
        // Returned textures are owned by the caller. No screenshot mutates saved assets.
        private static Texture2D Capture(string filename)
        {
            Camera camera = test.drawingCamera;
            Canvas canvas = test.GetComponentInChildren<Canvas>();
            RenderMode oldMode = canvas ? canvas.renderMode : RenderMode.ScreenSpaceOverlay;
            Camera oldUiCamera = canvas ? canvas.worldCamera : null;
            float oldPlane = canvas ? canvas.planeDistance : 0f;
            float oldAspect = camera.aspect, oldSize = camera.orthographicSize;
            Vector3 oldPosition = camera.transform.position;
            RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
            Texture2D readback = null;
            try
            {
                camera.targetTexture = target;
                camera.aspect = 1920f / 1080f;
                camera.orthographicSize = 5.4f;
                camera.transform.position = new Vector3(test.characterBody.bounds.center.x + (0.5f - test.runnerViewportX) * 19.2f,
                    (0.5f - test.groundViewportY) * 10.8f, oldPosition.z);
                test.SendMessage("UpdateHud");
                if (canvas)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1f;
                }
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                readback = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                readback.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                readback.Apply();
                File.WriteAllBytes(Path.Combine(Output, filename), readback.EncodeToPNG());
                return readback;
            }
            catch
            {
                if (readback) UnityEngine.Object.Destroy(readback);
                throw;
            }
            finally
            {
                RenderTexture.active = oldActive;
                camera.targetTexture = oldTarget; camera.aspect = oldAspect; camera.orthographicSize = oldSize;
                camera.transform.position = oldPosition;
                if (canvas) { canvas.renderMode = oldMode; canvas.worldCamera = oldUiCamera; canvas.planeDistance = oldPlane; }
                Canvas.ForceUpdateCanvases();
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static void WriteReport(bool complete)
        {
            Directory.CreateDirectory(Output);
            string mode = SessionState.GetBool(IncludeSlopeLimits, false) ? "STRICT AFTER + SLOPE LIMIT DIAGNOSTIC" :
                SessionState.GetBool(SlopeLimits, false) ? "SLOPE LIMIT DIAGNOSTIC (collect-only)" :
                SessionState.GetBool(Baseline, false) ? "BASELINE PROBE" : "STRICT AFTER";
            string state = complete ? (failures.Count == 0 ? "PASS" : "FAIL") : "IN PROGRESS";
            var report = new StringBuilder();
            report.AppendLine("# Current saved scene A / B / C regression report");
            report.AppendLine("\nDate (Asia/Seoul): " + DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
                "\nUnity: " + Application.unityVersion + "\nMode: " + mode + "\nResult: " + state);
            report.AppendLine("\n기존 BlackInkTestScene을 직접 열어 실제 Play Mode에서 검증합니다. 씬 재생성, 배경 교체, 새 카운트다운 추가는 하지 않습니다.");
            report.AppendLine("\nA = runtime platform visibility and spawn support; B = existing preparation and reset timing; C = actual capsule/ink physics.");
            report.AppendLine("\n## Checks\n");
            foreach (string result in results) report.AppendLine("- " + result);
            report.AppendLine("\n## Quantitative observations\n");
            foreach (string observation in observations) report.AppendLine("- " + observation);
            report.AppendLine("\n## Collected failures\n");
            foreach (string failure in failures) report.AppendLine("- " + failure);
            report.AppendLine("\nBaseline mode retains every failed assertion and exits 0 after completing the probe. Strict mode exits 1 for any collected failure. " +
                "Endpoint controls are diagnostic. Airborne duration and horizontal/total speed are reported separately, so downhill ballistic motion is not mislabeled as a collider stall.");
            File.WriteAllText(Path.Combine(Output, "ValidationReport.md"), report.ToString());
            File.WriteAllText(Path.Combine(Output, "PhysicsMetrics.csv"),
                "fixture,seconds,progress,averageVx,minVx,maxVx,minSpeed,maxSpeed,groundedSeconds,maxAirSeconds,maxStallSeconds,maxPenetration,contacts,growthEdits,reached,sunk\n" +
                string.Join("\n", physicsRows));
        }

        private static void Complete(string failure)
        {
            if (finishing) return;
            finishing = true;
            current = null; stages.Clear();
            Application.logMessageReceived -= OnLog;
            if (!string.IsNullOrEmpty(failure)) failures.Add(failure);
            Time.timeScale = SessionState.GetFloat(OriginalScale, 1f);
            observations.Add("Time.timeScale restored to its pre-run value: " + F(Time.timeScale) + ".");
            WriteReport(true);
            bool baseline = SessionState.GetBool(Baseline, false);
            SessionState.SetBool(Pending, false);
            Debug.Log("CURRENT_SCENE_VALIDATION_" + (failures.Count == 0 ? "PASS" : "FAIL") +
                " checks=" + results.Count + ", failures=" + failures.Count + ", mode=" + (baseline ? "baseline" : "strict") + ", report=" + Output);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            if (Application.isBatchMode)
            {
                int code = !baseline && failures.Count > 0 ? 1 : 0;
                EditorApplication.delayCall += () => EditorApplication.Exit(code);
            }
        }
    }
}
