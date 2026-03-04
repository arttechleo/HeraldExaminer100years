# Overlay persistence and grace behavior

Overlays stay visible for a short time after the object is no longer detected. This reduces flicker when the user moves their head or detection briefly drops.

## Per-category grace seconds

Each category has its own base grace time before an overlay is despawned:

- **screenGraceSeconds** (default **1.5 s**) – Typewriter HIGH/LOW overlays
- **bookGraceSeconds** (default **1.5 s**) – Newspaper overlays  
- **humanGraceSeconds** (default **1.2 s**) – Hat overlays (slightly shorter if desired)

After a detection stops being reported, the overlay remains for at least this many seconds before it is hidden. This prevents overlays from popping off when the detector misses a frame or two.

## Camera-move extra grace

When the camera has moved a lot since the overlay was last seen, an **extra** grace period is added so that turning your head doesn’t immediately remove overlays.

- **extraGraceOnCameraMoveSeconds** (default **0.75 s**) – Added to the base grace when “camera moved recently”.
- **cameraMoveDistanceThreshold** (default **0.25 m**) – If the camera position has moved at least this much since the overlay’s last seen time, we consider it “moved recently”.
- **cameraYawThresholdDegrees** (default **25°**) – If the camera yaw has changed by at least this much since last seen, we also consider it “moved recently”.

**Effective grace** = base grace (per category) + (extra grace if camera moved beyond threshold).

So when you turn your head quickly, overlays get a bit more time before despawning, which avoids pop-off during head motion. Flicker prevention is unchanged: we still use the same pooling and key-based tracking, and we don’t respawn/despawn rapidly.
