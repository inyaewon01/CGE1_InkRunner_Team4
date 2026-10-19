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
    /// <summary>Real editor/play/physics checks; no package or common build-settings changes.</summary>
    [InitializeOnLoad]
    public static class BlackInkValidation
    {
        private const string PendingKey = "InkRunner.BlackInkPrototype.ValidationPending";
        private static IEnumerator routine;
        private static readonly List<string> passed = new List<string>();
        private static readonly List<string> errors = new List<string>();
        private static readonly List<string> environmentNotes = new List<string>();
        private static double deadline;
        private static bool batch;
        private static string Output => Path.GetFullPath("Logs/BlackInkValidationResults");

        static BlackInkValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        // CLI: Unity -batchmode -projectPath <isolated copy> -executeMethod
        // InkRunner.BlackInkPrototype.Editor.BlackInkValidation.BuildAndValidateBatch -logFile <path>
        public static void BuildAndValidateBatch()
        {
            batch = true;
            // A fresh batch project starts with an untitled scene; Unity forbids an additive
            // scene beside it. Open the saved source scene in this disposable copy first.
            EditorSceneManager.OpenScene("Assets/Scenes/PlayScene.unity");
            BlackInkSceneBuilder.BuildScene();
            StartValidation();
        }

        [MenuItem("Tools/InkRunner/Validate Black Ink Prototype (Play Mode)")]
        public static void StartValidation()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before starting validation.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Output);
            SessionState.SetBool(PendingKey, true);
            passed.Clear();
            errors.Clear();
            environmentNotes.Clear();
            Application.logMessageReceived += OnLog;
            EditorSceneManager.OpenScene(BlackInkSceneBuilder.ScenePath);
            deadline = EditorApplication.timeSinceStartup + 150;
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
            if (EditorApplication.timeSinceStartup > deadline && deadline > 0)
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
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                if (stack.Contains("UnityEditor.Search."))
                {
                    environmentNotes.Add("Unity Editor search indexing: " + condition);
                    return;
                }
                errors.Add(condition + "\n" + stack);
            }
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("FAILED: " + description);
            passed.Add(description);
            Debug.Log("BLACK_INK_PASS: " + description);
        }

        private static bool Near(float a, float b, float epsilon = 0.003f) => Mathf.Abs(a - b) <= epsilon;

        private static IEnumerator RunChecks()
        {
            yield return null;
            yield return null;
            BlackInkTestController test = UnityEngine.Object.FindFirstObjectByType<BlackInkTestController>();
            Check(test != null && test.enabled, "Test scene starts with all required references");
            test.manualPointerForValidation = true;
            // Hidden batch editors do not own OS focus; tests drive the focus seam explicitly.
            test.SendMessage("OnApplicationFocus", true);
            Time.timeScale = 3f;
            test.preparationSeconds = 999f;
            test.ResetTrial();
            Release(test);
            Vector2 spawnPosition = test.character.position;

            Check(Near(test.Thickness, 1.575f) && Near(test.MinimumPointSpacing, 0.0525f), "Measured H=2.10, thickness=0.75H, spacing=0.025H");
            Check(Near(InkPathMath.AffordableLength(100f, 10f), 10f), "100 ink at 10/unit affords exactly 10 world units");
            Check(Near(InkPathMath.ClipToBudget(Vector2.zero, new Vector2(3f, 0f), 7f, 10f).x, 0.7f), "Budget clips the final segment to remaining affordable length");
            Check(InkPathMath.BodyIntersectsBounds(new Vector2(-2f, 0f), new Vector2(2f, 0f), 1.5f,
                new Bounds(new Vector3(0f, -0.8f), new Vector3(0.2f, 0.2f, 1f))), "Collision exclusion checks segment interior and full ink thickness");
            Check(!InkPathMath.GentleSegment(Vector2.zero, Vector2.up, test.maximumSlope), "Vertical walls are refused");

            Vector2 pointer = Screen(test, new Vector2(0f, 0f));
            test.ProcessPointer(pointer, true, true, false);
            for (int i = 0; i < 20; i++) test.ProcessPointer(pointer, false, true, false);
            Check(Near(test.CurrentInk, 100f) && test.StrokeCount == 0, "Stationary mouse / simple click generates no platform and consumes no ink");
            Release(test);

            Draw(test, new[] { new Vector2(-0.3f, 0f), new Vector2(3.3f, 0f) }, 1);
            float fastCost = 100f - test.CurrentInk;
            Check(Near(fastCost, 36f) && test.StrokeCount == 1,
                $"Fast sweep creates a continuous 3.6-unit bridge for 36 ink (actual cost={fastCost:0.000}, strokes={test.StrokeCount}, screen={UnityEngine.Screen.width}x{UnityEngine.Screen.height}, camera={test.drawingCamera.pixelRect})");
            var geometry = test.inkRoot.GetComponentInChildren<InkStrokeGeometry>();
            Check(geometry.GetComponent<PolygonCollider2D>().enabled &&
                geometry.GetComponent<MeshFilter>().sharedMesh.vertexCount > geometry.PointCount * 2,
                "Live generated stroke has an opaque-body mesh and an enabled continuous collider");
            Check(Near(geometry.GetComponent<PolygonCollider2D>().bounds.max.y, 0f) &&
                Near(geometry.GetComponent<PolygonCollider2D>().bounds.size.y, test.Thickness),
                "Mouse path is the collider top; full thickness extends downwards");
            test.ResetTrial(); Release(test);
            Draw(test, new[] { new Vector2(-0.3f, 0f), new Vector2(3.3f, 0f) }, 70);
            Check(Near(100f - test.CurrentInk, fastCost), "Fast and slow event sampling charges the same completed world path length");

            int count = test.StrokeCount;
            Draw(test, new[] { new Vector2(4f, 0f), new Vector2(5f, 0f) }, 8);
            Check(test.StrokeCount == count + 1, "New strokes preserve old platforms by default and do not auto-connect");
            test.clearPreviousOnNewStroke = true;
            pointer = Screen(test, new Vector2(6f, 0f));
            test.ProcessPointer(pointer, true, true, false); Release(test);
            Check(test.StrokeCount == count + 1, "Replacement option does not erase old roads on a simple click");
            Draw(test, new[] { new Vector2(6f, 0f), new Vector2(7f, 0f) }, 10);
            Check(test.StrokeCount == 1, "Replacement erases visuals and collision only on first successful segment");
            test.clearPreviousOnNewStroke = false;

            test.maximumInk = 7f;
            test.ResetTrial(); Release(test);
            Draw(test, new[] { Vector2.zero, new Vector2(3f, 0f) }, 1);
            Check(Near(test.CurrentInk, 0f) && Near(test.CreatedLength, 0.7f), "Ink exhaustion creates only the affordable last section and never goes negative");
            Check(test.StrokeCount == 1 && test.State != BlackInkTestController.TrialState.Failed, "Zero ink keeps existing collision and does not fail the trial");
            Vector2 savedPosition = test.character.position;
            test.Refill();
            Check(Near(test.CurrentInk, 7f) && test.StrokeCount == 1 && test.character.position == savedPosition, "Refill changes only ink and ends the held stroke");
            test.maximumInk = 100f;
            test.ResetTrial(); Release(test);
            yield return null;
            yield return null;
            Check(test.StrokeCount == 0 && test.inkRoot.childCount == 0 && Near(test.CurrentInk, 100f) &&
                test.State == BlackInkTestController.TrialState.Preparing, "Reset clears all generated objects/colliders and restores the preparation state");

            Canvas.ForceUpdateCanvases();
            var panel = test.GetComponentsInChildren<RectTransform>().First(t => t.name == "Ink and test measurements");
            Vector2 panelScreen = RectTransformUtility.WorldToScreenPoint(null, panel.TransformPoint(panel.rect.center));
            test.ProcessPointer(panelScreen, true, true, false);
            test.ProcessPointer(Screen(test, new Vector2(3f, 0f)), false, true, false);
            Release(test);
            Check(test.StrokeCount == 0 && Near(test.CurrentInk, 100f), "Click begun on UI remains blocked until mouse release");

            var forbidden = new GameObject("Validation forbidden area", typeof(BoxCollider2D));
            forbidden.transform.position = new Vector3(1.5f, -1.35f, 0f);
            forbidden.GetComponent<BoxCollider2D>().size = new Vector2(0.35f, 0.25f);
            test.noDrawZones = new[] { forbidden.GetComponent<Collider2D>() };
            Physics2D.SyncTransforms();
            Draw(test, new[] { Vector2.zero, new Vector2(3f, 0f) }, 1);
            bool noOverlap = true;
            foreach (InkStrokeGeometry stroke in test.inkRoot.GetComponentsInChildren<InkStrokeGeometry>())
                for (int i = 1; i < stroke.PointCount; i++)
                    noOverlap &= !InkPathMath.BodyIntersectsBounds(stroke.Points[i - 1], stroke.Points[i], test.Thickness, test.noDrawZones[0].bounds);
            Check(noOverlap && test.CreatedLength < 2.8f, "Fast sweep cuts around a forbidden area touched by the thick body, with no bridging across it");
            test.noDrawZones = new Collider2D[0];
            UnityEngine.Object.Destroy(forbidden);

            test.ResetTrial(); Release(test);
            test.maximumPoints = 8;
            Draw(test, new[] { Vector2.zero, new Vector2(3f, 0f) }, 1);
            Check(test.PointCount <= 8 && test.StrokeCount > 0, "Storage cap safely stops generation while retaining existing roads");
            test.maximumPoints = 2048;
            test.ResetTrial(); Release(test);
            Draw(test, new[] { new Vector2(3.3f, 0f), new Vector2(-0.3f, 0f) }, 30);
            Check(Near(test.CreatedLength, 3.6f), "Reverse-direction strokes generate the same continuous surface");
            test.SendMessage("OnApplicationFocus", false);
            test.SendMessage("OnApplicationFocus", true);
            float lengthBeforeFocus = test.CreatedLength;
            test.ProcessPointer(Screen(test, new Vector2(6f, 0f)), false, true, false);
            Check(Near(test.CreatedLength, lengthBeforeFocus), "Focus loss terminates input and prevents a long reconnecting segment");
            Release(test);

            test.ResetTrial(); Release(test);
            Draw(test, new[] { new Vector2(-0.3f, 0f), new Vector2(0.7f, 0.25f),
                new Vector2(2.3f, 0.25f), new Vector2(3.3f, 0f) }, 12);
            test.enabled = false;
            BlackInkHud hud = test.GetComponent<BlackInkHud>();
            foreach (int ink in new[] { 100, 50, 0 })
            {
                hud.Present(ink, 100f, test.characterBodyHeight, test.Thickness, 10f, false, test.PointCount,
                    test.StrokeCount, "GAUGE CHECK / " + ink + "%", "Frame and brush remain fixed; only the black fill is clipped.");
                Canvas.ForceUpdateCanvases();
                string screenshot = Path.Combine(Output, "BlackInk_" + ink + ".png");
                CaptureFrame(test, screenshot);
                yield return null;
                Check(File.Exists(screenshot), "Captured real Unity render for gauge " + ink + "%");
            }
            test.enabled = true;

            // The actual Rigidbody/PolygonCollider simulation must carry the original character across.
            test.preparationSeconds = 0.1f;
            test.ResetTrial(); Release(test);
            Draw(test, new[] { new Vector2(-0.3f, 0f), new Vector2(0.7f, 0.25f),
                new Vector2(2.3f, 0.25f), new Vector2(3.3f, 0f) }, 12);
            float endTime = Time.realtimeSinceStartup + 15f;
            while (test.State != BlackInkTestController.TrialState.Complete && test.State != BlackInkTestController.TrialState.Failed &&
                   Time.realtimeSinceStartup < endTime) yield return null;
            Check(test.State == BlackInkTestController.TrialState.Complete, "Actual Physics2D runner crosses terrain seams and gentle up/down slopes on the ink bridge");
            CaptureFrame(test, Path.Combine(Output, "BlackInk_Crossed.png"));
            yield return null;

            test.ResetTrial(); Release(test);
            Check(Near(test.character.position.x, spawnPosition.x) &&
                Near(test.characterBody.bounds.center.x, spawnPosition.x + test.characterBody.offset.x),
                "Reset after a completed crossing restores both the physics pose and rendered collider immediately");
            test.ProcessPointer(Screen(test, new Vector2(-0.3f, 0f)), true, true, false);
            test.ProcessPointer(Screen(test, new Vector2(1.3f, 0f)), false, true, false);
            endTime = Time.realtimeSinceStartup + 12f;
            while ((test.State == BlackInkTestController.TrialState.Preparing || test.characterBody.bounds.center.x < 0.6f) &&
                   test.State != BlackInkTestController.TrialState.Failed &&
                   Time.realtimeSinceStartup < endTime) yield return null;
            geometry = test.inkRoot.GetComponentInChildren<InkStrokeGeometry>();
            Check(test.State == BlackInkTestController.TrialState.Running && geometry != null &&
                test.characterBody.IsTouching(geometry.GetComponent<PolygonCollider2D>()),
                $"Runner stands on a valid section while the same mouse stroke is still held open (state={test.State}, body={test.characterBody.bounds.center}, length={test.CreatedLength:0.000})");
            test.ProcessPointer(Screen(test, new Vector2(3.3f, 0f)), false, true, false);
            test.ProcessPointer(Screen(test, new Vector2(3.3f, 0f)), false, false, true);
            endTime = Time.realtimeSinceStartup + 12f;
            while (test.State == BlackInkTestController.TrialState.Running && Time.realtimeSinceStartup < endTime) yield return null;
            Check(test.State == BlackInkTestController.TrialState.Complete, "Extending a live stroke while running carries the character to the arrival platform");

            test.ResetTrial(); Release(test);
            endTime = Time.realtimeSinceStartup + 15f;
            while (test.State != BlackInkTestController.TrialState.Failed && Time.realtimeSinceStartup < endTime) yield return null;
            Check(test.State == BlackInkTestController.TrialState.Failed, "Actual gravity causes an unbridged gap to fail; Reset can restart");
            test.preparationSeconds = 3f;
            test.ResetTrial();
            Time.timeScale = 1f;
            Check(errors.Count == 0, "No runtime errors, missing references, shader errors or exceptions during validation");
        }

        private static Vector2 Screen(BlackInkTestController test, Vector2 world) => test.drawingCamera.WorldToScreenPoint(world);
        // Clear the fixture button state without inventing a pointer sweep to screen (0,0).
        // Draw() separately exercises genuine releases at their actual cursor position.
        private static void Release(BlackInkTestController test) => test.ProcessPointer(Vector2.zero, false, false, false);

        private static void Draw(BlackInkTestController test, Vector2[] points, int subdivisions)
        {
            test.ProcessPointer(Screen(test, points[0]), true, true, false);
            for (int i = 1; i < points.Length; i++)
                for (int s = 1; s <= subdivisions; s++)
                    test.ProcessPointer(Screen(test, Vector2.Lerp(points[i - 1], points[i], s / (float)subdivisions)), false, true, false);
            test.ProcessPointer(Screen(test, points[points.Length - 1]), false, false, true);
        }

        private static void CaptureFrame(BlackInkTestController test, string path)
        {
            // ScreenCapture has no game backbuffer in a batch Editor. Render the actual
            // camera + uGUI through URP instead; no simulated/recreated picture is used.
            Camera camera = test.drawingCamera;
            Canvas canvas = test.GetComponentInChildren<Canvas>();
            RenderMode oldMode = canvas.renderMode;
            Camera oldUiCamera = canvas.worldCamera;
            float oldPlaneDistance = canvas.planeDistance;
            float oldAspect = camera.aspect;
            RenderTexture oldTarget = camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
            Texture2D readback = null;
            try
            {
                camera.targetTexture = target;
                camera.aspect = 1920f / 1080f;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
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
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldUiCamera;
                canvas.planeDistance = oldPlaneDistance;
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
            if (!string.IsNullOrEmpty(failure)) errors.Add(failure);
            if (!string.IsNullOrEmpty(failure)) Debug.Log("BLACK_INK_VALIDATION_FAILURE_DETAILS: " + failure);
            Directory.CreateDirectory(Output);
            string report = "# Black Ink Prototype — Unity validation\n\nUnity: " + Application.unityVersion +
                "\nResult: " + (errors.Count == 0 ? "PASS" : "FAIL") + "\n\n" +
                string.Join("\n", passed.Select(s => "- PASS: " + s)) + "\n\n" +
                string.Join("\n\n", errors.Select(s => "ERROR: " + s)) +
                (environmentNotes.Count > 0 ? "\n\nEditor environment observations (outside prototype runtime):\n" + string.Join("\n", environmentNotes.Distinct()) : "");
            File.WriteAllText(Path.Combine(Output, "ValidationReport.md"), report);
            Debug.Log("BLACK_INK_VALIDATION_" + (errors.Count == 0 ? "PASS" : "FAIL") + " (" + passed.Count + " checks). " + Output);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            if (batch || Application.isBatchMode)
            {
                int exitCode = errors.Count == 0 ? 0 : 1;
                EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
            }
        }
    }
}
