# Black Ink Prototype — Unity validation

Unity: 6000.3.23f1
Result: PASS

- PASS: Test scene starts with all required references
- PASS: Measured H=2.10, thickness=0.75H, spacing=0.025H
- PASS: 100 ink at 10/unit affords exactly 10 world units
- PASS: Budget clips the final segment to remaining affordable length
- PASS: Collision exclusion checks segment interior and full ink thickness
- PASS: Vertical walls are refused
- PASS: Stationary mouse / simple click generates no platform and consumes no ink
- PASS: Fast sweep creates a continuous 3.6-unit bridge for 36 ink (actual cost=36.000, strokes=1, screen=640x480, camera=(x:0.00, y:0.00, width:640.00, height:480.00))
- PASS: Live generated stroke has an opaque-body mesh and an enabled continuous collider
- PASS: Mouse path is the collider top; full thickness extends downwards
- PASS: Fast and slow event sampling charges the same completed world path length
- PASS: New strokes preserve old platforms by default and do not auto-connect
- PASS: Replacement option does not erase old roads on a simple click
- PASS: Replacement erases visuals and collision only on first successful segment
- PASS: Ink exhaustion creates only the affordable last section and never goes negative
- PASS: Zero ink keeps existing collision and does not fail the trial
- PASS: Refill changes only ink and ends the held stroke
- PASS: Reset clears all generated objects/colliders and restores the preparation state
- PASS: Click begun on UI remains blocked until mouse release
- PASS: Fast sweep cuts around a forbidden area touched by the thick body, with no bridging across it
- PASS: Storage cap safely stops generation while retaining existing roads
- PASS: Reverse-direction strokes generate the same continuous surface
- PASS: Focus loss terminates input and prevents a long reconnecting segment
- PASS: Captured real Unity render for gauge 100%
- PASS: Captured real Unity render for gauge 50%
- PASS: Captured real Unity render for gauge 0%
- PASS: Actual Physics2D runner crosses terrain seams and gentle up/down slopes on the ink bridge
- PASS: Reset after a completed crossing restores both the physics pose and rendered collider immediately
- PASS: Runner stands on a valid section while the same mouse stroke is still held open (state=Running, body=(0.61, 1.08, 0.00), length=1.600)
- PASS: Extending a live stroke while running carries the character to the arrival platform
- PASS: Actual gravity causes an unbridged gap to fail; Reset can restart
- PASS: No runtime errors, missing references, shader errors or exceptions during validation



Editor environment observations (outside prototype runtime):
Unity Editor search indexing: ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection.
Parameter name: index