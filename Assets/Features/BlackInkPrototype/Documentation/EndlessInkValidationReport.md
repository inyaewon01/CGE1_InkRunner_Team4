# Endless Ink Runner — Unity validation

Unity: 6000.3.23f1
Result: PASS

- PASS: Dedicated scene starts in endless mode with all required references
- PASS: 40 px character body and 25 px ink map to 0.40 / 0.25 world units at 100 px per unit
- PASS: Only the prototype variant uses the 40 / 210 character scale
- PASS: Current Game view actually projects the body to 40 px and the ink to 25 px
- PASS: Runner begins near the left screen edge (8 percent viewport X)
- PASS: All six supplied platforms and both supplied obstacle sprites are linked
- PASS: Initial random platforms cover the visible area with bounded live objects
- PASS: Ink is stored in stationary world space independently of the runner
- PASS: A simple click or stationary pointer creates no ink and has no cost
- PASS: A fast 3.6-unit stroke creates continuous ink for exactly 36 ink
- PASS: Ink has a continuous 25 px collision body extending down from the pointer path
- PASS: Generated ink keeps the brush material and feathered mesh
- PASS: Fast and slow sampling charge the same completed world path length
- PASS: The final ink segment is clipped to the affordable 0.7 units
- PASS: Refill restores ink while preserving existing platforms and the runner pose
- PASS: Reset removes generated ink and restores the ink budget
- PASS: Actual runner motion advances the following camera (state=Running, body=(1.54, 0.29), velocity=(1.80, -0.11), cameraX=4.320, initialX=2.776, simulated=0.803s)
- PASS: Holding the pointer still while the camera scrolls creates no ink and has no cost
- PASS: Camera scrolling leaves painted ink position, texture UVs and collider geometry unchanged
- PASS: Actual Physics2D runner crosses random platform seams for 120 simulated seconds without completing or falling
- PASS: The map continuously generates ahead beyond 200 world units of actual travel
- PASS: The long run visibly generates every platform_1 through platform_6 and both obstacle variants
- PASS: Offscreen world recycling bounds live platforms / obstacles (observed 11 / 7)
- PASS: Intermittent obstacle sprites have no Collider2D or Rigidbody2D hit detection
- PASS: Runner remains near the left screen edge while the world scrolls
- PASS: Saved real 1920 x 1080 Unity URP renders at 10 and 60 simulated seconds
- PASS: Painted ink entirely behind the camera is cleaned up during the long run
- PASS: Reset after a long run restores the starting character and camera positions
- PASS: Reset restarts preparation and rebuilds only the initial visible world
- PASS: Falling still enters the recoverable failed state in endless mode
- PASS: Reset from failure restores the live physics body and its matching rendered collider
- PASS: No runtime errors, missing references, shader errors or exceptions during validation

