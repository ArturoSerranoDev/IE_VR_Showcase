# IE VR Showcase

A set of small, incremental Unity scenes, each demonstrating one VR interaction feature. Every
scene is built on the same shared XR rig so the scenes themselves stay thin.

## Requirements

| | |
|---|---|
| Unity | 6000.3.12f1 (Unity 6.3 LTS) |
| Render pipeline | URP 17.3 |
| Interaction | XR Interaction Toolkit 3.3.2 |
| XR provider | OpenXR 1.19 (Standalone / PC VR) |

Android build support is **not** configured — see [Platform notes](#platform-notes).

## Running it

Open any scene under `Assets/_Project/Scenes/` and press Play.

You do **not** need a headset. The XR Interaction Simulator is imported and set to
auto-instantiate in the editor only, so every scene gets mouse-and-keyboard control of the
simulated headset and controllers with no per-scene setup.

> **Note:** the simulator starts the simulated headset at the tracking origin, and the rig uses
> Floor tracking mode, so play mode begins with the camera at floor height. Use the simulator's
> translate controls to raise the view.

## Scenes

| Scene | Demonstrates |
|---|---|
| `01_GrabTypes` | The three `XRGrabInteractable` movement types: Kinematic, Velocity Tracking, Instantaneous |
| `02_Sockets` | A dispenser socket that respawns its item when you take one, plus two receiver sockets |
| `03_Drivers_Lever` | A `HingeJoint` lever, grabbable only at its end point, constrained by real physics |
| `04_AirDrawing` | Grab the pencil and pull the trigger to draw `LineRenderer` strokes in mid-air |
| `05_Climbing` | Two climbing walls: one with discrete holds, one grabbable across its whole surface |
| `06_TwoHandInteractables` | A hammer, a staff, and a gun with two distinct grab points |

## The shared XR rig

`Assets/_Project/Prefabs/XR/XR Rig.prefab` is a complete, self-contained setup — drop it into a
scene and nothing else needs wiring.

```
XR Rig                      XROrigin, XRInputActionsEnabler,
                            LocomotionMediator, XRBodyTransformer, ClimbProvider
├── XR Interaction Manager
└── Camera Offset
    ├── Main Camera         Camera, AudioListener, TrackedPoseDriver (head)
    ├── Left Hand           TrackedPoseDriver, SphereCollider (trigger),
    │   ├── Attach          ShowcaseDirectInteractor
    │   └── Hand Visual     palm + 4 fingers + thumb, no Animator
    └── Right Hand          (mirrored)
```

The hands are deliberately **not** posed or animated. `ShowcaseDirectInteractor` hides the hand
visual while something is held, so a static open hand never clips through a grabbed object.

`XRInputActionsEnabler` enables the input action asset at runtime. Without it nothing tracks and
no grab fires, because Input System actions are disabled by default. It is the project-local
equivalent of the `InputActionManager` inside the XRI Starter Assets sample, kept here so the rig
does not depend on importing that sample.

## Layout

```
Assets/_Project/
├── Materials/                  generated URP materials
├── Prefabs/
│   ├── Interactables/          shared grabbables
│   └── XR/                     the shared rig
├── Scenes/                     one scene per feature
├── Settings/                   XRI Default Input Actions
└── _Scripts/
    ├── Editor/                 generators for the rig and the scenes
    └── Runtime/
        ├── Interactions/       AirDrawPen, SocketItemSpawner
        └── XR/                 ShowcaseDirectInteractor, XRInputActionsEnabler
```

## Regenerating

The rig and the scenes are produced by editor scripts, under the `IE Showcase` menu:

- `IE Showcase > XR > Build XR Rig Prefab`
- `IE Showcase > Scenes > Build All Showcase Scenes` (or one scene at a time)

**Re-running a generator overwrites its output.** Once you start hand-tuning a scene or the rig
prefab, treat the asset as the source of truth and stop regenerating it.

## Platform notes

Configured for **Standalone / PC VR** only. OpenXR is enabled for the Standalone build target
with the Oculus Touch, Meta Quest Touch Plus, Meta Quest Touch Pro and KHR Simple interaction
profiles.

Quest standalone builds need Android Build Support (with SDK and NDK) added through Unity Hub
first; the XR settings for Android are not set up. Until then the project runs on PC VR, including
a Quest over Link.

The Oculus XR Plugin package is still installed but intentionally **not** assigned as a loader, so
switching back to it is a one-dropdown change.
