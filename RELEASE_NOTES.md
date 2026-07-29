# KathanaBot 1.0.89

## What changed

- **Fixed runaway zoom while dragging/resizing in the Snapshot preview:** resizing a region used to recompute the zoomed viewport from the region's own (changing) size on every frame, which fed back into itself and made the preview zoom in/out wildly with tiny mouse movements. The viewport is now frozen for the whole drag gesture and only re-fits once you release - dragging and resizing are smooth and predictable now.

## Recent change history - last 5

1. **Fixed runaway zoom during Snapshot drag/resize:** the preview viewport no longer rescales itself mid-drag, eliminating the feedback loop that made resizing feel wildly oversensitive.
2. **Drag/resize regions from the Snapshot preview:** move or resize the selected region directly on the zoomed live preview, synced with the Calibration Regions grid in real time.
3. **Live, zoomable region preview:** Snapshot panel is real-time now (200ms refresh), with an optional zoomed-in view of the selected calibration region plus its coordinates.
4. **Notification body cleanup + Stats Interval 0=off:** dropped the redundant Character/Party lines from Stats and On-Demand notification bodies, and Stats Interval (min) now allows 0-9999, with 0 disabling periodic stats notifications.
5. **HP alert speed-up + character name in every notification title, fixed name detection cadence:** 30s/50-sample death confirmation (was 60s/100), all alert titles now show the character's name, and character-name OCR now refreshes every 5s instead of every 30 minutes.
