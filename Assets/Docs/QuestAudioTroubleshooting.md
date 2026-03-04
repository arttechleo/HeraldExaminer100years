# Quest Microphone Audio Troubleshooting Checklist

## Inspector toggles (in order)

1. **microphonePlayBeepForDebug** (PersistentDiscoveryManager) OR **playGeneratedBeepInstead** (MicrophoneInteractable on prefab)
   - Turn **ON** first. Pinch a mic.
   - If beep is audible → audio output works; problem is clip/import.
   - If beep is not audible → fix listener / audio routing.

2. **force2DForDebug** (MicrophoneInteractable)
   - Turn **ON** to bypass 3D attenuation (use with imported clips).
   - Turn **OFF** once clips play, for normal spatial audio.

3. **playGeneratedBeepInstead**
   - Turn **OFF** when testing imported .wav clips.

## Diagnostic overlay (DEVELOPMENT_BUILD)

- LastHitMicID vs LastTriggeredMicID should match.
- ListenerCount, ActiveListenerName: expect 1 listener, name like CenterEyeAnchor.
- LastTriggerSuccess=true, Reason="Play() called" or "Beep played (2D)".
- ClipLoadState: Loaded.
- IsPlaying: true after pinch.

## If beep audible but clips not

1. Run **Tools > Temporal Echo > Validate Audio Clips**.
2. Click **Fix** for each clip.
3. Rebuild / re-enter Play Mode.
4. Ensure force2DForDebug is ON while testing.

## Conclusion

- **A)** Beep not audible → fix listener (ensure single AudioListener on CenterEyeAnchor).
- **B)** Beep audible, clips not → fix import settings, confirm ClipLoadState Loaded.
