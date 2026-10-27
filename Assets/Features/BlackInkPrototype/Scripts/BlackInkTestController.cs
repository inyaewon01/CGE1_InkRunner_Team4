using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InkRunner.BlackInkPrototype
{
    /// <summary>Black ink drawing plus a camera-following endless preview of the original Inkwarrior.</summary>
    public sealed class BlackInkTestController : MonoBehaviour
    {
        [Header("Scene references (test scene only)")]
        public Rigidbody2D character;
        public CapsuleCollider2D characterBody;
        public Animator characterAnimator;
        public Camera drawingCamera;
        public Transform inkRoot;
        public Collider2D arrivalPlatform;
        public Transform topSurfaceGuide;
        public Collider2D[] noDrawZones = new Collider2D[0];
        public Material inkMaterial;
        public PhysicsMaterial2D surfaceMaterial;
        public Texture2D gaugeSource;

        [Header("Endless runner / actual Game-view pixels")]
        public bool endlessMode = true;
        public EndlessInkWorld endlessWorld;
        public bool lockToScreenPixels = true;
        [Min(1f)] public float pixelsPerWorldUnit = 100f;
        [Min(1f)] public float referencePixelHeight = 1080f;
        [Min(1f)] public float characterHeightPixels = 40f;
        [Min(1f)] public float inkThicknessPixels = 25f;
        [Min(0.1f)] public float originalCharacterBodyHeight = 2.10f;
        [Range(0.03f, 0.4f)] public float runnerViewportX = 0.08f;
        [Range(0.15f, 0.55f)] public float groundViewportY = 0.27f;

        [Header("Ink / measured reference pose")]
        [Min(0.01f)] public float maximumInk = 100f;
        [Min(0.01f)] public float costPerUnit = 10f;
        [Tooltip("run_01: head to boot sole, approximately 210 pixels / 100 PPU; excludes sword and cape.")]
        [Min(0.1f)] public float characterBodyHeight = 2.10f;
        [Range(0.1f, 1.5f)] public float thicknessRatio = 0.75f;
        [Min(0.001f)] public float minimumPointSpacingRatio = 0.025f;
        public bool clearPreviousOnNewStroke;

        [Header("Trial")]
        [Min(0f)] public float preparationSeconds = 3f;
        [Min(0.1f)] public float runSpeed = 1.8f;
        [Tooltip("Legacy two-platform trial only. In endless mode use EndlessInkWorld.Platform Gap.")]
        [Min(0f)] public float platformGap = 3f;
        [Range(5f, 45f)] public float maximumSlope = 35f;
        public float failureY = -5f;
        public Rect drawingBounds = new Rect(-12f, -3f, 28f, 7.5f);

        [Header("Brush appearance / bounded storage")]
        [Min(0.05f)] public float textureScale = 0.7f;
        [Range(0f, 1f)] public float edgeRoughness = 0.5f;
        [Min(1f)] public float offscreenCleanupDistance = 8f;
        [Min(2)] public int maximumPoints = 2048;
        [Min(1)] public int maximumSegments = 1984;
        [Min(1)] public int maximumStrokes = 64;

        public enum TrialState { Preparing, Running, Failed, Complete }
        public TrialState State { get; private set; }
        public float CurrentInk { get; private set; }
        public float Thickness => characterBodyHeight * thicknessRatio;
        public float MinimumPointSpacing => characterBodyHeight * minimumPointSpacingRatio;
        public int StrokeCount => strokes.Count;
        public int PointCount { get; private set; }
        public int SegmentCount { get; private set; }
        public float CreatedLength { get; private set; }
        public float PreparationRemaining => preparationRemaining;
        public float DistanceTravelled => Mathf.Max(0f, character.position.x - startPosition.x);
        public float ProjectedBodyHeightPixels => characterBodyHeight * drawingCamera.pixelHeight / (2f * drawingCamera.orthographicSize);
        public float ProjectedInkThicknessPixels => Thickness * drawingCamera.pixelHeight / (2f * drawingCamera.orthographicSize);
        [System.NonSerialized] public bool manualPointerForValidation;

        private readonly List<InkStrokeGeometry> strokes = new List<InkStrokeGeometry>();
        private readonly List<Vector2> activePoints = new List<Vector2>();
        private InkStrokeGeometry activeStroke;
        private BlackInkHud hud;
        private Vector2 startPosition;
        private Vector2 anchor;
        private Vector2 previousScreen;
        private bool held, suppressUntilRelease, hasAnchor, focused = true;
        private int strokeDirection;
        private float preparationRemaining;
        private string feedback = "Draw across the gap before the runner arrives.";

        private void Awake()
        {
            if (!character || !characterBody || !drawingCamera || !inkRoot || !inkMaterial)
            {
                Debug.LogError("BlackInkTestScene has missing references. Use Tools > InkRunner > Rebuild Black Ink Test Scene.", this);
                enabled = false;
                return;
            }
            if (endlessMode && !endlessWorld)
            {
                Debug.LogError("Endless world is missing. Rebuild Black Ink Test Scene from Tools > InkRunner.", this);
                enabled = false;
                return;
            }
            if (endlessMode)
            {
                characterBodyHeight = characterHeightPixels / pixelsPerWorldUnit;
                thicknessRatio = inkThicknessPixels / characterHeightPixels;
                character.transform.localScale = Vector3.one * (characterBodyHeight / originalCharacterBodyHeight);
                Physics2D.SyncTransforms();
                UpdateRunnerCamera();
                endlessWorld.Initialize(drawingCamera, character, surfaceMaterial);
            }
            startPosition = character.position;
            if (!endlessMode && arrivalPlatform)
            {
                Vector3 position = arrivalPlatform.transform.position;
                position.x += platformGap - arrivalPlatform.bounds.min.x;
                arrivalPlatform.transform.position = position;
            }
            if (topSurfaceGuide) topSurfaceGuide.localScale = new Vector3(platformGap / 3f, 1f, 1f);
            hud = GetComponent<BlackInkHud>();
            if (!hud) hud = gameObject.AddComponent<BlackInkHud>();
            hud.Build(gaugeSource, ResetTrial, Refill);
            ResetTrial();
        }

        private void Update()
        {
            // Losing pointer focus ends painting, but must not pause the countdown or run.
            if (State == TrialState.Preparing)
            {
                preparationRemaining = Mathf.Max(0f, preparationRemaining - Time.deltaTime);
                if (preparationRemaining <= 0f)
                {
                    State = TrialState.Running;
                    if (characterAnimator) characterAnimator.speed = 1f;
                }
            }
            if (character.position.y < failureY && State != TrialState.Failed) Finish(TrialState.Failed);
            if (!endlessMode && State == TrialState.Running && arrivalPlatform &&
                characterBody.bounds.center.x > arrivalPlatform.bounds.min.x + 0.65f &&
                characterBody.IsTouching(arrivalPlatform)) Finish(TrialState.Complete);

            Mouse mouse = Mouse.current;
            if (mouse != null && !manualPointerForValidation)
                ProcessPointer(mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame,
                    mouse.leftButton.isPressed, mouse.leftButton.wasReleasedThisFrame);
            CleanupBehindCamera();
            UpdateHud();
        }

        private void FixedUpdate()
        {
            if (!character) return;
            Vector2 velocity = character.linearVelocity;
            velocity.x = State == TrialState.Running ? runSpeed : 0f;
            character.linearVelocity = velocity;
        }

        private void LateUpdate()
        {
            if (!endlessMode || !drawingCamera || !characterBody) return;
            UpdateRunnerCamera();
            if (endlessWorld) endlessWorld.Tick();
        }

        private void UpdateRunnerCamera()
        {
            float targetPixelHeight = lockToScreenPixels ? Mathf.Max(1, Screen.height) : referencePixelHeight;
            drawingCamera.orthographicSize = targetPixelHeight / (2f * pixelsPerWorldUnit);
            float viewWidth = 2f * drawingCamera.orthographicSize * drawingCamera.aspect;
            Vector3 position = drawingCamera.transform.position;
            position.x = characterBody.bounds.center.x + (0.5f - runnerViewportX) * viewWidth;
            position.y = (0.5f - groundViewportY) * 2f * drawingCamera.orthographicSize;
            drawingCamera.transform.position = position;
        }

        private void UpdateHud()
        {
            string state = State == TrialState.Preparing ? $"READY  {preparationRemaining:0.0}s  / draw now" :
                State == TrialState.Running ? (endlessMode ? "RUNNING  / endless route" : "RUNNING  / bridge the gap") :
                State == TrialState.Failed ? "FALLEN  / press Reset to retry" : "CROSSED  / test complete";
            if (hud) hud.Present(CurrentInk, maximumInk, characterBodyHeight, Thickness, costPerUnit,
                clearPreviousOnNewStroke, PointCount, StrokeCount, state, feedback);
            if (hud && endlessMode)
                hud.PresentRunner(DistanceTravelled, ProjectedBodyHeightPixels, ProjectedInkThicknessPixels,
                    endlessWorld.ActivePlatformCount, endlessWorld.ActiveObstacleCount);
        }

        // Public input seam also used by deterministic editor validation; no legacy Input API.
        public void ProcessPointer(Vector2 screen, bool pressed, bool down, bool released)
        {
            if (released || !down)
            {
                if (released && held && !suppressUntilRelease && focused && InWindow(screen))
                    PaintSweep(screen, true);
                EndStroke();
                held = false;
                suppressUntilRelease = false;
                return;
            }
            if (!focused || State == TrialState.Failed || State == TrialState.Complete) return;
            if (pressed)
            {
                EndStroke();
                held = true;
                previousScreen = screen;
                suppressUntilRelease = suppressUntilRelease || IsOverUI(screen) || !InWindow(screen) || CurrentInk <= 0f;
                if (!suppressUntilRelease) SetAnchorFromScreen(screen);
                return;
            }
            if (!held || suppressUntilRelease) return;
            if (!InWindow(screen))
            {
                EndStroke();
                suppressUntilRelease = true;
                return;
            }
            PaintSweep(screen, false);
        }

        private void PaintSweep(Vector2 screen, bool flushEndpoint)
        {
            Vector2 screenDelta = screen - previousScreen;
            if (screenDelta.sqrMagnitude < 0.01f)
            {
                // Following the runner must never paint a bridge by camera motion alone.
                // After a held stationary frame, resume at a new world anchor on real movement.
                if (endlessMode && State == TrialState.Running)
                {
                    EndStroke();
                    return;
                }
                if (!flushEndpoint) return;
            }

            // Sample the entire pointer sweep so a fast event cannot jump over UI or a no-draw zone.
            int steps = Mathf.Max(1, Mathf.CeilToInt(screenDelta.magnitude / 6f));
            Vector2 oldScreen = previousScreen;
            for (int i = 1; i <= steps && !suppressUntilRelease; i++)
            {
                Vector2 sample = Vector2.Lerp(oldScreen, screen, i / (float)steps);
                if (IsOverUI(sample)) { EndStroke(); continue; }
                Vector2 point = ScreenToInk(sample);
                if (!PointAllowed(point)) { EndStroke(); continue; }
                if (!hasAnchor) { anchor = point; hasAnchor = true; continue; }
                AppendTo(point);
            }
            if (flushEndpoint && hasAnchor && !suppressUntilRelease && !IsOverUI(screen))
                AppendTo(ScreenToInk(screen), true);
            previousScreen = screen;
        }

        private void SetAnchorFromScreen(Vector2 screen)
        {
            Vector2 point = ScreenToInk(screen);
            if (PointAllowed(point)) { anchor = point; hasAnchor = true; }
        }

        private Vector2 ScreenToInk(Vector2 screen)
        {
            Ray ray = drawingCamera.ScreenPointToRay(screen);
            Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, inkRoot.position.z));
            plane.Raycast(ray, out float distance);
            return inkRoot.InverseTransformPoint(ray.GetPoint(distance));
        }

        private bool IsOverUI(Vector2 screen) => hud && hud.IsScreenPointOverUI(screen);
        private static bool InWindow(Vector2 screen) => screen.x >= 0f && screen.y >= 0f && screen.x < Screen.width && screen.y < Screen.height;

        private bool PointAllowed(Vector2 local)
        {
            if (!InsideDrawingArea(local)) return false;
            Vector2 world = inkRoot.TransformPoint(local);
            Bounds body = characterBody.bounds;
            body.Expand(0.035f);
            if (body.Contains(new Vector3(world.x, world.y, body.center.z))) return false;
            foreach (Collider2D zone in noDrawZones)
                if (zone && zone.enabled && zone.OverlapPoint(world)) return false;
            return true;
        }

        private bool InsideDrawingArea(Vector2 local)
        {
            if (!endlessMode) return drawingBounds.Contains(local);
            Vector3 viewport = drawingCamera.WorldToViewportPoint(inkRoot.TransformPoint(local));
            return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
        }

        public bool AppendTo(Vector2 requestedEnd, bool forceShortSegment = false)
        {
            if (!hasAnchor || CurrentInk <= 0f) return false;
            float requestedLength = Vector2.Distance(anchor, requestedEnd);
            if (requestedLength < (forceShortSegment ? 0.00001f : MinimumPointSpacing)) return false;
            Vector2 end = InkPathMath.ClipToBudget(anchor, requestedEnd, CurrentInk, costPerUnit);
            float length = Vector2.Distance(anchor, end);
            if (length < 0.00001f) return false;
            int direction = end.x >= anchor.x ? 1 : -1;
            if (!InkPathMath.GentleSegment(anchor, end, maximumSlope) ||
                (strokeDirection != 0 && strokeDirection != direction))
            {
                EndStroke();
                anchor = requestedEnd;
                hasAnchor = PointAllowed(requestedEnd);
                feedback = $"Use a left-to-right or right-to-left path with slopes under {maximumSlope:0} degrees.";
                return false;
            }
            if (!SegmentAllowed(anchor, end))
            {
                EndStroke();
                feedback = "Blocked: keep the entire thick ink body clear of the runner / forbidden area.";
                return false;
            }

            bool first = !activeStroke;
            int retainedPoints = first && clearPreviousOnNewStroke ? 0 : PointCount;
            int retainedSegments = first && clearPreviousOnNewStroke ? 0 : SegmentCount;
            int retainedStrokes = first && clearPreviousOnNewStroke ? 0 : strokes.Count;
            if (retainedPoints + (first ? 2 : 1) > maximumPoints || retainedSegments + 1 > maximumSegments ||
                (first && retainedStrokes + 1 > maximumStrokes))
            {
                EndStroke();
                suppressUntilRelease = true;
                feedback = "Storage limit reached. Existing platforms are kept. Reset to clear.";
                return false;
            }
            if (first)
            {
                if (clearPreviousOnNewStroke) ClearStrokes();
                GameObject strokeObject = new GameObject($"Black ink stroke {strokes.Count + 1:00}");
                strokeObject.transform.SetParent(inkRoot, false);
                activeStroke = strokeObject.AddComponent<InkStrokeGeometry>();
                activeStroke.Initialize(inkMaterial, Thickness, textureScale, edgeRoughness, surfaceMaterial);
                strokes.Add(activeStroke);
                activePoints.Clear();
                activePoints.Add(anchor);
                PointCount++;
                strokeDirection = direction;
            }
            activePoints.Add(end);
            activeStroke.SetPoints(activePoints);
            PointCount++;
            SegmentCount++;
            CreatedLength += length;
            CurrentInk = Mathf.Max(0f, CurrentInk - length * costPerUnit);
            anchor = end;
            Physics2D.SyncTransforms();
            feedback = $"Created {CreatedLength:0.00} world units. Left drag draws black ink.";
            if (CurrentInk < 0.0001f)
            {
                CurrentInk = 0f;
                EndStroke();
                suppressUntilRelease = true;
                feedback = "Ink empty. Existing paths stay. Refill, then start a new click.";
            }
            return true;
        }

        private bool SegmentAllowed(Vector2 localStart, Vector2 localEnd)
        {
            if (!InsideDrawingArea(localEnd)) return false;
            Vector2 a = inkRoot.TransformPoint(localStart), b = inkRoot.TransformPoint(localEnd);
            Bounds body = characterBody.bounds;
            body.Expand(0.035f);
            if (InkPathMath.BodyIntersectsBounds(a, b, Thickness, body)) return false;
            foreach (Collider2D zone in noDrawZones)
                if (zone && zone.enabled && InkPathMath.BodyIntersectsBounds(a, b, Thickness, zone.bounds)) return false;
            return true;
        }

        public void EndStroke()
        {
            activeStroke = null;
            activePoints.Clear();
            hasAnchor = false;
            strokeDirection = 0;
        }

        public void ResetTrial()
        {
            EndStroke();
            ClearStrokes();
            // Reset both transform and physics pose. SyncTransforms can otherwise publish an
            // old interpolated render pose back into a body teleported through position alone.
            RigidbodyInterpolation2D interpolation = character.interpolation;
            character.interpolation = RigidbodyInterpolation2D.None;
            character.simulated = false;
            character.transform.SetPositionAndRotation(
                new Vector3(startPosition.x, startPosition.y, character.transform.position.z), Quaternion.identity);
            character.position = startPosition;
            character.rotation = 0f;
            character.linearVelocity = Vector2.zero;
            character.angularVelocity = 0f;
            character.simulated = true;
            Physics2D.SyncTransforms();
            character.interpolation = interpolation;
            character.WakeUp();
            if (endlessMode)
            {
                UpdateRunnerCamera();
                Vector2 foot = new Vector2(characterBody.bounds.center.x, 0f);
                endlessWorld.ResetWorld(foot);
            }
            CurrentInk = maximumInk;
            CreatedLength = 0f;
            preparationRemaining = preparationSeconds;
            State = TrialState.Preparing;
            held = false;
            suppressUntilRelease = Mouse.current != null && Mouse.current.leftButton.isPressed;
            if (characterAnimator)
            {
                characterAnimator.Play("InkWarrior_Run", 0, 0f);
                characterAnimator.Update(0f);
                characterAnimator.speed = 0f;
            }
            feedback = endlessMode ? "Continuous terrain / decorative obstacles. Left drag draws 25 px ink; Refill adds ink." :
                "Draw a bridge along y = 0. The mouse path is the platform TOP.";
            Physics2D.SyncTransforms();
            UpdateHud();
        }

        public void Refill()
        {
            CurrentInk = maximumInk;
            EndStroke();
            suppressUntilRelease = true;
            feedback = "Ink refilled. Release, then start a new left drag.";
            UpdateHud();
        }

        private void ClearStrokes()
        {
            foreach (InkStrokeGeometry stroke in strokes)
            {
                if (!stroke) continue;
                // Disable immediately; delayed Destroy must not leave a live collider for this frame.
                stroke.gameObject.SetActive(false);
                Destroy(stroke.gameObject);
            }
            strokes.Clear();
            PointCount = SegmentCount = 0;
        }

        private void CleanupBehindCamera()
        {
            float left = drawingCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, -drawingCamera.transform.position.z)).x;
            for (int i = strokes.Count - 1; i >= 0; i--)
            {
                InkStrokeGeometry stroke = strokes[i];
                float right = inkRoot.TransformPoint(new Vector3(stroke.RightmostX, 0f)).x;
                if (stroke == activeStroke || right >= left - offscreenCleanupDistance ||
                    right >= characterBody.bounds.min.x - offscreenCleanupDistance) continue;
                PointCount -= stroke.PointCount;
                SegmentCount -= Mathf.Max(0, stroke.PointCount - 1);
                stroke.gameObject.SetActive(false);
                Destroy(stroke.gameObject);
                strokes.RemoveAt(i);
            }
        }

        private void Finish(TrialState result)
        {
            State = result;
            EndStroke();
            character.linearVelocity = Vector2.zero;
            if (result == TrialState.Failed) character.simulated = false;
            if (characterAnimator) characterAnimator.speed = 0f;
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            EndStroke();
            held = false;
            suppressUntilRelease = true;
        }

        private void OnDisable() { EndStroke(); held = false; }

        private void OnValidate()
        {
            maximumInk = Mathf.Max(0.01f, maximumInk);
            costPerUnit = Mathf.Max(0.01f, costPerUnit);
            maximumPoints = Mathf.Max(2, maximumPoints);
            maximumSegments = Mathf.Max(1, maximumSegments);
            maximumStrokes = Mathf.Max(1, maximumStrokes);
            pixelsPerWorldUnit = Mathf.Max(1f, pixelsPerWorldUnit);
            characterHeightPixels = Mathf.Max(1f, characterHeightPixels);
            inkThicknessPixels = Mathf.Max(1f, inkThicknessPixels);
        }
    }
}
