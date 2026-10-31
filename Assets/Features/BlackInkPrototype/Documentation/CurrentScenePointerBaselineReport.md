# Current saved scene A / B / C regression report

Date: 2026-10-30
Unity: 6000.3.23f1
Mode: BASELINE PROBE
Result: FAIL

기존 BlackInkTestScene을 직접 열어 실제 Play Mode에서 검증합니다. 씬 재생성, 배경 교체, 새 카운트다운 추가는 하지 않습니다.

A = runtime platform visibility and spawn support; B = existing preparation and reset timing; C = actual capsule/ink physics.

## Checks

- PASS: Existing saved BlackInkTestScene enters actual Play Mode with its original references.
- PASS: Saved user background survives scene opening and runtime Reset.
- PASS: A: runtime generated platforms have SpriteRenderers.
- PASS: A: Platform 0001 - platform_5 sprite/alpha/material/culling/bounds are renderable.
- FAIL: A: Platform 0001 - platform_5 is explicitly above overlapping background background1_0 (platform=Default/0, background=Default/0).
- PASS: A: Platform 0002 - platform_3 sprite/alpha/material/culling/bounds are renderable.
- FAIL: A: Platform 0002 - platform_3 is explicitly above overlapping background background1_0 (platform=Default/0, background=Default/0).
- PASS: A: Platform 0003 - platform_2 sprite/alpha/material/culling/bounds are renderable.
- FAIL: A: Platform 0003 - platform_2 is explicitly above overlapping background background1_0 (platform=Default/0, background=Default/0).
- PASS: A: Platform 0004 - platform_4 sprite/alpha/material/culling/bounds are renderable.
- FAIL: A: Platform 0004 - platform_4 is explicitly above overlapping background background1_0 (platform=Default/0, background=Default/0).
- PASS: A: actual generated preparation platform supports the spawn feet (downward physics ray).
- FAIL: C: pointer_integer_10deg_1px_left_to_right generates one continuous drawable gentle uphill (strokes=40, length=4.5062).
- PASS: C: pointer_integer_10deg_1px_left_to_right charges only actual generated ink length.
- PASS: C: pointer_integer_10deg_1px_left_to_right actual pointer-created terrain traverses without sustained middle-slope stall (progress=4.7391, maxStall=0.0200).
- FAIL: C: pointer_integer_10deg_1px_right_to_left generates one continuous drawable gentle uphill (strokes=46, length=4.4321).
- PASS: C: pointer_integer_10deg_1px_right_to_left charges only actual generated ink length.
- PASS: C: pointer_integer_10deg_1px_right_to_left actual pointer-created terrain traverses without sustained middle-slope stall (progress=4.6882, maxStall=0.0000).
- PASS: C: pointer_integer_10deg_2px_left_to_right generates one continuous drawable gentle uphill (strokes=1, length=5.0007).
- PASS: C: pointer_integer_10deg_2px_left_to_right charges only actual generated ink length.
- PASS: C: pointer_integer_10deg_2px_left_to_right actual pointer-created terrain traverses without sustained middle-slope stall (progress=4.6823, maxStall=0.0000).
- PASS: C: pointer_integer_10deg_2px_right_to_left generates one continuous drawable gentle uphill (strokes=1, length=5.0007).
- PASS: C: pointer_integer_10deg_2px_right_to_left charges only actual generated ink length.
- PASS: C: pointer_integer_10deg_2px_right_to_left actual pointer-created terrain traverses without sustained middle-slope stall (progress=4.6823, maxStall=0.0000).
- FAIL: C: pointer_integer_20deg_1px_left_to_right generates one continuous drawable gentle uphill (strokes=64, length=2.1203).
- PASS: C: pointer_integer_20deg_1px_left_to_right charges only actual generated ink length.
- FAIL: C: pointer_integer_20deg_1px_left_to_right actual pointer-created terrain traverses without sustained middle-slope stall (progress=4.2273, maxStall=0.0200).
- FAIL: C: pointer_integer_20deg_1px_right_to_left generates one continuous drawable gentle uphill (strokes=64, length=2.4339).
- PASS: C: pointer_integer_20deg_1px_right_to_left charges only actual generated ink length.
- FAIL: C: pointer_integer_20deg_1px_right_to_left actual pointer-created terrain traverses without sustained middle-slope stall (progress=0.6480, maxStall=0.0000).
- PASS: C: pointer_integer_20deg_2px_left_to_right generates one continuous drawable gentle uphill (strokes=1, length=5.2131).
- PASS: C: pointer_integer_20deg_2px_left_to_right charges only actual generated ink length.
- PASS: C: pointer_integer_20deg_2px_left_to_right actual pointer-created terrain traverses without sustained middle-slope stall (progress=4.7357, maxStall=0.0200).
- PASS: C: pointer_integer_20deg_2px_right_to_left generates one continuous drawable gentle uphill (strokes=1, length=5.2132).
- PASS: C: pointer_integer_20deg_2px_right_to_left charges only actual generated ink length.
- PASS: C: pointer_integer_20deg_2px_right_to_left actual pointer-created terrain traverses without sustained middle-slope stall (progress=4.7357, maxStall=0.0200).
- FAIL: C: pointer_integer_30deg_1px_left_to_right generates one continuous drawable gentle uphill (strokes=64, length=2.2631).
- PASS: C: pointer_integer_30deg_1px_left_to_right charges only actual generated ink length.
- FAIL: C: pointer_integer_30deg_1px_left_to_right actual pointer-created terrain traverses without sustained middle-slope stall (progress=1.0080, maxStall=0.0800).
- FAIL: C: pointer_integer_30deg_1px_right_to_left generates one continuous drawable gentle uphill (strokes=64, length=1.6824).
- PASS: C: pointer_integer_30deg_1px_right_to_left charges only actual generated ink length.
- FAIL: C: pointer_integer_30deg_1px_right_to_left actual pointer-created terrain traverses without sustained middle-slope stall (progress=0.6480, maxStall=0.0000).
- FAIL: C: pointer_integer_30deg_2px_left_to_right generates one continuous drawable gentle uphill (strokes=38, length=4.5394).
- PASS: C: pointer_integer_30deg_2px_left_to_right charges only actual generated ink length.
- PASS: C: pointer_integer_30deg_2px_left_to_right actual pointer-created terrain traverses without sustained middle-slope stall (progress=4.7281, maxStall=0.0200).
- FAIL: C: pointer_integer_30deg_2px_right_to_left generates one continuous drawable gentle uphill (strokes=38, length=4.5394).
- PASS: C: pointer_integer_30deg_2px_right_to_left charges only actual generated ink length.
- PASS: C: pointer_integer_30deg_2px_right_to_left actual pointer-created terrain traverses without sustained middle-slope stall (progress=4.7281, maxStall=0.0200).
- PASS: A: temporary rendering probes restore all user background renderers.
- PASS: B: controlled validation keeps Time.timeScale at 1 during real preparation checks.

