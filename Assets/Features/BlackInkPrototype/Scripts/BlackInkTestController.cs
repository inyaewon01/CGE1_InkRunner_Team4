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
        [HideInInspector] public float inkThicknessPixels = 10f;
        [Min(0.1f)] public float originalCharacterBodyHeight = 2.10f;
        [Range(0.03f, 0.4f)] public float runnerViewportX = 0.08f;
        [Range(0.15f, 0.55f)] public float groundViewportY = 0.27f;

        [Header("Ink / measured reference pose")]
        [Min(0.01f)] public float maximumInk = 100f;
        [Min(0.01f)] public float costPerUnit = 10f;
        [Min(0f)] public float inkRegenerationDelay = 1f;
        [Tooltip("Fraction of maximum ink restored per second while running and not holding a valid draw gesture.")]
        [Range(0f, 1f)] public float inkRegenerationRate = 0.2f;
        [Tooltip("run_01: head to boot sole, approximately 210 pixels / 100 PPU; excludes sword and cape.")]
        [Min(0.1f)] public float characterBodyHeight = 2.10f;
        [Range(0.05f, 1.5f)] public float thicknessRatio = 0.25f;
        [Min(0.001f)] public float minimumPointSpacingRatio = 0.025f;
        public bool clearPreviousOnNewStroke;

        [Header("Trial")]
        [Min(0f)] public float preparationSeconds = 3f;
        [Min(0.1f)] public float runSpeed = 1.8f;
        [Tooltip("Legacy two-platform trial only. In endless mode use EndlessInkWorld Minimum / Maximum Platform Gap.")]
        [Min(0f)] public float platformGap = 3f;
        [Tooltip("Maximum walkable / drawable slope. Steeper contacts block forward movement.")]
        [Range(5f, 80f)] public float maximumSlope = 60f;
        [Tooltip("Maximum grounded step height as a fraction of the fixed character body height.")]
        [Range(0f, 0.8f)] public float maximumStepRatio = 0.4f;
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
        private bool whiteInk;
        private BlackInkHud hud;
        private Vector2 startPosition;
        private Vector2 anchor;
        private Vector2 anchorViewOrigin;
        private Vector2 previousScreen;
        private bool held, suppressUntilRelease, hasAnchor, focused = true;
        private bool validDrawHeld;
        private float inkRegenerationReadyAt;
        private int strokeDirection;
        private float preparationRemaining;
        private readonly ContactPoint2D[] groundContacts = new ContactPoint2D[32];
        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[16];
        private bool wasSupported;
        private bool stepLiftPending;
        private readonly RaycastHit2D[] stepHits = new RaycastHit2D[16];
        private readonly Collider2D[] stepOverlaps = new Collider2D[16];
        private string feedback = "Draw across the gap before the runner arrives.";
        private string failureReason;

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
                inkThicknessPixels = characterHeightPixels * thicknessRatio;
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
                    mouse.leftButton.isPressed, mouse.leftButton.wasReleasedThisFrame, mouse.rightButton.wasPressedThisFrame);
            RegenerateInk();
            CleanupBehindCamera();
            UpdateHud();
        }

        private void RegenerateInk()
        {
            if (State != TrialState.Running || validDrawHeld) return;
            float recoverySeconds = Mathf.Max(0f,
                Time.time - Mathf.Max(inkRegenerationReadyAt, Time.time - Time.deltaTime));
            CurrentInk = Mathf.Min(maximumInk,
                CurrentInk + maximumInk * inkRegenerationRate * recoverySeconds);
        }

        private void FixedUpdate()
        {
            if (!character) return;
            Vector2 velocity = character.linearVelocity;
            if (stepLiftPending)
            {
                // MovePosition's lift velocity must not become an airborne jump.
                velocity.y = 0f;
                stepLiftPending = false;
            }
            if (State != TrialState.Running)
            {
                velocity.x = 0f;
                character.linearVelocity = velocity;
                wasSupported = false;
                return;
            }

            float minimumNormalY = Mathf.Cos(maximumSlope * Mathf.Deg2Rad) - 0.0001f;
            Vector2 normal = Vector2.zero;
            bool wall = false;
            var filter = new ContactFilter2D();
            filter.NoFilter();
            filter.useTriggers = false;
            int count = characterBody.GetContacts(filter, groundContacts);
            for (int i = 0; i < count; i++)
            {
                var contact = groundContacts[i];
                Collider2D surface = contact.collider == characterBody ? contact.otherCollider : contact.collider;
                Vector2 n = SurfaceNormal(surface, contact.point, contact.normal);
                if (n.x < -0.01f && n.y < minimumNormalY && n.y > -0.1f) wall = true;
                if (n.y >= minimumNormalY && n.y > normal.y &&
                    groundContacts[i].point.y <= characterBody.bounds.center.y)
                    normal = n;
            }
            if (wasSupported && normal != Vector2.zero && TryStepUp(filter))
            {
                wasSupported = false;
                stepLiftPending = true;
                return;
            }
            // Only recover an immediately adjacent support after a grounded step.
            // There is no position snap, long downward ray, or attachment across a cliff.
            if (normal == Vector2.zero && wasSupported && velocity.y <= 0.01f)
            {
                count = characterBody.Cast(Vector2.down, filter, groundHits, 0.02f);
                for (int i = 0; i < count; i++)
                {
                    Vector2 n = groundHits[i].normal;
                    if (n.y >= minimumNormalY && n.y > normal.y) normal = n;
                }
            }
            wasSupported = normal != Vector2.zero;
            if (wasSupported)
            {
                Vector2 tangent = new Vector2(normal.y, -normal.x);
                // Compensate this step's gravity only while supported. Physics still
                // resolves contacts; airborne motion keeps gravity and vertical momentum.
                velocity = tangent * (wall ? 0f : runSpeed) -
                    Physics2D.gravity * character.gravityScale * Time.fixedDeltaTime;
            }
            else velocity.x = wall ? 0f : runSpeed;
            character.linearVelocity = velocity;
        }

        private bool TryStepUp(ContactFilter2D filter)
        {
            float limit = characterBodyHeight * maximumStepRatio;
            if (limit <= 0f || runSpeed <= 0f) return false;
            Bounds body = characterBody.bounds;
            float skin = Mathf.Max(0.001f, characterBodyHeight * 0.01f);
            float minimumNormalY = Mathf.Cos(maximumSlope * Mathf.Deg2Rad) - 0.0001f;
            if (!TryStepSurface(new Vector2(body.center.x, body.min.y + skin),
                    characterBodyHeight * 0.5f, filter, minimumNormalY, out RaycastHit2D support)) return false;
            Vector2 supportPoint = support.point;
            Vector2 groundNormal = SurfaceNormal(support.collider, support.point, support.normal);
            int count = characterBody.Cast(Vector2.right, filter, stepHits, runSpeed * Time.fixedDeltaTime + skin);
            RaycastHit2D front = default;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = stepHits[i];
                Vector2 n = SurfaceNormal(hit.collider, hit.point, hit.normal);
                if (n.x < -0.01f && n.y < minimumNormalY && n.y > -0.1f &&
                    (!front.collider || hit.distance < front.distance)) front = hit;
            }
            if (!front.collider || IsStepObstacle(front.collider)) return false;

            float baseline = supportPoint.y - groundNormal.x / groundNormal.y * (front.point.x - supportPoint.x);
            Vector2 probe = new Vector2(front.point.x + skin, baseline + limit + skin);
            if (!TryStepSurface(probe, limit + skin * 2f, filter, minimumNormalY, out RaycastHit2D top))
                return false;
            Vector2 topNormal = SurfaceNormal(top.collider, top.point, top.normal);
            float height = top.point.y + topNormal.x / topNormal.y * skin - baseline;
            if (height <= skin || height > limit + 0.0001f) return false;
            Vector2 landingProbe = probe + new Vector2(body.size.x, -topNormal.x / topNormal.y * body.size.x);
            if (!TryStepSurface(landingProbe, limit + skin * 2f,
                    filter, minimumNormalY, out RaycastHit2D landing) ||
                Mathf.Abs(landing.point.y - top.point.y) > Mathf.Tan(maximumSlope * Mathf.Deg2Rad) * body.size.x + skin) return false;

            // Require continuous supporting floor up to the riser, including edge-only terrain.
            for (float x = body.center.x; x < front.point.x; x += skin)
            {
                float floorY = supportPoint.y - groundNormal.x / groundNormal.y * (x - supportPoint.x);
                if (!TryStepSurface(new Vector2(x, floorY + skin), skin * 2f,
                        filter, minimumNormalY, out _)) return false;
            }

            float lift = top.point.y - body.min.y + skin;
            if (lift <= 0f) return false;
            count = characterBody.Cast(Vector2.up, filter, stepHits, lift);
            for (int i = 0; i < count; i++)
                if (stepHits[i].distance < lift && stepHits[i].normal.y < -0.01f) return false;
            count = Physics2D.OverlapCapsule((Vector2)body.center + Vector2.up * lift,
                body.size, characterBody.direction, character.rotation, filter, stepOverlaps);
            if (count == stepOverlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (stepOverlaps[i].attachedRigidbody != character) return false;

            Vector2 raisedCenter = (Vector2)body.center + Vector2.up * lift;
            float landingDistance = Mathf.Max(0f, front.point.x + body.size.x - body.center.x);
            count = Physics2D.CapsuleCast(raisedCenter, body.size, characterBody.direction,
                character.rotation, Vector2.right, filter, stepHits, landingDistance);
            if (count == stepHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = stepHits[i];
                if (hit.collider.attachedRigidbody == character) continue;
                if (IsStepObstacle(hit.collider) ||
                    SurfaceNormal(hit.collider, hit.point, hit.normal).y < minimumNormalY) return false;
            }

            character.MovePosition(character.position + Vector2.up * lift);
            return true;
        }

        private bool TryStepSurface(Vector2 origin, float distance, ContactFilter2D filter,
            float minimumNormalY, out RaycastHit2D surface)
        {
            int count = Physics2D.Raycast(origin, Vector2.down, filter, groundHits, distance);
            surface = default;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = groundHits[i];
                if (hit.collider.attachedRigidbody == character) continue;
                if (!surface.collider || hit.distance < surface.distance) surface = hit;
            }
            return surface.collider && !IsStepObstacle(surface.collider) &&
                SurfaceNormal(surface.collider, surface.point, surface.normal).y >= minimumNormalY;
        }

        private static bool IsStepObstacle(Collider2D surface) =>
            surface.GetComponentInParent<Obstacle>() || surface.tag == "Obstacle";

        // Polygon decomposition can return a steep corner normal on a densely
        // sampled, walkable ink top. Use the actual ribbon segment near that top;
        // leave underside contacts and upright endpoint walls to normal physics.
        private Vector2 SurfaceNormal(Collider2D surface, Vector2 point, Vector2 fallback)
        {
            if (!surface || fallback.y < 0f) return fallback;
            var ink = surface.GetComponent<InkStrokeGeometry>();
            if (!ink || ink.PointCount < 2) return fallback;
            Vector2 p = ink.transform.InverseTransformPoint(point);
            var path = ink.Points;
            int low = 0, high = path.Count - 1;
            if (p.x <= path[low].x + 0.00001f || p.x >= path[high].x - 0.00001f) return fallback;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (path[middle].x <= p.x) low = middle; else high = middle;
            }
            Vector2 a = path[low], b = path[high];
            float top = Mathf.Lerp(a.y, b.y, (p.x - a.x) / (b.x - a.x));
            if (Mathf.Abs(p.y - top) > 0.025f) return fallback;
            Vector2 tangent = ink.transform.TransformVector(b - a).normalized;
            return new Vector2(-tangent.y, tangent.x);
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
                State == TrialState.Failed ? (!string.IsNullOrEmpty(failureReason) ? $"GAME OVER  / {failureReason} - press Reset" : "GAME OVER  / press Reset to retry") : "CROSSED  / test complete";
            if (hud) hud.Present(CurrentInk, maximumInk, characterBodyHeight, Thickness, costPerUnit,
                clearPreviousOnNewStroke, PointCount, StrokeCount, state, feedback, whiteInk ? "WHITE" : "BLACK");
            if (hud && endlessMode)
                hud.PresentRunner(DistanceTravelled, ProjectedBodyHeightPixels, ProjectedInkThicknessPixels,
                    endlessWorld.ActivePlatformCount, endlessWorld.ActiveObstacleCount);
        }

        // Public input seam also used by deterministic editor validation; no legacy Input API.
        public void ProcessPointer(Vector2 screen, bool pressed, bool down, bool released, bool switchInk = false)
        {
            if (switchInk && focused)
            {
                whiteInk = !whiteInk;
                if (held)
                {
                    EndStroke();
                    held = false;
                    if (down) suppressUntilRelease = true;
                }
            }

            if (released || !down)
            {
                if (released && held && !suppressUntilRelease && focused && InWindow(screen))
                    PaintSweep(screen, true);
                if (validDrawHeld) inkRegenerationReadyAt = Time.time + inkRegenerationDelay;
                validDrawHeld = false;
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
                // An empty tank still counts as a draw attempt until the left button is released.
                validDrawHeld = !suppressUntilRelease && !IsOverUI(screen) && InWindow(screen) &&
                    PointAllowed(ScreenToInk(screen));
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
                // Keep the existing stroke, but never paint or charge camera-only motion.
                return;
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
                validDrawHeld = true;
                if (!hasAnchor) { anchor = point; anchorViewOrigin = ScreenToInk(Vector2.zero); hasAnchor = true; continue; }
                AppendTo(point, false, ScreenToInk(Vector2.zero) - anchorViewOrigin);
            }
            if (flushEndpoint && hasAnchor && !suppressUntilRelease && !IsOverUI(screen))
                AppendTo(ScreenToInk(screen), true, ScreenToInk(Vector2.zero) - anchorViewOrigin);
            previousScreen = screen;
        }

        private void SetAnchorFromScreen(Vector2 screen)
        {
            Vector2 point = ScreenToInk(screen);
            if (PointAllowed(point)) { anchor = point; anchorViewOrigin = ScreenToInk(Vector2.zero); hasAnchor = true; }
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

        public bool AppendTo(Vector2 requestedEnd, bool forceShortSegment = false, Vector2 scrollDelta = default)
        {
            if (!hasAnchor || CurrentInk <= 0f) return false;
            float requestedLength = Vector2.Distance(anchor, requestedEnd);
            if (requestedLength < (forceShortSegment ? 0.00001f : MinimumPointSpacing)) return false;
            float inputLength = (requestedEnd - anchor - scrollDelta).magnitude;
            if (inputLength < 0.00001f) return false;
            float fraction = Mathf.Min(1f, CurrentInk / (inputLength * costPerUnit));
            Vector2 end = Vector2.Lerp(anchor, requestedEnd, fraction);
            float length = Vector2.Distance(anchor, end);
            if (length < 0.00001f) return false;
            int direction = end.x >= anchor.x ? 1 : -1;
            bool reversing = strokeDirection != 0 && strokeDirection != direction &&
                Mathf.Abs(end.x - anchor.x) > 0.00001f;
            bool gentle = InkPathMath.GentleSegment(anchor, end, maximumSlope);
            // A rounded cursor at the slope limit can overshoot by one pixel.
            // Correct only that precision error; mesh and collider share this top,
            // and every committed segment still respects the configured angle.
            float maxRise = Mathf.Abs(end.x - anchor.x) * Mathf.Tan(maximumSlope * Mathf.Deg2Rad);
            float pixel = 2f * drawingCamera.orthographicSize / Mathf.Max(1, drawingCamera.pixelHeight);
            float excess = Mathf.Abs(end.y - anchor.y) - maxRise;
            if (!gentle && !reversing && Mathf.Abs(end.x - anchor.x) > 0.00001f &&
                excess > 0f && excess <= pixel * 1.01f)
            {
                end.y = anchor.y + Mathf.Sign(end.y - anchor.y) * maxRise;
                length = Vector2.Distance(anchor, end);
                gentle = true;
            }
            // At the 40 px body scale the spacing is one pixel. A rounded mouse
            // step can be (1, 1) even on a 20-degree line. Keep the last valid
            // anchor until a few pixels establish the slope, instead of ending
            // the collider at every such step. Pending input creates no ink/cost.
            // Created-path reversals, forbidden bodies and sustained steep input still split.
            if (!gentle && !reversing && requestedLength < MinimumPointSpacing * 6f &&
                SegmentAllowed(anchor, end))
                return false;
            if (!gentle || reversing)
            {
                EndStroke();
                anchor = requestedEnd;
                anchorViewOrigin = ScreenToInk(Vector2.zero);
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
                GameObject strokeObject = new GameObject($"{(whiteInk ? "White" : "Black")} ink stroke {strokes.Count + 1:00}");
                strokeObject.transform.SetParent(inkRoot, false);
                activeStroke = strokeObject.AddComponent<InkStrokeGeometry>();
                activeStroke.Initialize(inkMaterial, Thickness, textureScale, edgeRoughness, surfaceMaterial, whiteInk);
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
            float chargedLength = (end - anchor - scrollDelta * fraction).magnitude;
            CurrentInk = Mathf.Max(0f, CurrentInk - chargedLength * costPerUnit);
            anchor = end;
            anchorViewOrigin = ScreenToInk(Vector2.zero);
            Physics2D.SyncTransforms();
            feedback = $"Created {CreatedLength:0.00} world units.";
            if (CurrentInk < 0.0001f)
            {
                CurrentInk = 0f;
                EndStroke();
                suppressUntilRelease = true;
                feedback = "Ink empty. Release the left button to let ink recover.";
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
            character.constraints = RigidbodyConstraints2D.FreezeRotation;
            failureReason = null;
            if (character)
            {
                var warriorCollision = character.GetComponent<InkWarriorCollision>();
                if (warriorCollision) warriorCollision.ResetState();
            }
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
            whiteInk = false;
            validDrawHeld = false;
            inkRegenerationReadyAt = Time.time + inkRegenerationDelay;
            preparationRemaining = preparationSeconds;
            State = TrialState.Preparing;
            wasSupported = false;
            stepLiftPending = false;
            inkThicknessPixels = characterHeightPixels * thicknessRatio;
            held = false;
            suppressUntilRelease = Mouse.current != null && Mouse.current.leftButton.isPressed;
            if (characterAnimator)
            {
                characterAnimator.Play("InkWarrior_Run", 0, 0f);
                characterAnimator.Update(0f);
                characterAnimator.speed = 0f;
            }
            feedback = endlessMode ? $"Bridge the random gaps with {inkThicknessPixels:0} px ink. Obstacles are visual only." :
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

        public void Finish(TrialState result)
        {
            State = result;
            validDrawHeld = false;
            EndStroke();
            if (character)
            {
                character.linearVelocity = Vector2.zero;
                character.angularVelocity = 0f;
                if (result == TrialState.Failed)
                {
                    character.constraints = RigidbodyConstraints2D.FreezeAll;
                    character.simulated = false;
                }
            }

            if (characterAnimator) characterAnimator.speed = 0f;
        }

        public void Fail(string customFeedback = null)
        {
            if (State == TrialState.Failed) return;
            failureReason = customFeedback;
            Finish(TrialState.Failed);
            if (!string.IsNullOrEmpty(customFeedback))
            {
                feedback = customFeedback;
            }
            UpdateHud();
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (validDrawHeld) inkRegenerationReadyAt = Time.time + inkRegenerationDelay;
            validDrawHeld = false;
            EndStroke();
            held = false;
            suppressUntilRelease = true;
        }

        private void OnDisable()
        {
            if (validDrawHeld) inkRegenerationReadyAt = Time.time + inkRegenerationDelay;
            validDrawHeld = false;
            EndStroke();
            held = false;
        }

        private void OnValidate()
        {
            maximumInk = Mathf.Max(0.01f, maximumInk);
            costPerUnit = Mathf.Max(0.01f, costPerUnit);
            inkRegenerationDelay = Mathf.Max(0f, inkRegenerationDelay);
            inkRegenerationRate = Mathf.Clamp01(inkRegenerationRate);
            maximumPoints = Mathf.Max(2, maximumPoints);
            maximumSegments = Mathf.Max(1, maximumSegments);
            maximumStrokes = Mathf.Max(1, maximumStrokes);
            pixelsPerWorldUnit = Mathf.Max(1f, pixelsPerWorldUnit);
            characterHeightPixels = Mathf.Max(1f, characterHeightPixels);
            inkThicknessPixels = Mathf.Max(1f, inkThicknessPixels);
            thicknessRatio = Mathf.Clamp(thicknessRatio, 0.05f, 1.5f);
            maximumSlope = Mathf.Clamp(maximumSlope, 5f, 80f);
        }
    }
}
