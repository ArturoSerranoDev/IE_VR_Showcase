using System.Collections.Generic;
using IE.VRShowcase.XR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;
using TrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;

namespace IE.VRShowcase.EditorTools
{
    /// <summary>
    /// Generates the shared "XR Rig" prefab used by every showcase scene:
    /// an XR Origin with a head camera and two non-animated hands, each carrying
    /// a <see cref="ShowcaseDirectInteractor"/> for near-field (direct) grabbing,
    /// plus the locomotion plumbing the climbing scene needs.
    /// </summary>
    static class XRRigPrefabBuilder
    {
        const string k_ActionsPath = "Assets/_Project/Settings/XRI Default Input Actions.inputactions";
        const string k_PrefabPath = "Assets/_Project/Prefabs/XR/XR Rig.prefab";
        const string k_HandMaterialPath = "Assets/_Project/Materials/M_Hand.mat";

        [MenuItem("IE Showcase/XR/Build XR Rig Prefab")]
        public static void Build()
        {
            var actionAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(k_ActionsPath);
            if (actionAsset == null)
            {
                Debug.LogError("[XRRigPrefabBuilder] Input action asset not found at " + k_ActionsPath);
                return;
            }

            var subAssets = AssetDatabase.LoadAllAssetsAtPath(k_ActionsPath);
            var handMaterial = CreateHandMaterial();

            // ---------- Root: XR Origin ----------
            var root = new GameObject("XR Rig");
            var origin = root.AddComponent<XROrigin>();

            var interactionManagerGo = new GameObject("XR Interaction Manager");
            interactionManagerGo.transform.SetParent(root.transform, false);
            var interactionManager = interactionManagerGo.AddComponent<XRInteractionManager>();

            var cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(root.transform, false);

            // ---------- Head ----------
            var cameraGo = new GameObject("Main Camera");
            cameraGo.transform.SetParent(cameraOffset.transform, false);
            cameraGo.tag = "MainCamera";
            var cam = cameraGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 1000f;
            cameraGo.AddComponent<AudioListener>();

            var headDriver = cameraGo.AddComponent<TrackedPoseDriver>();
            headDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            headDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            headDriver.positionInput = Prop(subAssets, "XRI Head", "Position");
            headDriver.rotationInput = Prop(subAssets, "XRI Head", "Rotation");
            headDriver.trackingStateInput = Prop(subAssets, "XRI Head", "Tracking State");

            origin.Origin = root;
            origin.Camera = cam;
            origin.CameraFloorOffsetObject = cameraOffset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            // ---------- Locomotion ----------
            // The mediator pulls in XRBodyTransformer via RequireComponent. The climb provider is
            // inert until a ClimbInteractable is grabbed, so carrying it in every scene is free.
            var mediator = root.AddComponent<LocomotionMediator>();

            // LocomotionMediator.xrOrigin only forwards to the body transformer once Awake has
            // run, so at author time the origin has to be set on the transformer itself.
            var bodyTransformer = root.GetComponent<XRBodyTransformer>();
            if (bodyTransformer != null)
                bodyTransformer.xrOrigin = origin;
            else
                Debug.LogWarning("[XRRigPrefabBuilder] XRBodyTransformer was not added alongside LocomotionMediator.");

            var climbProvider = root.AddComponent<ClimbProvider>();
            climbProvider.mediator = mediator;
            climbProvider.climbSettings = new ClimbSettingsDatumProperty(new ClimbSettings());

            // ---------- Hands ----------
            BuildHand(cameraOffset.transform, true, subAssets, handMaterial, interactionManager);
            BuildHand(cameraOffset.transform, false, subAssets, handMaterial, interactionManager);

            // ---------- Input actions have to be enabled at runtime ----------
            var enabler = root.AddComponent<XRInputActionsEnabler>();
            enabler.actionAssets = new List<InputActionAsset> { actionAsset };

            // ---------- Save ----------
            var saved = PrefabUtility.SaveAsPrefabAsset(root, k_PrefabPath);
            Object.DestroyImmediate(root);

            if (saved != null)
                Debug.Log("[XRRigPrefabBuilder] Saved prefab to " + k_PrefabPath);
            else
                Debug.LogError("[XRRigPrefabBuilder] Failed to save prefab to " + k_PrefabPath);
        }

        static void BuildHand(Transform parent, bool isLeft, Object[] subAssets, Material handMaterial,
            XRInteractionManager interactionManager)
        {
            var side = isLeft ? "Left" : "Right";
            var mirror = isLeft ? -1f : 1f;

            var hand = new GameObject(side + " Hand");
            hand.transform.SetParent(parent, false);

            // Tracked pose of the physical controller.
            var driver = hand.AddComponent<TrackedPoseDriver>();
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.positionInput = Prop(subAssets, "XRI " + side, "Position");
            driver.rotationInput = Prop(subAssets, "XRI " + side, "Rotation");
            driver.trackingStateInput = Prop(subAssets, "XRI " + side, "Tracking State");

            // Grab volume. A trigger collider is what the direct interactor uses to
            // discover interactables; the interactables bring the Rigidbody.
            var grabVolume = hand.AddComponent<SphereCollider>();
            grabVolume.isTrigger = true;
            grabVolume.radius = 0.08f;
            grabVolume.center = new Vector3(0f, 0f, 0.02f);

            // Where grabbed objects are held.
            var attach = new GameObject("Attach");
            attach.transform.SetParent(hand.transform, false);
            attach.transform.localPosition = new Vector3(0f, -0.01f, 0.02f);

            var interactor = hand.AddComponent<ShowcaseDirectInteractor>();
            interactor.interactionManager = interactionManager;
            interactor.attachTransform = attach.transform;
            interactor.handedness = isLeft ? InteractorHandedness.Left : InteractorHandedness.Right;

            var interactionMap = "XRI " + side + " Interaction";
            BindButton(interactor.selectInput, subAssets, interactionMap, "Select", "Select Value");
            BindButton(interactor.activateInput, subAssets, interactionMap, "Activate", "Activate Value");

            // Assigned explicitly so the prefab does not rely on the Awake-time name lookup.
            interactor.handVisual = BuildHandVisual(hand.transform, mirror, handMaterial);
        }