## Quantitative observations

- Editor indexing environment: ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection.
Parameter name: index
- Scene: Assets/Features/BlackInkPrototype/Scenes/BlackInkTestScene.unity; Unity 6000.3.23f1; graphics=Direct3D12; screen=640x480; render pipeline=UniversalRP
- Original preparationSeconds=3.0000; body shape=Vertical; world size=(0.19, 0.41, 0.00); gravity=2.5000; CCD=Continuous; material friction=0.0000; fixedDeltaTime=0.0200; runSpeed=1.8000
- BACKGROUND background1_0; active=True; enabled=True; sprite=background1_0; alpha=1.0000; material=Sprite-Lit-Default; shader=Universal Render Pipeline/2D/Sprite-Lit-Default; sorting=Default/0; objectLayer=0; cameraMask=-1; bounds=Center: (11.00, 2.52, 0.00), Extents: (14.88, 5.58, 0.15); inCameraFrustum=True
- PLATFORM platform_5 (original aspect ratio); active=True; enabled=True; sprite=platform_5; alpha=1.0000; material=EnvironmentSprite; shader=Universal Render Pipeline/2D/Sprite-Unlit-Default; sorting=Default/0; objectLayer=0; cameraMask=-1; bounds=Center: (0.08, -0.59, 0.00), Extents: (1.62, 0.93, 0.10); inCameraFrustum=True
- PLATFORM platform_3 (original aspect ratio); active=True; enabled=True; sprite=platform_3; alpha=1.0000; material=EnvironmentSprite; shader=Universal Render Pipeline/2D/Sprite-Unlit-Default; sorting=Default/0; objectLayer=0; cameraMask=-1; bounds=Center: (4.43, -1.42, 0.00), Extents: (1.38, 2.21, 0.10); inCameraFrustum=True
- PLATFORM platform_2 (original aspect ratio); active=True; enabled=True; sprite=platform_2; alpha=1.0000; material=EnvironmentSprite; shader=Universal Render Pipeline/2D/Sprite-Unlit-Default; sorting=Default/0; objectLayer=0; cameraMask=-1; bounds=Center: (8.37, -2.75, 0.00), Extents: (1.38, 3.69, 0.10); inCameraFrustum=False
- PLATFORM platform_4 (original aspect ratio); active=True; enabled=True; sprite=platform_4; alpha=1.0000; material=EnvironmentSprite; shader=Universal Render Pipeline/2D/Sprite-Unlit-Default; sorting=Default/0; objectLayer=0; cameraMask=-1; bounds=Center: (13.15, -2.06, 0.00), Extents: (1.73, 2.77, 0.10); inCameraFrustum=False
- C method: SAME saved scene, SAME Rigidbody2D/CapsuleCollider2D, SAME production InkStrokeGeometry, material and FixedUpdate motor. Only Editor fixture terrain is added. Physics2D simulationMode=Script; every 0.02 s step calls production FixedUpdate then Physics2D.Simulate. The controller Update/LateUpdate are disabled during this isolated measurement; original colliders/configuration are restored in finally.
- C INPUT GENERATION pointer_integer_10deg_1px_left_to_right: events=481, nominalPPU=100, actualProjectedPPU=100.0000, localPointerSamplesOverSlopeLimit=85, generatedStrokes=40, points=294, colliderShapes=107, createdLength=4.5062, inkCharge=45.0623, maximumSlope=35.0000, minimumSpacing=0.0100. Actual public ProcessPointer/AppendTo path; no direct SetPoints fixture is used.
- C INPUT PHYSICS pointer_integer_10deg_1px_left_to_right: generatedStrokes=40, reached=True, progress=4.7391, seconds=2.9800, avgVx=1.5903, minVx=0.0571, maxStall=0.0200, grounded=1.9000, maxAir=0.0400, maxPenetration=0.0138, firstStall=none.
- C INPUT GENERATION pointer_integer_10deg_1px_right_to_left: events=481, nominalPPU=100, actualProjectedPPU=100.0000, localPointerSamplesOverSlopeLimit=85, generatedStrokes=46, points=294, colliderShapes=104, createdLength=4.4321, inkCharge=44.3207, maximumSlope=35.0000, minimumSpacing=0.0100. Actual public ProcessPointer/AppendTo path; no direct SetPoints fixture is used.
- C INPUT PHYSICS pointer_integer_10deg_1px_right_to_left: generatedStrokes=46, reached=True, progress=4.6882, seconds=2.8800, avgVx=1.6278, minVx=0.6820, maxStall=0.0000, grounded=2.1400, maxAir=0.0400, maxPenetration=0.0147, firstStall=none.
- C INPUT GENERATION pointer_integer_10deg_2px_left_to_right: events=241, nominalPPU=100, actualProjectedPPU=100.0000, localPointerSamplesOverSlopeLimit=0, generatedStrokes=1, points=241, colliderShapes=86, createdLength=5.0007, inkCharge=50.0066, maximumSlope=35.0000, minimumSpacing=0.0100. Actual public ProcessPointer/AppendTo path; no direct SetPoints fixture is used.
- C INPUT PHYSICS pointer_integer_10deg_2px_left_to_right: generatedStrokes=1, reached=True, progress=4.6823, seconds=2.8800, avgVx=1.6258, minVx=1.0850, maxStall=0.0000, grounded=2.0800, maxAir=0.0400, maxPenetration=0.0138, firstStall=none.
- C INPUT GENERATION pointer_integer_10deg_2px_right_to_left: events=241, nominalPPU=100, actualProjectedPPU=100.0000, localPointerSamplesOverSlopeLimit=0, generatedStrokes=1, points=241, colliderShapes=86, createdLength=5.0007, inkCharge=50.0065, maximumSlope=35.0000, minimumSpacing=0.0100. Actual public ProcessPointer/AppendTo path; no direct SetPoints fixture is used.
- C INPUT PHYSICS pointer_integer_10deg_2px_right_to_left: generatedStrokes=1, reached=True, progress=4.6823, seconds=2.8800, avgVx=1.6258, minVx=1.0850, maxStall=0.0000, grounded=2.0800, maxAir=0.0400, maxPenetration=0.0138, firstStall=none.
- C INPUT GENERATION pointer_integer_20deg_1px_left_to_right: events=481, nominalPPU=100, actualProjectedPPU=100.0000, localPointerSamplesOverSlopeLimit=175, generatedStrokes=64, points=178, colliderShapes=96, createdLength=2.1203, inkCharge=21.2026, maximumSlope=35.0000, minimumSpacing=0.0100. Actual public ProcessPointer/AppendTo path; no direct SetPoints fixture is used.
- C INPUT PHYSICS pointer_integer_20deg_1px_left_to_right: generatedStrokes=64, reached=False, progress=4.2273, seconds=2.6400, avgVx=1.6012, minVx=0.0214, maxStall=0.0200, grounded=1.3600, maxAir=0.8400, maxPenetration=0.0110, firstStall=none.
- C INPUT GENERATION pointer_integer_20deg_1px_right_to_left: events=481, nominalPPU=100, actualProjectedPPU=100.0000, localPointerSamplesOverSlopeLimit=175, generatedStrokes=64, points=207, colliderShapes=108, createdLength=2.4339, inkCharge=24.3388, maximumSlope=35.0000, minimumSpacing=0.0100. Actual public ProcessPointer/AppendTo path; no direct SetPoints fixture is used.
- C INPUT PHYSICS pointer_integer_20deg_1px_right_to_left: generatedStrokes=64, reached=False, progress=0.6480, seconds=0.3600, avgVx=1.8000, minVx=1.8000, maxStall=0.0000, grounded=0.0000, maxAir=0.3600, maxPenetration=0.0000, firstStall=none.
- C INPUT GENERATION pointer_integer_20deg_2px_left_to_right: events=241, nominalPPU=100, actualProjectedPPU=100.0000, localPointerSamplesOverSlopeLimit=0, generatedStrokes=1, points=241, colliderShapes=128, createdLength=5.2131, inkCharge=52.1313, maximumSlope=35.0000, minimumSpacing=0.0100. Actual public ProcessPointer/AppendTo path; no direct SetPoints fixture is used.
- C INPUT PHYSICS pointer_integer_20deg_2px_left_to_right: generatedStrokes=1, reached=True, progress=4.7357, seconds=3.0800, avgVx=1.5376, minVx=0.1175, maxStall=0.0200, grounded=2.4000, maxAir=0.0200, maxPenetration=0.0147, firstStall=none.
- C INPUT GENERATION pointer_integer_20deg_2px_right_to_left: events=241, nominalPPU=100, actualProjectedPPU=100.0000, localPointerSamplesOverSlopeLimit=0, generatedStrokes=1, points=241, colliderShapes=128, createdLength=5.2132, inkCharge=52.1313, maximumSlope=35.0000, minimumSpacing=0.0100. Actual public ProcessPointer/AppendTo path; no direct SetPoints fixture is used.
- C INPUT PHYSICS pointer_integer_20deg_2px_right_to_left: generatedStrokes=1, reached=True, progress=4.7357, seconds=3.0800, avgVx=1.5376, minVx=0.1175, maxStall=0.0200, grounded=2.4000, maxAir=0.0200, maxPenetration=0.0147, firstStall=none.
- C INPUT GENERATION pointer_integer_30deg_1px_left_to_right: events=481, nominalPPU=100, actualProjectedPPU=100.0000, localPointerSamplesOverSlopeLimit=277, generatedStrokes=64, points=184, colliderShapes=111, createdLength=2.2631, inkCharge=22.6302, maximumSlope=35.0000, minimumSpacing=0.0100. Actual public ProcessPointer/AppendTo path; no direct SetPoints fixture is used.
- C INPUT PHYSICS pointer_integer_30deg_1px_left_to_right: generatedStrokes=64, reached=False, progress=1.0080, seconds=0.6400, avgVx=1.5750, minVx=0.0000, maxStall=0.0800, grounded=0.0000, maxAir=0.6400, maxPenetration=0.0006, firstStall=none.
- C INPUT GENERATION pointer_integer_30deg_1px_right_to_left: events=481, nominalPPU=100, actualProjectedPPU=100.0000, localPointerSamplesOverSlopeLimit=277, generatedStrokes=64, points=163, colliderShapes=94, createdLength=1.6824, inkCharge=16.8221, maximumSlope=35.0000, minimumSpacing=0.0100. Actual public ProcessPointer/AppendTo path; no direct SetPoints fixture is used.
- C INPUT PHYSICS pointer_integer_30deg_1px_right_to_left: generatedStrokes=64, reached=False, progress=0.6480, seconds=0.3600, avgVx=1.8000, minVx=1.8000, maxStall=0.0000, grounded=0.0000, maxAir=0.3600, maxPenetration=0.0000, firstStall=none.
- C INPUT GENERATION pointer_integer_30deg_2px_left_to_right: events=241, nominalPPU=100, actualProjectedPPU=100.0000, localPointerSamplesOverSlopeLimit=37, generatedStrokes=38, points=241, colliderShapes=154, createdLength=4.5394, inkCharge=45.3923, maximumSlope=35.0000, minimumSpacing=0.0100. Actual public ProcessPointer/AppendTo path; no direct SetPoints fixture is used.
- C INPUT PHYSICS pointer_integer_30deg_2px_left_to_right: generatedStrokes=38, reached=True, progress=4.7281, seconds=3.4200, avgVx=1.3825, minVx=0.1891, maxStall=0.0200, grounded=2.5800, maxAir=0.0400, maxPenetration=0.0145, firstStall=none.
- C INPUT GENERATION pointer_integer_30deg_2px_right_to_left: events=241, nominalPPU=100, actualProjectedPPU=100.0000, localPointerSamplesOverSlopeLimit=37, generatedStrokes=38, points=241, colliderShapes=154, createdLength=4.5394, inkCharge=45.3922, maximumSlope=35.0000, minimumSpacing=0.0100. Actual public ProcessPointer/AppendTo path; no direct SetPoints fixture is used.
- C INPUT PHYSICS pointer_integer_30deg_2px_right_to_left: generatedStrokes=38, reached=True, progress=4.7281, seconds=3.4200, avgVx=1.3825, minVx=0.1891, maxStall=0.0200, grounded=2.5800, maxAir=0.0400, maxPenetration=0.0145, firstStall=none.
- Time.timeScale restored to its pre-run value: 1.0000.

