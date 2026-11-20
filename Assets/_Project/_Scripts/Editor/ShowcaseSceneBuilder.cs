using System.Collections.Generic;
using IE.VRShowcase.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace IE.VRShowcase.EditorTools
{
    /// <summary>
    /// Generates the showcase scenes. Every scene shares the same base setup - a floor, a cube
    /// standing in for a table, lighting and an instance of the shared XR Rig prefab - and then
    /// adds the interactables for the one feature it demonstrates.
    /// </summary>
    /// <remarks>
    /// Re-running a build overwrites that scene, so treat the generated scenes as the source of
    /// truth once you start hand-tuning them.
    /// </remarks>
    static class ShowcaseSceneBuilder
    {
        const string k_ScenesFolder = "Assets/_Project/Scenes/";
        const string k_RigPrefabPath = "Assets/_Project/Prefabs/XR/XR Rig.prefab";
        const string k_CubePrefabPath = "Assets/_Project/Prefabs/Interactables/Cube Interactable.prefab";
        const string k_MaterialsFolder = "Assets/_Project/Materials/";

        /// <summary>Height of the table top, i.e. where interactables are placed.</summary>
        const float k_TableTopY = 0.75f;

        /// <summary>How far in front of the rig the table sits.</summary>
        const float k_TableZ = 0.62f;

        static readonly string[] k_SceneNames =
        {
            "01_GrabTypes",
            "02_Sockets",
            "03_Drivers_Lever",
            "04_AirDrawing",
            "05_Climbing",
            "06_TwoHandInteractables",
        };

        // ------------------------------------------------------------------ menu

        [MenuItem("IE Showcase/Scenes/Build All Showcase Scenes")]
        public static void BuildAll()
        {
            EnsureCubeInteractablePrefab();

            BuildGrabTypesScene();
            BuildSocketsScene();
            BuildLeverScene();
            BuildAirDrawingScene();
            BuildClimbingScene();
            BuildTwoHandScene();

            AddScenesToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ShowcaseSceneBuilder] Built " + k_SceneNames.Length + " showcase scenes.");
        }

        // ------------------------------------------------------- scene 1: grab types

        [MenuItem("IE Showcase/Scenes/01 Grab Types")]
        public static void BuildGrabTypesScene()
        {
            var ctx = CreateBaseScene("01_GrabTypes");

            var specs = new[]
            {
                new { Name = "Kinematic", Type = XRBaseInteractable.MovementType.Kinematic,
                      Color = new Color(0.25f, 0.5f, 0.9f), X = -0.42f },
                new { Name = "Velocity Tracking", Type = XRBaseInteractable.MovementType.VelocityTracking,
                      Color = new Color(0.3f, 0.75f, 0.4f), X = 0f },
                new { Name = "Instantaneous", Type = XRBaseInteractable.MovementType.Instantaneous,
                      Color = new Color(0.95f, 0.55f, 0.2f), X = 0.42f },
            };

            const float size = 0.12f;
            foreach (var spec in specs)
            {
                var cube = CreatePrimitive(PrimitiveType.Cube, spec.Name + " Cube", ctx.content,
                    new Vector3(spec.X, k_TableTopY + size * 0.5f, k_TableZ),
                    Quaternion.identity, Vector3.one * size,
                    GetOrCreateMaterial("M_Grab_" + spec.Name.Replace(" ", ""), spec.Color));

                var body = cube.AddComponent<Rigidbody>();
                body.mass = 0.5f;

                var grab = cube.AddComponent<XRGrabInteractable>();
                grab.interactionManager = ctx.interactionManager;
                grab.movementType = spec.Type;
                grab.useDynamicAttach = true;

                CreateLabel(ctx.content, spec.Name,
                    new Vector3(spec.X, k_TableTopY + 0.26f, k_TableZ));
            }

            CreateTitle(ctx.content, "Grab Movement Types");
            SaveScene(ctx.scene, "01_GrabTypes");
        }

        // --------------------------------------------------------- scene 2: sockets

        [MenuItem("IE Showcase/Scenes/02 Sockets")]
        public static void BuildSocketsScene()
        {
            EnsureCubeInteractablePrefab();

            var ctx = CreateBaseScene("02_Sockets");
            var cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_CubePrefabPath);

            var spawnParent = new GameObject("Spawned Cubes");
            spawnParent.transform.SetParent(ctx.content, false);

            // The dispenser: take the cube out and a replacement appears.
            var dispenser = CreateSocket(ctx, "Dispenser Socket",
                new Vector3(-0.42f, k_TableTopY + 0.06f, k_TableZ));
            var spawner = dispenser.AddComponent<SocketItemSpawner>();
            var spawnerSo = new SerializedObject(spawner);
            spawnerSo.FindProperty("m_ItemPrefab").objectReferenceValue = cubePrefab;
            spawnerSo.FindProperty("m_SpawnOnStart").boolValue = true;
            spawnerSo.FindProperty("m_SpawnParent").objectReferenceValue = spawnParent.transform;
            spawnerSo.ApplyModifiedPropertiesWithoutUndo();

            CreateLabel(ctx.content, "Dispenser\n(take one, get another)",
                new Vector3(-0.42f, k_TableTopY + 0.28f, k_TableZ));

            // Somewhere to put them, so the scene is not just a dispenser.
            CreateSocket(ctx, "Receiver Socket A", new Vector3(0.1f, k_TableTopY + 0.06f, k_TableZ));
            CreateSocket(ctx, "Receiver Socket B", new Vector3(0.5f, k_TableTopY + 0.06f, k_TableZ));
            CreateLabel(ctx.content, "Receivers",
                new Vector3(0.3f, k_TableTopY + 0.28f, k_TableZ));

            CreateTitle(ctx.content, "Sockets");
            SaveScene(ctx.scene, "02_Sockets");
        }

        static GameObject CreateSocket(SceneContext ctx, string name, Vector3 position)
        {
            var socketGo = new GameObject(name);
            socketGo.transform.SetParent(ctx.content, false);
            socketGo.transform.position = position;

            var trigger = socketGo.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.09f;

            var attach = new GameObject("Attach");
            attach.transform.SetParent(socketGo.transform, false);

            var socket = socketGo.AddComponent<XRSocketInteractor>();
            socket.interactionManager = ctx.interactionManager;
            socket.attachTransform = attach.transform;
            socket.showInteractableHoverMeshes = true;
            socket.recycleDelayTime = 0f;
            socket.hoverSocketSnapping = true;

            // A visible ring so the socket reads as a slot rather than empty air.
            CreatePrimitive(PrimitiveType.Cylinder, "Marker", socketGo.transform,
                position + new Vector3(0f, -0.055f, 0f), Quaternion.identity,
                new Vector3(0.11f, 0.004f, 0.11f),
                GetOrCreateMaterial("M_SocketMarker", new Color(0.95f, 0.85f, 0.25f)),
                removeCollider: true);

            return socketGo;
        }

        // ----------------------------------------------------------- scene 3: lever

        [MenuItem("IE Showcase/Scenes/03 Drivers - Lever")]
        public static void BuildLeverScene()
        {
            var ctx = CreateBaseScene("03_Drivers_Lever");

            var pivot = new Vector3(0f, k_TableTopY + 0.06f, k_TableZ);
            var metal = GetOrCreateMaterial("M_Metal", new Color(0.55f, 0.57f, 0.62f), 0.65f, 0.8f);
            var handleMat = GetOrCreateMaterial("M_LeverHandle", new Color(0.85f, 0.2f, 0.2f), 0.4f);

            // Housing: static, just something for the lever to come out of.
            CreatePrimitive(PrimitiveType.Cube, "Lever Housing", ctx.content,
                pivot + new Vector3(0f, -0.03f, 0f), Quaternion.identity,
                new Vector3(0.22f, 0.12f, 0.22f), metal);

            // Scale 1 root at the pivot, so the joint anchor and children stay undistorted.
            var arm = new GameObject("Lever Arm");
            arm.transform.SetParent(ctx.content, false);
            arm.transform.position = pivot;

            CreatePrimitive(PrimitiveType.Cylinder, "Shaft", arm.transform,
                pivot + new Vector3(0f, 0.19f, 0f), Quaternion.identity,
                new Vector3(0.028f, 0.19f, 0.028f), metal);

            var handle = CreatePrimitive(PrimitiveType.Sphere, "Handle", arm.transform,
                pivot + new Vector3(0f, 0.38f, 0f), Quaternion.identity,
                Vector3.one * 0.075f, handleMat);

            var body = arm.AddComponent<Rigidbody>();
            body.mass = 1.5f;
            // Damped and gravity-free so the lever holds wherever you leave it. With gravity on,
            // the arm balances on the hinge axis until the first nudge and then slams to a limit
            // and stays there. For a gravity-driven or spring-return lever instead, re-enable
            // gravity here, or set hinge.useSpring with a targetPosition.
            body.angularDamping = 3f;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Connected to the world, so the lever stays put while it swings.
            var hinge = arm.AddComponent<HingeJoint>();
            hinge.anchor = Vector3.zero;
            hinge.axis = new Vector3(1f, 0f, 0f);   // swings toward and away from the player
            hinge.useLimits = true;
            hinge.limits = new JointLimits { min = -65f, max = 65f, bounciness = 0f, bounceMinVelocity = 0f };
            hinge.useSpring = false;
            hinge.enableCollision = false;

            var grab = arm.AddComponent<XRGrabInteractable>();
            grab.interactionManager = ctx.interactionManager;
            // Velocity tracking lets the hinge keep the constraint instead of fighting a teleport.
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = false;
            grab.attachTransform = handle.transform;

            // Grabbable only at the end point: restrict interaction to the handle's collider.
            grab.colliders.Clear();
            grab.colliders.Add(handle.GetComponent<Collider>());

            CreateLabel(ctx.content, "Hinge joint lever\n(grab the red handle)",
                pivot + new Vector3(0f, 0.62f, 0f));
            CreateTitle(ctx.content, "Drivers - Lever");
            SaveScene(ctx.scene, "03_Drivers_Lever");
        }

        // ------------------------------------------------------- scene 4: air drawing

        [MenuItem("IE Showcase/Scenes/04 Air Drawing")]
        public static void BuildAirDrawingScene()
        {
            var ctx = CreateBaseScene("04_AirDrawing");

            var strokeParent = new GameObject("Drawings");
            strokeParent.transform.SetParent(ctx.content, false);

            var lineMaterial = GetOrCreateUnlitMaterial("M_AirLine", new Color(0.2f, 0.9f, 1f));

            var pencilRoot = new GameObject("Pencil");
            pencilRoot.transform.SetParent(ctx.content, false);
            pencilRoot.transform.position = new Vector3(0f, k_TableTopY + 0.02f, k_TableZ);
            pencilRoot.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var bodyMat = GetOrCreateMaterial("M_Pencil", new Color(0.9f, 0.7f, 0.2f));
            var tipMat = GetOrCreateMaterial("M_PencilTip", new Color(0.15f, 0.15f, 0.18f));

            var shaft = CreatePrimitive(PrimitiveType.Cylinder, "Body", pencilRoot.transform,
                pencilRoot.transform.position, pencilRoot.transform.rotation,
                new Vector3(0.011f, 0.075f, 0.011f), bodyMat);

            CreatePrimitive(PrimitiveType.Sphere, "Nib", pencilRoot.transform,
                pencilRoot.transform.position + pencilRoot.transform.up * 0.08f,
                Quaternion.identity, Vector3.one * 0.018f, tipMat, removeCollider: true);

            var tip = new GameObject("Tip");
            tip.transform.SetParent(pencilRoot.transform, false);
            tip.transform.localPosition = new Vector3(0f, 0.095f, 0f);

            var pencilBody = pencilRoot.AddComponent<Rigidbody>();
            pencilBody.mass = 0.15f;
            pencilBody.angularDamping = 2.5f;

            var grab = pencilRoot.AddComponent<XRGrabInteractable>();
            grab.interactionManager = ctx.interactionManager;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.useDynamicAttach = true;

            var pen = pencilRoot.AddComponent<AirDrawPen>();
            var penSo = new SerializedObject(pen);
            penSo.FindProperty("m_Tip").objectReferenceValue = tip.transform;
            penSo.FindProperty("m_LineMaterial").objectReferenceValue = lineMaterial;
            penSo.FindProperty("m_StrokeParent").objectReferenceValue = strokeParent.transform;
            penSo.ApplyModifiedPropertiesWithoutUndo();

            // Keep the pencil the only grab target on the object.
            grab.colliders.Clear();
            grab.colliders.Add(shaft.GetComponent<Collider>());

            CreateLabel(ctx.content, "Grab the pencil,\npull the trigger to draw",
                new Vector3(0f, k_TableTopY + 0.32f, k_TableZ));
            CreateTitle(ctx.content, "Air Drawing");
            SaveScene(ctx.scene, "04_AirDrawing");
        }

        // --------------------------------------------------------- scene 5: climbing

        [MenuItem("IE Showcase/Scenes/05 Climbing")]
        public static void BuildClimbingScene()
        {
            var ctx = CreateBaseScene("05_Climbing", withTable: false);

            var climbProvider = ctx.rig.GetComponentInChildren<ClimbProvider>();
            if (climbProvider == null)
                Debug.LogWarning("[ShowcaseSceneBuilder] No ClimbProvider on the rig; climb interactables will not move the rig.");

            var wallMat = GetOrCreateMaterial("M_Wall", new Color(0.45f, 0.44f, 0.46f));
            var climbWallMat = GetOrCreateMaterial("M_ClimbWall", new Color(0.35f, 0.5f, 0.6f));
            var holdMat = GetOrCreateMaterial("M_ClimbHold", new Color(0.95f, 0.45f, 0.15f));

            const float wallHeight = 4f;
            const float wallWidth = 1.6f;

            // ---- Wall A: discrete holds ----
            var wallA = new GameObject("Wall A - Holds");
            wallA.transform.SetParent(ctx.content, false);
            wallA.transform.position = new Vector3(-1.25f, 0f, 1.4f);

            CreatePrimitive(PrimitiveType.Cube, "Surface", wallA.transform,
                wallA.transform.position + new Vector3(0f, wallHeight * 0.5f, 0.12f),
                Quaternion.identity, new Vector3(wallWidth, wallHeight, 0.2f), wallMat);

            var holds = new GameObject("Holds");
            holds.transform.SetParent(wallA.transform, false);

            // Staggered left/right so you have to alternate hands going up.
            for (var i = 0; i < 9; i++)
            {
                var side = (i % 2 == 0) ? -1f : 1f;
                var localPos = new Vector3(side * 0.32f, 0.55f + i * 0.38f, 0f);
                var hold = CreatePrimitive(PrimitiveType.Sphere, $"Hold {i + 1:00}", holds.transform,
                    wallA.transform.position + localPos, Quaternion.identity,
                    Vector3.one * 0.14f, holdMat);

                AddClimbInteractable(hold, ctx, climbProvider);
            }

            CreateLabel(ctx.content, "Grab the orange holds",
                wallA.transform.position + new Vector3(0f, 0.25f, -0.2f));

            // ---- Wall B: the whole surface is grabbable ----
            var wallB = new GameObject("Wall B - Whole Surface");
            wallB.transform.SetParent(ctx.content, false);
            wallB.transform.position = new Vector3(1.25f, 0f, 1.4f);

            var surfaceB = CreatePrimitive(PrimitiveType.Cube, "Climbable Surface", wallB.transform,
                wallB.transform.position + new Vector3(0f, wallHeight * 0.5f, 0.12f),
                Quaternion.identity, new Vector3(wallWidth, wallHeight, 0.2f), climbWallMat);

            AddClimbInteractable(surfaceB, ctx, climbProvider);

            CreateLabel(ctx.content, "Grab anywhere on this wall",
                wallB.transform.position + new Vector3(0f, 0.25f, -0.2f));

            // Placed in the gap between the two walls, which would otherwise hide it.
            CreateLabel(ctx.content, "Climbing", new Vector3(0f, 2.3f, 1.5f), 0.07f);
            SaveScene(ctx.scene, "05_Climbing");
        }

        /// <summary>
        /// Adds a <see cref="ClimbInteractable"/> and tames the Rigidbody it drags in.
        /// </summary>
        /// <remarks>
        /// <see cref="ClimbInteractable"/> is marked <c>[RequireComponent(typeof(Rigidbody))]</c>,
        /// so adding it gives the wall or hold a dynamic body that would otherwise drop to the
        /// floor the moment play mode starts. Kinematic also gives the hand's trigger volume a
        /// Rigidbody to collide against, which is what makes the hold detectable.
        /// </remarks>
        static ClimbInteractable AddClimbInteractable(GameObject target, SceneContext ctx,
            ClimbProvider climbProvider)
        {
            var climb = target.AddComponent<ClimbInteractable>();
            climb.interactionManager = ctx.interactionManager;
            climb.climbProvider = climbProvider;

            var body = target.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            return climb;
        }

        // --------------------------------------------------- scene 6: two-hand items

        [MenuItem("IE Showcase/Scenes/06 Two Hand Interactables")]
        public static void BuildTwoHandScene()
        {
            var ctx = CreateBaseScene("06_TwoHandInteractables");

            var woodMat = GetOrCreateMaterial("M_Wood", new Color(0.5f, 0.35f, 0.2f));
            var metalMat = GetOrCreateMaterial("M_Metal", new Color(0.55f, 0.57f, 0.62f), 0.65f, 0.8f);
            var gunMat = GetOrCreateMaterial("M_GunBody", new Color(0.2f, 0.21f, 0.24f), 0.5f, 0.4f);
            var gripMat = GetOrCreateMaterial("M_GunGrip", new Color(0.35f, 0.28f, 0.24f));

            // Laid out in two rows so the long staff does not overlap the other two.
            const float frontRowZ = k_TableZ - 0.17f;
            const float backRowZ = k_TableZ + 0.18f;

            // ---- Hammer: grab anywhere along the handle, with either or both hands ----
            var hammer = CreateTwoHandRoot("Hammer", ctx,
                new Vector3(-0.42f, k_TableTopY + 0.05f, frontRowZ), Quaternion.Euler(0f, 0f, 90f), 2.5f);
            var hammerHandle = CreatePrimitive(PrimitiveType.Cylinder, "Handle", hammer.transform,
                hammer.transform.position, hammer.transform.rotation,
                new Vector3(0.022f, 0.2f, 0.022f), woodMat);
            // The head keeps its collider: it is not grabbable, but it stops the hammer rolling.
            CreatePrimitive(PrimitiveType.Cube, "Head", hammer.transform,
                hammer.transform.position + hammer.transform.up * 0.23f, hammer.transform.rotation,
                new Vector3(0.1f, 0.075f, 0.075f), metalMat);
            SetGrabColliders(hammer, hammerHandle.GetComponent<Collider>());

            // ---- Staff: long shaft, two hands anywhere ----
            var staff = CreateTwoHandRoot("Staff", ctx,
                new Vector3(0f, k_TableTopY + 0.05f, backRowZ), Quaternion.Euler(0f, 0f, 90f), 2f);
            var staffShaft = CreatePrimitive(PrimitiveType.Cylinder, "Shaft", staff.transform,
                staff.transform.position, staff.transform.rotation,
                new Vector3(0.019f, 0.55f, 0.019f), woodMat);
            CreatePrimitive(PrimitiveType.Sphere, "Tip", staff.transform,
                staff.transform.position + staff.transform.up * 0.56f, Quaternion.identity,
                Vector3.one * 0.06f, metalMat, removeCollider: true);
            SetGrabColliders(staff, staffShaft.GetComponent<Collider>());

            // ---- Gun: exactly two grab points, the grip and the foregrip ----
            // Rolled onto its side so the grip lies along the table instead of through it.
            var gun = CreateTwoHandRoot("Gun", ctx,
                new Vector3(0.42f, k_TableTopY + 0.035f, frontRowZ), Quaternion.Euler(0f, 0f, 90f), 1.8f);
            CreatePrimitive(PrimitiveType.Cube, "Body", gun.transform,
                gun.transform.position, gun.transform.rotation,
                new Vector3(0.045f, 0.075f, 0.3f), gunMat);
            var grip = CreatePrimitive(PrimitiveType.Cube, "Grip (grab point)", gun.transform,
                gun.transform.position
                    + gun.transform.up * -0.075f + gun.transform.forward * -0.1f,
                gun.transform.rotation, new Vector3(0.04f, 0.11f, 0.055f), gripMat);
            var foregrip = CreatePrimitive(PrimitiveType.Cube, "Foregrip (grab point)", gun.transform,
                gun.transform.position
                    + gun.transform.up * -0.06f + gun.transform.forward * 0.1f,
                gun.transform.rotation, new Vector3(0.034f, 0.08f, 0.045f), gripMat);
            SetGrabColliders(gun, grip.GetComponent<Collider>(), foregrip.GetComponent<Collider>());

            CreateLabel(ctx.content, "Hammer", new Vector3(-0.42f, k_TableTopY + 0.4f, frontRowZ));
            CreateLabel(ctx.content, "Staff", new Vector3(0f, k_TableTopY + 0.56f, backRowZ));
            CreateLabel(ctx.content, "Gun\n(grip + foregrip)", new Vector3(0.42f, k_TableTopY + 0.4f, frontRowZ));
            CreateTitle(ctx.content, "Two Hand Interactables");
            SaveScene(ctx.scene, "06_TwoHandInteractables");
        }

        static GameObject CreateTwoHandRoot(string name, SceneContext ctx, Vector3 position,
            Quaternion rotation, float mass)
        {
            var root = new GameObject(name);
            root.transform.SetParent(ctx.content, false);
            root.transform.SetPositionAndRotation(position, rotation);

            var body = root.AddComponent<Rigidbody>();
            body.mass = mass;
            // These are cylinders resting on a flat table; damping stops them rolling off.
            body.angularDamping = 2.5f;

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.interactionManager = ctx.interactionManager;
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            // Multiple is what actually permits a second hand onto the same object.
            grab.selectMode = InteractableSelectMode.Multiple;
            grab.useDynamicAttach = true;

            // Added explicitly so its two-handed settings are visible in the inspector rather
            // than being created implicitly at runtime.
            root.AddComponent<XRGeneralGrabTransformer>();

            return root;
        }

        static void SetGrabColliders(GameObject interactableRoot, params Collider[] colliders)
        {
            var grab = interactableRoot.GetComponent<XRGrabInteractable>();
            grab.colliders.Clear();
            foreach (var collider in colliders)
            {
                if (collider != null)
                    grab.colliders.Add(collider);
            }
        }

        // ------------------------------------------------------------- shared setup

        struct SceneContext
        {
            public Scene scene;
            public Transform content;
            public GameObject rig;
            public XRInteractionManager interactionManager;
        }

        static SceneContext CreateBaseScene(string sceneName, bool withTable = true)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // An empty scene has no ambient setup, which renders almost black under URP.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.33f, 0.35f, 0.40f);
            var skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            if (skybox != null)
            {
                RenderSettings.skybox = skybox;
                RenderSettings.ambientMode = AmbientMode.Skybox;
            }

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.SetPositionAndRotation(new Vector3(0f, 3f, 0f),
                Quaternion.Euler(50f, -30f, 0f));

            // A thick slab rather than a plane, so nothing tunnels through the floor.
            CreatePrimitive(PrimitiveType.Cube, "Floor", null,
                new Vector3(0f, -0.1f, 0f), Quaternion.identity, new Vector3(24f, 0.2f, 24f),
                GetOrCreateMaterial("M_Floor", new Color(0.32f, 0.34f, 0.36f)));

            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_RigPrefabPath);
            if (rigPrefab == null)
                Debug.LogError("[ShowcaseSceneBuilder] XR Rig prefab missing at " + k_RigPrefabPath);

            var rig = rigPrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab) : null;
            if (rig != null)
                rig.transform.position = Vector3.zero;

            var content = new GameObject("Showcase Content").transform;

            if (withTable)
            {
                CreatePrimitive(PrimitiveType.Cube, "Table", content,
                    new Vector3(0f, k_TableTopY * 0.5f, k_TableZ), Quaternion.identity,
                    new Vector3(1.5f, k_TableTopY, 0.7f),
                    GetOrCreateMaterial("M_Table", new Color(0.42f, 0.3f, 0.22f)));
            }

            return new SceneContext
            {
                scene = scene,
                content = content,
                rig = rig,
                interactionManager = rig != null ? rig.GetComponentInChildren<XRInteractionManager>() : null,
            };
        }

        static void SaveScene(Scene scene, string sceneName)
        {
            EditorSceneManager.SaveScene(scene, k_ScenesFolder + sceneName + ".unity");
        }

        // -------------------------------------------------------------- prefab setup

        static void EnsureCubeInteractablePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_CubePrefabPath) != null)
                return;

            var folder = "Assets/_Project/Prefabs/Interactables";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Interactables");

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Cube Interactable";
            cube.transform.localScale = Vector3.one * 0.09f;
            cube.GetComponent<Renderer>().sharedMaterial =
                GetOrCreateMaterial("M_SocketCube", new Color(0.4f, 0.7f, 0.95f));

            var body = cube.AddComponent<Rigidbody>();
            body.mass = 0.4f;

            var grab = cube.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;

            PrefabUtility.SaveAsPrefabAsset(cube, k_CubePrefabPath);
            Object.DestroyImmediate(cube);
        }

        static void AddScenesToBuildSettings()
        {
            var existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var changed = false;

            foreach (var sceneName in k_SceneNames)
            {
                var path = k_ScenesFolder + sceneName + ".unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    continue;

                var alreadyListed = false;
                foreach (var entry in existing)
                {
                    if (entry.path == path)
                    {
                        alreadyListed = true;
                        break;
                    }
                }

                if (alreadyListed)
                    continue;

                existing.Add(new EditorBuildSettingsScene(path, true));
                changed = true;
            }

            if (changed)
                EditorBuildSettings.scenes = existing.ToArray();
        }

        // ------------------------------------------------------------------ helpers

        static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent,
            Vector3 position, Quaternion rotation, Vector3 scale, Material material,
            bool removeCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;

            if (parent != null)
                go.transform.SetParent(parent, true);

            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;

            if (removeCollider)
            {
                var collider = go.GetComponent<Collider>();
                if (collider != null)
                    Object.DestroyImmediate(collider);
            }

            if (material != null)
                go.GetComponent<Renderer>().sharedMaterial = material;

            return go;
        }

        static GameObject CreateLabel(Transform parent, string text, Vector3 position,
            float characterSize = 0.022f)
        {
            var go = new GameObject("Label - " + text.Replace("\n", " "));
            if (parent != null)
                go.transform.SetParent(parent, true);

            // Rotated to face the rig, which starts at the origin looking down +Z.
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 180f, 0f));

            var textMesh = go.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.characterSize = characterSize;
            textMesh.fontSize = 72;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textMesh.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            return go;
        }

        static GameObject CreateTitle(Transform parent, string text)
        {
            return CreateLabel(parent, text, new Vector3(0f, 2.1f, 2.4f), 0.07f);
        }

        static Material GetOrCreateMaterial(string assetName, Color color,
            float smoothness = 0.25f, float metallic = 0f)
        {
            var path = k_MaterialsFolder + assetName + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[ShowcaseSceneBuilder] URP Lit shader not found.");
                return null;
            }

            var material = new Material(shader) { name = assetName };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Material GetOrCreateUnlitMaterial(string assetName, Color color)
        {
            var path = k_MaterialsFolder + assetName + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogError("[ShowcaseSceneBuilder] URP Unlit shader not found.");
                return null;
            }

            var material = new Material(shader) { name = assetName };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
