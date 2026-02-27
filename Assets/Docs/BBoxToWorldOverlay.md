# BBox to World Overlay Pipeline

This doc explains how a 2D detection bounding box becomes a 3D overlay transform (position, rotation, scale) in the scene, and what each tuning setting does.

## 1. What the bbox is

- The **bbox** (bounding box) is a 2D rectangle in the **camera image** from the passthrough camera.
- It comes from the object-detection model (e.g. YOLO-style): `(xmin, ymin, xmax, ymax)` in **pixel coordinates**.
  - Origin: **top-left** of the image.
  - `x` increases right, `y` increases **down**.
- The detection also has a **label** (e.g. "person", "laptop") and a **confidence** score (0–1).

## 2. Picking a point inside the bbox

We don’t use the full bbox for placement; we pick **one 2D point** and cast a ray through it:

- **Screen / Book:** use the **center** of the bbox.
  - `px = (xmin + xmax) / 2`, `py = (ymin + ymax) / 2`.
- **Human (hat):** use **top-center** of the bbox (approximate head).
  - `px = (xmin + xmax) / 2`, `py = ymin` (top edge), then add an optional **screen-space Y offset** (e.g. a bit above the top) so the ray aims at the head.

So: **bbox → one (px, py) in pixels** → that point is used for the rest of the pipeline.

## 3. Screen point and viewport

- We convert pixel `(px, py)` to **normalized viewport** for the passthrough camera:
  - `normX = px / textureWidth`, `normY = py / textureHeight`.
- Camera APIs usually use **viewport** with **Y up** (bottom-left origin), so we flip Y:
  - `viewportY = 1 - normY`.
- We then get a **world-space ray** from the camera through that viewport point (using the camera’s intrinsics / pose). So:
  - **bbox center (or top-center) → normalized (normX, normY) → viewport (normX, 1-normY) → ray from camera.**

## 4. Ray → world position (depth)

- We cast the ray into the scene to get a **world position**.
- **Preferred:** sample the **depth texture** (scene mesh / depth buffer) at the same viewport point to get **depth** in front of the camera, then:
  - `worldPos = ray.origin + ray.direction * depth`.
- **Fallback:** if depth is invalid (no hit, out of range, etc.), we use a **fixed depth** (e.g. 1.8 m):
  - `worldPos = ray.origin + ray.direction * fallbackDepth`.
- So: **ray + depth → single 3D point in world space.** That’s the “anchor” position before any tuning offsets.

## 5. Why we don’t use bbox rotation

- The detection model may output an angle or the bbox may be slightly rotated; in practice this is **noisy** and changes frame-to-frame.
- Using it for overlay rotation causes **jitter and misalignment**.
- So we **ignore bbox rotation entirely** and build rotation from stable rules (upright + yaw only).

## 6. How we build rotation (no bbox angle)

- **Base orientation** is chosen per category with toggles:
  - **Lock pitch and roll:** object stays **upright** (world up = `Vector3.up`), only **yaw** varies.
  - **Face camera (yaw only):** yaw is set so the object faces the camera (around world up).
  - **Use camera forward yaw:** yaw is set from the camera’s forward direction (same for all overlays in view).
- So: **rotation = upright + chosen yaw**, then **rotation offset (Euler)** is applied on top. No bbox rotation is used.

## 7. Offsets and tuning (what each setting does)

Applied **after** we have the anchor position and base rotation:

- **worldPositionOffset**  
  - **Space:** world.  
  - **When:** after anchor position is computed (ray + depth), before any rotation.  
  - **Effect:** `anchorPos += worldPositionOffset`.  
  - Use to shift the overlay in world axes (e.g. move up/down/forward) without depending on its rotation.

- **localPositionOffset**  
  - **Space:** local (after base rotation is set).  
  - **When:** after rotation (including rotation offset) is applied.  
  - **Effect:** `finalPos += rotation * localPositionOffset`.  
  - Use to shift the overlay along its own axes (e.g. forward in “object space”).

- **rotationOffsetEuler**  
  - **Space:** Euler degrees, applied in the object’s local space after the base rotation.  
  - **Effect:** `finalRotation = baseRotation * Quaternion.Euler(rotationOffsetEuler)`.  
  - Use for small tweaks (tilt, nudge angle) without touching the base “upright + yaw” logic.

- **localScaleMultiplier**  
  - **Effect:** `finalScale = prefabOriginalScale * localScaleMultiplier` (component-wise).  
  - Use to make overlays bigger/smaller per category without changing the prefab.

So in order: **anchor position → + worldPositionOffset → set base rotation (upright + yaw) → apply rotationOffsetEuler → final position += rotation * localPositionOffset → final scale = prefabScale * localScaleMultiplier.**

## 8. Resolve cadence and smoothing

- **Resolve** = recomputing the anchor (bbox → point → ray → depth → world pose + tuning).
- We do **not** resolve every frame. We resolve at a **fixed rate** (e.g. **10 Hz**): only when `time >= nextResolveTime` per overlay.
- **Between** resolves we keep the **last resolved target** (position, rotation, scale) and every frame **interpolate** the current transform toward that target:
  - `pos = Lerp(current, target, 1 - exp(-posSmoothing * dt))`
  - `rot = Slerp(current, target, 1 - exp(-rotSmoothing * dt))`
- So:
  - **Resolve cadence** reduces jitter from noisy bbox/depth by not re-anchoring every frame.
  - **Smoothing** makes motion stable and avoids snapping; the overlay “catches up” to the target over a short time.

## 9. Summary flow

1. **Bbox** (2D rectangle in camera image, pixels).
2. **Point:** center (or for human, top-center + offset) → **(px, py)**.
3. **Viewport:** **(normX, 1-normY)**.
4. **Ray:** from camera through that viewport point.
5. **Depth:** sample depth texture at that point, or use fixed depth.
6. **Anchor position:** ray origin + ray direction × depth (+ human head world-up offset).
7. **Tuning:** add worldPositionOffset → set rotation (upright + yaw, no bbox rotation) → apply rotationOffsetEuler → add localPositionOffset → scale = prefabScale × localScaleMultiplier.
8. **Cadence:** resolve at e.g. 10 Hz; every frame, smooth transform toward last resolved target.

This pipeline is implemented in **DetectionPrefabReplacer** (and its resolve/smoothing logic); the tuning struct **CategoryTuning** exposes the offsets and toggles so you can adjust placement per category without recompiling.