        /// <summary>
        /// Builds a static, non-animated hand out of primitives: a palm slab,
        /// four fingers and a thumb. No Animator, no skinned rig - purely a visual.
        /// Tweak the "Hand Visual" transform to re-seat the hand on the controller.
        /// </summary>
        /// <returns>The root of the hand visual, so the interactor can hide it while grabbing.</returns>
        static GameObject BuildHandVisual(Transform parent, float mirror, Material material)
        {
            var visual = new GameObject("Hand Visual");
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, -0.012f, 0.012f);
            // Tilt forward, then roll the palm inward so it reads as a relaxed grip.
            visual.transform.localRotation = Quaternion.Euler(-25f, 0f, -55f * mirror);

            const float palmFrontZ = 0.0565f;

            AddPart(visual.transform, PrimitiveType.Cube, "Palm",
                new Vector3(0f, 0f, 0.015f), Quaternion.identity,
                new Vector3(0.075f, 0.024f, 0.085f), material);

            // Index -> little. For a right hand the thumb side is -X.
            var fingerX = new[] { -0.027f, -0.009f, 0.009f, 0.027f };
            var fingerHalfLength = new[] { 0.030f, 0.032f, 0.029f, 0.024f };
            var fingerNames = new[] { "Index Finger", "Middle Finger", "Ring Finger", "Little Finger" };
            var fingerDir = new Vector3(0f, -0.18f, 0.98f).normalized;

            for (var i = 0; i < fingerX.Length; i++)
            {
                var knuckle = new Vector3(fingerX[i] * mirror, -0.003f, palmFrontZ);
                AddPart(visual.transform, PrimitiveType.Capsule, fingerNames[i],
                    knuckle + fingerDir * fingerHalfLength[i],
                    Quaternion.FromToRotation(Vector3.up, fingerDir),
                    new Vector3(0.016f, fingerHalfLength[i], 0.016f), material);
            }

            var thumbDir = new Vector3(-0.60f * mirror, 0.25f, 0.76f).normalized;
            var thumbBase = new Vector3(-0.032f * mirror, 0.002f, 0.012f);
            AddPart(visual.transform, PrimitiveType.Capsule, "Thumb",
                thumbBase + thumbDir * 0.026f,
                Quaternion.FromToRotation(Vector3.up, thumbDir),
                new Vector3(0.018f, 0.026f, 0.018f), material);

            return visual;
        }

        static void AddPart(Transform parent, PrimitiveType type, string name,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;

            // Primitives ship with a solid collider; the hand must not collide with the world.
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;

            var meshRenderer = go.GetComponent<Renderer>();
            if (meshRenderer != null)
                meshRenderer.sharedMaterial = material;
        }

        static void BindButton(XRInputButtonReader reader, Object[] subAssets,
            string mapName, string performedAction, string valueAction)
        {
            if (reader == null)
            {
                Debug.LogWarning("[XRRigPrefabBuilder] Null input reader for " + mapName + "/" + performedAction);
                return;
            }

            reader.inputSourceMode = XRInputButtonReader.InputSourceMode.InputActionReference;
            reader.inputActionReferencePerformed = Reference(subAssets, mapName, performedAction);
            reader.inputActionReferenceValue = Reference(subAssets, mapName, valueAction);
        }

        static InputActionProperty Prop(Object[] subAssets, string mapName, string actionName)
        {
            return new InputActionProperty(Reference(subAssets, mapName, actionName));
        }

        static InputActionReference Reference(Object[] subAssets, string mapName, string actionName)
        {
            foreach (var asset in subAssets)
            {
                var reference = asset as InputActionReference;
                if (reference == null || reference.action == null || reference.action.actionMap == null)
                    continue;

                if (reference.action.actionMap.name == mapName && reference.action.name == actionName)
                    return reference;
            }

            Debug.LogWarning("[XRRigPrefabBuilder] Action reference not found: " + mapName + "/" + actionName);
            return null;
        }

        static Material CreateHandMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(k_HandMaterialPath);
            if (existing != null)
                return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[XRRigPrefabBuilder] URP Lit shader not found.");
                return null;
            }

            var material = new Material(shader) { name = "M_Hand" };
            material.SetColor("_BaseColor", new Color(0.78f, 0.76f, 0.80f, 1f));
            material.SetFloat("_Smoothness", 0.2f);
            material.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(material, k_HandMaterialPath);
            return material;
        }
    }
}
