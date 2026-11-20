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

The XR Interaction Simulator sample is imported at
`Assets/Samples/XR Interaction Toolkit/3.3.2/XR Interaction Simulator`, which lets you drive the
scenes with mouse and keyboard instead of a headset. **Automatic instantiation is currently
turned off.** To use it, either tick *Use XR Interaction Simulator in scenes* under
`Edit > Project Settings > XR Plug-in Management > XR Interaction Toolkit` (and point it at that
prefab), or drop the prefab into a scene by hand.

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
| `07_ThrowAssist` | Throw assistance: near-miss throws at a basket are snapped onto target, toggled by a button in the scene |

### Throw assist

`07_ThrowAssist` has a ball dispenser, a basket and a button that switches assistance on and off.

With assistance on, a throw whose release direction is within a tolerance (default 14°) of a
direction that *would* have landed is rotated onto that solution. The player's release **speed is
preserved exactly** — only the aim is corrected — so a throw that is too weak to reach the basket
stays too weak. Throws further off than the tolerance are left completely alone.

Two arcs always reach a reachable target — a flat one and a lobbed one — and the solver keeps only
those that arrive falling at least `minDescentAngle` (35° for the basket) below horizontal. This is
not cosmetic: a flat arc passes through the centre of the opening on paper but reaches rim height
*before* the basket and clips the near rim. Requiring a real lob is what makes assisted throws
actually drop in, and it means a flat throw correctly gets no assist — you cannot score flat.

All of the logic lives on the thrown object, in `ThrowAssistant`:

- XRI ships an `IXRAimAssist` hook, but `XRGrabInteractable` resolves it with
  `GetComponentInParent` on the *interactor*, so it has to live on the rig. This implementation
  deliberately does not use it, leaving the rig free of assist components.
- `XRGrabInteractable` applies the throw velocity in **LateUpdate**, after `selectExited` fires, so
  the release velocity cannot be read in the release handler. The release arms the component and
  the correction runs on the next physics step, where the velocity is still the release velocity
  and gravity has not been integrated into it yet.
- The solver assumes plain ballistic motion, so the ball's Rigidbody keeps `linearDamping` at 0.
  With drag, an assisted throw falls short.

`ThrowAssistTarget` on the basket holds only the aim point and the on/off flag, and registers
itself in a static list so balls spawned at runtime find it with nothing wired up.

> The toggle button is driven by **select, not by a physical press**: the hands carry no solid
> colliders, only a trigger volume for grabbing, so nothing in the rig can push a cap. Touch the
> button and squeeze grip. It also carries a kinematic Rigidbody, because Unity sends no trigger
> messages between a static trigger and a static collider.

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
        ├── Interactions/       AirDrawPen, SocketItemSpawner,
        │                       ThrowAssistant, ThrowAssistTarget, AssistToggleButton
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