## Collected failures

- A: Platform 0001 - platform_5 is explicitly above overlapping background background1_0 (platform=Default/0, background=Default/0).
- A: Platform 0002 - platform_3 is explicitly above overlapping background background1_0 (platform=Default/0, background=Default/0).
- A: Platform 0003 - platform_2 is explicitly above overlapping background background1_0 (platform=Default/0, background=Default/0).
- A: Platform 0004 - platform_4 is explicitly above overlapping background background1_0 (platform=Default/0, background=Default/0).
- C: pointer_integer_10deg_1px_left_to_right generates one continuous drawable gentle uphill (strokes=40, length=4.5062).
- C: pointer_integer_10deg_1px_right_to_left generates one continuous drawable gentle uphill (strokes=46, length=4.4321).
- C: pointer_integer_20deg_1px_left_to_right generates one continuous drawable gentle uphill (strokes=64, length=2.1203).
- C: pointer_integer_20deg_1px_left_to_right actual pointer-created terrain traverses without sustained middle-slope stall (progress=4.2273, maxStall=0.0200).
- C: pointer_integer_20deg_1px_right_to_left generates one continuous drawable gentle uphill (strokes=64, length=2.4339).
- C: pointer_integer_20deg_1px_right_to_left actual pointer-created terrain traverses without sustained middle-slope stall (progress=0.6480, maxStall=0.0000).
- C: pointer_integer_30deg_1px_left_to_right generates one continuous drawable gentle uphill (strokes=64, length=2.2631).
- C: pointer_integer_30deg_1px_left_to_right actual pointer-created terrain traverses without sustained middle-slope stall (progress=1.0080, maxStall=0.0800).
- C: pointer_integer_30deg_1px_right_to_left generates one continuous drawable gentle uphill (strokes=64, length=1.6824).
- C: pointer_integer_30deg_1px_right_to_left actual pointer-created terrain traverses without sustained middle-slope stall (progress=0.6480, maxStall=0.0000).
- C: pointer_integer_30deg_2px_left_to_right generates one continuous drawable gentle uphill (strokes=38, length=4.5394).
- C: pointer_integer_30deg_2px_right_to_left generates one continuous drawable gentle uphill (strokes=38, length=4.5394).

Baseline mode retains every failed assertion and exits 0 after completing the probe. Strict mode exits 1 for any collected failure. Endpoint controls are diagnostic. Airborne duration and horizontal/total speed are reported separately, so downhill ballistic motion is not mislabeled as a collider stall.
