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
- PASS: A: runtime platform art produces actual camera pixels when background is temporarily hidden.
- FAIL: A: actual preparation render visibly contains runtime platforms with user background enabled (pixel ratio=0.0000).
- PASS: B: saved preparation is the existing three-second setting (3.0000 s).
- PASS: B: existing preparation permits actual pointer drawing and charges ink.
- PASS: B: existing READY decimal-seconds HUD is present; no new 3/2/1 interface is created.
- PASS: B: preparation keeps runner/map/distance still: dx=0.0000, dy=0.0020, distance=0.0000, cameraDx=0.0000.
- PASS: B: existing preparation enters Running exactly once after 3.0001 scaled seconds (configured=3.0000).
- PASS: B: saved preparation is the existing three-second setting (3.0000 s).
- PASS: B: Reset during preparation starts one fresh delay; early=False, starts=1, runAt=3.0013 s.
- PASS: B: saved preparation is the existing three-second setting (3.0000 s).
- PASS: B: running reset fixture first observes real runner motion.
- PASS: B: Reset during running starts one fresh delay; early=False, starts=1, runAt=3.0020 s.
- PASS: B: saved preparation is the existing three-second setting (3.0000 s).
- PASS: B: Ten rapid Resets followed by final Reset (old countdown cancellation) starts one fresh delay; early=False, starts=1, runAt=3.0010 s.
- PASS: C: slope_0 traverses without sustained stalls, falling through or deep penetration (dx=4.8240, maxStall=0.0000, penetration=0.0050).
- PASS: C: slope_0 retains useful forward speed (avg=1.8000, configured=1.8000).
- PASS: C: slope_+10 traverses without sustained stalls, falling through or deep penetration (dx=4.8623, maxStall=0.0000, penetration=0.0050).
- PASS: C: slope_+10 retains useful forward speed (avg=1.7121, configured=1.8000).
- PASS: C: slope_-10 traverses without sustained stalls, falling through or deep penetration (dx=4.7846, maxStall=0.0000, penetration=0.0050).
- PASS: C: slope_-10 retains useful forward speed (avg=1.8837, configured=1.8000).
- PASS: C: slope_+20 traverses without sustained stalls, falling through or deep penetration (dx=4.8921, maxStall=0.0000, penetration=0.0015).
- PASS: C: slope_+20 retains useful forward speed (avg=1.6199, configured=1.8000).
- PASS: C: slope_-20 traverses without sustained stalls, falling through or deep penetration (dx=4.7417, maxStall=0.0000, penetration=0.0070).
- PASS: C: slope_-20 retains useful forward speed (avg=1.9757, configured=1.8000).
- PASS: C: slope_+35 traverses without sustained stalls, falling through or deep penetration (dx=4.9637, maxStall=0.0000, penetration=0.0050).
- PASS: C: slope_+35 retains useful forward speed (avg=1.4514, configured=1.8000).
- PASS: C: slope_-35 traverses without sustained stalls, falling through or deep penetration (dx=4.6979, maxStall=0.0000, penetration=0.0050).
- PASS: C: slope_-35 retains useful forward speed (avg=2.1354, configured=1.8000).
- PASS: C: connected_flat_up10_down10_flat traverses without sustained stalls, falling through or deep penetration (dx=4.8099, maxStall=0.0000, penetration=0.0060).
- PASS: C: connected_flat_up10_down10_flat retains useful forward speed (avg=1.7947, configured=1.8000).
- PASS: C: connected_flat_up20_down20_flat traverses without sustained stalls, falling through or deep penetration (dx=4.8209, maxStall=0.0000, penetration=0.0150).
- PASS: C: connected_flat_up20_down20_flat retains useful forward speed (avg=1.7855, configured=1.8000).
- PASS: C: connected_flat_up35_down35_flat traverses without sustained stalls, falling through or deep penetration (dx=4.8156, maxStall=0.0000, penetration=0.0150).
- PASS: C: connected_flat_up35_down35_flat retains useful forward speed (avg=1.7575, configured=1.8000).
- PASS: C: dense_collinear_20_300points traverses without sustained stalls, falling through or deep penetration (dx=4.8841, maxStall=0.0000, penetration=0.0103).
- PASS: C: dense_collinear_20_300points retains useful forward speed (avg=1.5755, configured=1.8000).
- PASS: C: dense_collinear_20_600points traverses without sustained stalls, falling through or deep penetration (dx=4.8926, maxStall=0.0000, penetration=0.0103).
- PASS: C: dense_collinear_20_600points retains useful forward speed (avg=1.5194, configured=1.8000).
- PASS: C: dense_small_jitter_10 traverses without sustained stalls, falling through or deep penetration (dx=4.8182, maxStall=0.0000, penetration=0.0127).
- PASS: C: dense_small_jitter_10 retains useful forward speed (avg=1.6501, configured=1.8000).
- PASS: C: growing_stroke_actor_on_existing_valid_part traverses without sustained stalls, falling through or deep penetration (dx=4.8405, maxStall=0.0000, penetration=0.0112).
- PASS: C: growing_stroke_actor_on_existing_valid_part retains useful forward speed (avg=1.6925, configured=1.8000).
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
- A rendered differential (1920x1080 real URP + existing HUD): platform-contributing pixels with background=0, without background=201938, visibility ratio=0.0000. Four immediate captures share one physics pose; only the named renderers are temporarily toggled.
- A BASELINE ROOT CAUSE: platform pixels appear after hiding background, confirming renderer occlusion rather than missing platform physics/art.
- C method: SAME saved scene, SAME Rigidbody2D/CapsuleCollider2D, SAME production InkStrokeGeometry, material and FixedUpdate motor. Only Editor fixture terrain is added. Physics2D simulationMode=Script; every 0.02 s step calls production FixedUpdate then Physics2D.Simulate. The controller Update/LateUpdate are disabled during this isolated measurement; original colliders/configuration are restored in finally.
- C: slope_0; reached=True; dx=4.8240; time=2.6800; avgVx=1.8000; vx=[1.8000,1.8000]; speed=[1.8000,1.8000]; grounded=2.6800; maxAir=0.0000; maxStall=0.0000; maxPenetration=0.0050; contactSamples=134; growthEdits=0; sourcePoints=3; colliderShapeCount=1; firstStall=none.
- C: slope_+10; reached=True; dx=4.8623; time=2.8400; avgVx=1.7121; vx=[1.5176,1.8000]; speed=[1.6201,1.8016]; grounded=2.8200; maxAir=0.0200; maxStall=0.0000; maxPenetration=0.0050; contactSamples=144; growthEdits=0; sourcePoints=3; colliderShapeCount=2; firstStall=none.
- C: slope_-10; reached=True; dx=4.7846; time=2.5400; avgVx=1.8837; vx=[1.6799,1.9636]; speed=[1.6915,1.9939]; grounded=2.5000; maxAir=0.0400; maxStall=0.0000; maxPenetration=0.0050; contactSamples=126; growthEdits=0; sourcePoints=3; colliderShapeCount=2; firstStall=none.
- C: slope_+20; reached=True; dx=4.8921; time=3.0200; avgVx=1.6199; vx=[1.4109,1.6215]; speed=[1.5015,1.7255]; grounded=3.0200; maxAir=0.0000; maxStall=0.0000; maxPenetration=0.0015; contactSamples=151; growthEdits=0; sourcePoints=3; colliderShapeCount=1; firstStall=none.
- C: slope_-20; reached=True; dx=4.7417; time=2.4000; avgVx=1.9757; vx=[1.8000,2.0833]; speed=[1.8838,2.2170]; grounded=2.3600; maxAir=0.0200; maxStall=0.0000; maxPenetration=0.0070; contactSamples=119; growthEdits=0; sourcePoints=3; colliderShapeCount=2; firstStall=none.
- C: slope_+35; reached=True; dx=4.9637; time=3.4200; avgVx=1.4514; vx=[0.8644,1.4565]; speed=[1.0552,1.7781]; grounded=3.4200; maxAir=0.0000; maxStall=0.0000; maxPenetration=0.0050; contactSamples=177; growthEdits=0; sourcePoints=3; colliderShapeCount=2; firstStall=none.
- C: slope_-35; reached=True; dx=4.6979; time=2.2000; avgVx=2.1354; vx=[1.8000,2.2426]; speed=[1.9428,2.7378]; grounded=2.1600; maxAir=0.0400; maxStall=0.0000; maxPenetration=0.0050; contactSamples=108; growthEdits=0; sourcePoints=3; colliderShapeCount=2; firstStall=none.
- C: connected_flat_up10_down10_flat; reached=True; dx=4.8099; time=2.6800; avgVx=1.7947; vx=[1.4078,1.9693]; speed=[1.5526,1.9996]; grounded=2.6200; maxAir=0.0600; maxStall=0.0000; maxPenetration=0.0060; contactSamples=134; growthEdits=0; sourcePoints=5; colliderShapeCount=4; firstStall=none.
- C: connected_flat_up20_down20_flat; reached=True; dx=4.8209; time=2.7000; avgVx=1.7855; vx=[1.4318,2.1371]; speed=[1.5237,2.2743]; grounded=2.6200; maxAir=0.0800; maxStall=0.0000; maxPenetration=0.0150; contactSamples=134; growthEdits=0; sourcePoints=5; colliderShapeCount=4; firstStall=none.
- C: connected_flat_up35_down35_flat; reached=True; dx=4.8156; time=2.7400; avgVx=1.7575; vx=[0.9545,2.4871]; speed=[1.4745,3.0362]; grounded=2.6000; maxAir=0.1400; maxStall=0.0000; maxPenetration=0.0150; contactSamples=133; growthEdits=0; sourcePoints=5; colliderShapeCount=4; firstStall=none.
- C: dense_collinear_20_300points; reached=True; dx=4.8841; time=3.1000; avgVx=1.5755; vx=[0.8886,1.8000]; speed=[1.1675,1.8483]; grounded=2.5600; maxAir=0.0200; maxStall=0.0000; maxPenetration=0.0103; contactSamples=214; growthEdits=0; sourcePoints=301; colliderShapeCount=152; firstStall=none.
- C: dense_collinear_20_600points; reached=True; dx=4.8926; time=3.2200; avgVx=1.5194; vx=[0.7226,1.8000]; speed=[1.0186,1.8420]; grounded=2.2400; maxAir=0.0200; maxStall=0.0000; maxPenetration=0.0103; contactSamples=340; growthEdits=0; sourcePoints=601; colliderShapeCount=322; firstStall=none.
- C: dense_small_jitter_10; reached=True; dx=4.8182; time=2.9200; avgVx=1.6501; vx=[0.6891,1.8000]; speed=[0.8849,1.8306]; grounded=2.1400; maxAir=0.0400; maxStall=0.0000; maxPenetration=0.0127; contactSamples=223; growthEdits=0; sourcePoints=281; colliderShapeCount=177; firstStall=none.
- C: growing_stroke_actor_on_existing_valid_part; reached=True; dx=4.8405; time=2.8600; avgVx=1.6925; vx=[1.1238,1.8000]; speed=[1.2665,1.8267]; grounded=2.6000; maxAir=0.0400; maxStall=0.0000; maxPenetration=0.0112; contactSamples=159; growthEdits=72; sourcePoints=81; colliderShapeCount=47; firstStall=none.
- C: raised_endpoint_007_control; reached=True; dx=4.8039; time=2.7200; avgVx=1.7662; vx=[0.2045,1.8000]; speed=[0.6067,1.9777]; grounded=2.6400; maxAir=0.0800; maxStall=0.0000; maxPenetration=0.0154; contactSamples=133; growthEdits=0; sourcePoints=2; colliderShapeCount=1; firstStall=none.
- DIAGNOSTIC ONLY: raised_endpoint_007_control deliberately has an upright 0.0700 unit leading edge. Its result is kept separate from continuous slope regression checks.
- C: raised_endpoint_015_control; reached=False; dx=1.1880; time=6.0000; avgVx=0.1980; vx=[0.0000,1.8000]; speed=[0.0000,1.8000]; grounded=6.0000; maxAir=0.0000; maxStall=5.3400; maxPenetration=0.0050; contactSamples=834; growthEdits=0; sourcePoints=2; colliderShapeCount=1; firstStall=at t=0.7600, body=(1.09965, 2.28634), foot=(1.09243, 2.00500, 0.00000), velocity=(0.00000, 0.00000), contacts=p=(1.18800, 2.00750)/n=(0.00000, 1.00000)/separation=-0.0050;p=(1.28178, 2.10057)/n=(-1.00000, 0.00000)/separation=-0.0036;p=(1.28178, 2.15000)/n=(-1.00000, 0.00000)/separation=-0.0036.
- DIAGNOSTIC ONLY: raised_endpoint_015_control deliberately has an upright 0.1500 unit leading edge. Its result is kept separate from continuous slope regression checks.
- Time.timeScale restored to its pre-run value: 1.0000.

## Collected failures

- A: Platform 0001 - platform_5 is explicitly above overlapping background background1_0 (platform=Default/0, background=Default/0).
- A: Platform 0002 - platform_3 is explicitly above overlapping background background1_0 (platform=Default/0, background=Default/0).
- A: Platform 0003 - platform_2 is explicitly above overlapping background background1_0 (platform=Default/0, background=Default/0).
- A: Platform 0004 - platform_4 is explicitly above overlapping background background1_0 (platform=Default/0, background=Default/0).
- A: actual preparation render visibly contains runtime platforms with user background enabled (pixel ratio=0.0000).

Baseline mode retains every failed assertion and exits 0 after completing the probe. Strict mode exits 1 for any collected failure. Endpoint controls are diagnostic. Airborne duration and horizontal/total speed are reported separately, so downhill ballistic motion is not mislabeled as a collider stall.
