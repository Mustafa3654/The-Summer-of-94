using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates a small, near-black moonlit cabin corridor sandbox for The Summer of '94.
/// Run from Tools > Build Summer of 94 Sandbox.
/// </summary>
public static class SetupSummerOf94
{
    private const string ScenePath = "Assets/Scenes/SummerOf94_Sandbox.unity";
    private const string MaterialFolder = "Assets/Settings/SummerOf94";

    [MenuItem("Tools/Build Summer of 94 Sandbox", priority = 10)]
    public static void BuildSandbox()
    {
        BuildSandboxInternal();
    }

    [MenuItem("Tools/Setup Car Intro", priority = 11)]
    public static void SetupCarIntro()
    {
        BuildSandboxInternal();
    }

    private static void BuildSandboxInternal()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EnsureFolder("Assets/Scenes");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "SummerOf94_Sandbox";

        ConfigurePitchBlackEnvironment();

        Material floorMaterial = CreateMaterial(
            "SummerOf94_Floor_Material",
            new Color(0.035f, 0.045f, 0.055f),
            0.05f,
            0.18f);
        Material wallMaterial = CreateMaterial(
            "SummerOf94_Wall_Material",
            new Color(0.055f, 0.065f, 0.075f),
            0f,
            0.12f);
        Material beamMaterial = CreateMaterial(
            "SummerOf94_Beam_Material",
            new Color(0.025f, 0.028f, 0.032f),
            0f,
            0.08f);
        Material playerMaterial = CreateMaterial(
            "SummerOf94_Player_Material",
            new Color(0.18f, 0.2f, 0.22f),
            0f,
            0.25f);

        GameObject environment = new GameObject("Environment");
        GameObject geometry = new GameObject("Cabin Corridor Geometry");
        geometry.transform.SetParent(environment.transform);
        GameObject atmosphere = new GameObject("Storm Atmosphere");
        atmosphere.transform.SetParent(environment.transform);

        // Lights first, so the foyer slam trigger can be wired to the LightningEffect.
        CreateStormLight(atmosphere.transform);
        CreateMoonLight(atmosphere.transform);
        CreateGround(geometry.transform, floorMaterial);
        CreateCabinCorridor(geometry.transform, wallMaterial, beamMaterial);

        GameObject player = CreatePlayer(playerMaterial);
        CreateFlashlight(player.transform.Find("Main Camera"));
        CreateCarIntro(environment.transform, player, wallMaterial, beamMaterial);

        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.LookAt(
                new Vector3(0f, 1.5f, 0f),
                Quaternion.Euler(20f, 0f, 0f),
                12f);
        }

        EditorUtility.DisplayDialog(
            "The Summer of '94",
            "Sandbox built successfully.\n\nOpen the saved scene and press Play.\n\n" +
            "WASD: move  |  Mouse: look\nF: flashlight  |  Esc: unlock cursor\n" +
            "E: radio, ignition, doors, and pickups\n\n" +
            "The engine stalls, then step out and head for the cabin doorway.\n" +
            "The entrance slams shut behind you in the foyer.\n" +
            "The office door needs the key from the bunk room table.\n\n" +
            "All floors and walls are flagged Navigation Static, so\n" +
            "Window > AI > Navigation can bake a NavMesh for Step 5.",
            "OK");
    }

    [MenuItem("Tools/Build Summer of 94 Sandbox", validate = true)]
    private static bool ValidateBuildSandbox()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem("Tools/Setup Car Intro", validate = true)]
    private static bool ValidateSetupCarIntro()
    {
        return ValidateBuildSandbox();
    }

    private static void ConfigurePitchBlackEnvironment()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.039f, 0.055f, 0.102f);
        RenderSettings.ambientIntensity = 0.35f;
        RenderSettings.reflectionIntensity = 0f;
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.039f, 0.055f, 0.102f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.03f;
        RenderSettings.subtractiveShadowColor = Color.black;

        Light[] existingLights = Object.FindObjectsByType<Light>();
        foreach (Light light in existingLights)
        {
            light.enabled = false;
        }
    }

    private static void CreateCarIntro(Transform parent, GameObject player, Material wallMaterial, Material beamMaterial)
    {
        Material dashboardMaterial = CreateMaterial(
            "SummerOf94_Dashboard_Material",
            new Color(0.045f, 0.05f, 0.055f),
            0.05f,
            0.2f);
        Material upholsteryMaterial = CreateMaterial(
            "SummerOf94_Upholstery_Material",
            new Color(0.035f, 0.028f, 0.025f),
            0f,
            0.28f);
        Material trimMaterial = CreateMaterial(
            "SummerOf94_Interior_Trim_Material",
            new Color(0.12f, 0.105f, 0.09f),
            0.1f,
            0.3f);
        Material glassMaterial = CreateGlassMaterial();

        CreateBlock(parent, "Camp Entrance Ground", new Vector3(0f, -0.05f, -11f), new Vector3(10f, 0.1f, 5.5f), beamMaterial);

        GameObject car = new GameObject("Intro Car");
        car.transform.SetParent(parent);
        car.transform.position = new Vector3(0f, 0f, -11f);

        CreateBlock(car.transform, "Car Floor", new Vector3(0f, 0.32f, 0f), new Vector3(4.2f, 0.18f, 3.25f), beamMaterial);
        CreateBlock(car.transform, "Dashboard", new Vector3(0f, 1.27f, 1.38f), new Vector3(4.1f, 0.5f, 0.38f), dashboardMaterial);
        CreateBlock(car.transform, "Dashboard Lower Panel", new Vector3(0f, 0.86f, 1.46f), new Vector3(4.0f, 0.45f, 0.28f), dashboardMaterial);
        CreateBlock(car.transform, "Driver Door", new Vector3(-2.02f, 1.45f, 0.1f), new Vector3(0.2f, 2.25f, 3.0f), wallMaterial);
        CreateBlock(car.transform, "Passenger Door", new Vector3(2.02f, 1.45f, 0.1f), new Vector3(0.2f, 2.25f, 3.0f), wallMaterial);
        CreateBlock(car.transform, "Rear Interior Panel", new Vector3(0f, 1.55f, -1.48f), new Vector3(4.05f, 2.35f, 0.18f), wallMaterial);
        CreateBlock(car.transform, "Roof", new Vector3(0f, 3.05f, 0.1f), new Vector3(4.2f, 0.18f, 3.2f), beamMaterial);

        CreateBlock(car.transform, "Windshield Frame Top", new Vector3(0f, 2.95f, 1.5f), new Vector3(4.1f, 0.18f, 0.18f), trimMaterial);
        CreateBlock(car.transform, "Windshield Frame Left", new Vector3(-1.93f, 2.05f, 1.5f), new Vector3(0.18f, 1.75f, 0.18f), trimMaterial);
        CreateBlock(car.transform, "Windshield Frame Right", new Vector3(1.93f, 2.05f, 1.5f), new Vector3(0.18f, 1.75f, 0.18f), trimMaterial);
        CreateBlock(car.transform, "Front Windshield", new Vector3(0f, 2.05f, 1.51f), new Vector3(3.72f, 1.72f, 0.06f), glassMaterial);

        CreateBlock(car.transform, "Driver Seat Cushion", new Vector3(-0.72f, 0.65f, -0.05f), new Vector3(1.1f, 0.32f, 1.15f), upholsteryMaterial);
        CreateBlock(car.transform, "Driver Seat Back", new Vector3(-0.72f, 1.42f, -0.55f), new Vector3(1.1f, 1.45f, 0.3f), upholsteryMaterial);
        CreateBlock(car.transform, "Passenger Seat Cushion", new Vector3(0.72f, 0.65f, -0.05f), new Vector3(1.1f, 0.32f, 1.15f), upholsteryMaterial);
        CreateBlock(car.transform, "Passenger Seat Back", new Vector3(0.72f, 1.42f, -0.55f), new Vector3(1.1f, 1.45f, 0.3f), upholsteryMaterial);

        GameObject steeringWheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        steeringWheel.name = "Steering Wheel";
        steeringWheel.transform.SetParent(car.transform);
        steeringWheel.transform.localPosition = new Vector3(-0.72f, 1.43f, 0.85f);
        steeringWheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        steeringWheel.transform.localScale = new Vector3(0.46f, 0.07f, 0.46f);
        steeringWheel.GetComponent<Renderer>().sharedMaterial = trimMaterial;

        CreateBlock(car.transform, "Gauge Cluster", new Vector3(-0.72f, 1.5f, 1.16f), new Vector3(0.85f, 0.25f, 0.15f), trimMaterial);
        GameObject radio = CreateBlock(car.transform, "1994 Radio", new Vector3(0.35f, 1.48f, 1.2f), new Vector3(0.62f, 0.28f, 0.26f), trimMaterial);
        RadioStatic radioStatic = radio.AddComponent<RadioStatic>();

        GameObject ignition = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ignition.name = "Ignition Key";
        ignition.transform.SetParent(car.transform);
        ignition.transform.localPosition = new Vector3(-0.38f, 1.43f, 1.2f);
        ignition.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ignition.transform.localScale = new Vector3(0.09f, 0.16f, 0.09f);
        ignition.GetComponent<Renderer>().sharedMaterial = trimMaterial;

        GameObject playerSeat = new GameObject("Player Seat");
        playerSeat.transform.SetParent(car.transform);
        playerSeat.transform.localPosition = Vector3.zero;
        playerSeat.transform.localRotation = Quaternion.identity;

        GameObject dashboardLightObject = new GameObject("Dashboard Gauge Light");
        dashboardLightObject.transform.SetParent(car.transform);
        dashboardLightObject.transform.localPosition = new Vector3(-0.72f, 1.68f, 1.28f);
        Light dashboardLight = dashboardLightObject.AddComponent<Light>();
        dashboardLight.type = LightType.Point;
        dashboardLight.color = new Color(1f, 0.62f, 0.2f);
        dashboardLight.intensity = 0.32f;
        dashboardLight.range = 2.4f;
        dashboardLight.shadows = LightShadows.None;

        GameObject stormObject = new GameObject("Rain and Thunder Outside Windshield");
        stormObject.transform.SetParent(car.transform);
        StormAmbience stormAmbience = stormObject.AddComponent<StormAmbience>();

        AudioSource engineAudioSource = car.AddComponent<AudioSource>();
        CarController carController = car.AddComponent<CarController>();
        CarBreakdownSequence breakdownSequence = car.AddComponent<CarBreakdownSequence>();
        EngineVibration engineVibration = player.AddComponent<EngineVibration>();

        player.transform.position = new Vector3(-0.72f, 0.05f, 0.15f);
        player.transform.rotation = Quaternion.identity;
        PlayerInteraction playerInteraction = player.GetComponent<PlayerInteraction>();
        if (playerInteraction == null)
        {
            playerInteraction = player.AddComponent<PlayerInteraction>();
        }

        SetSerializedReference(playerInteraction, "playerCamera", player.transform.Find("Main Camera"));
        SetSerializedReference(engineVibration, "targetCamera", player.transform.Find("Main Camera"));
        SetSerializedReference(carController, "playerController", player.GetComponent<FirstPersonController>());
        SetSerializedReference(carController, "playerSeat", playerSeat.transform);
        SetSerializedReference(carController, "playerCamera", player.transform.Find("Main Camera"));
        SetSerializedReference(carController, "driverDoor", car.transform.Find("Driver Door"));
        SetSerializedReference(carController, "driverDoorCollider", car.transform.Find("Driver Door").GetComponent<Collider>());
        SetSerializedReference(breakdownSequence, "carController", carController);
        SetSerializedReference(breakdownSequence, "engineVibration", engineVibration);
        SetSerializedReference(breakdownSequence, "stormAmbience", stormAmbience);
        SetSerializedReference(breakdownSequence, "engineAudioSource", engineAudioSource);
        SetSerializedObjectArray(breakdownSequence, "dashboardLights", new Object[] { dashboardLight });
        SetSerializedReference(radioStatic, "radioAudioSource", radio.GetComponent<AudioSource>());
        SetSerializedReference(ignition.AddComponent<IgnitionKey>(), "breakdownSequence", breakdownSequence);
        carController.PlacePlayerInSeat();
    }

    private static Material CreateGlassMaterial()
    {
        Material glass = CreateMaterial(
            "SummerOf94_Windshield_Glass_Material",
            new Color(0.06f, 0.11f, 0.15f, 0.18f),
            0f,
            0.65f);

        if (glass.HasProperty("_Surface")) glass.SetFloat("_Surface", 1f);
        if (glass.HasProperty("_Blend")) glass.SetFloat("_Blend", 0f);
        if (glass.HasProperty("_AlphaClip")) glass.SetFloat("_AlphaClip", 0f);
        if (glass.HasProperty("_BaseColor")) glass.SetColor("_BaseColor", new Color(0.06f, 0.11f, 0.15f, 0.18f));
        glass.renderQueue = 3000;
        EditorUtility.SetDirty(glass);
        return glass;
    }

    private static void CreateGround(Transform parent, Material floorMaterial)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground Plane (100x100)";
        ground.transform.SetParent(parent);
        ground.transform.localPosition = Vector3.zero;
        ground.transform.localScale = new Vector3(10f, 1f, 10f);
        ground.GetComponent<Renderer>().sharedMaterial = floorMaterial;

        MeshCollider meshCollider = ground.GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            meshCollider = ground.AddComponent<MeshCollider>();
        }

        meshCollider.sharedMesh = ground.GetComponent<MeshFilter>().sharedMesh;
        MarkNavigationStatic(ground);
    }

    private const float WallHeight = 4f;
    private const float WallThickness = 0.35f;
    private const float DoorwayWidth = 1.5f;
    private const float DoorwayHeight = 2.7f;

    /// <summary>
    /// Builds the cabin shell: entrance foyer, main corridor, an open bunk room and a locked
    /// camp office. All floor and wall geometry is flagged Navigation Static, ready for a
    /// NavMesh bake in Step 5.
    /// </summary>
    private static void CreateCabinCorridor(Transform parent, Material wallMaterial, Material beamMaterial)
    {
        GameObject corridor = new GameObject("Main Corridor");
        corridor.transform.SetParent(parent);

        // Front wall (z = -9) holds the main entrance the player walks in through.
        Vector3 entranceDoorway = CreateWallWithDoorway(
            corridor.transform,
            "Entrance Wall",
            new Vector3(0f, WallHeight * 0.5f, -9f),
            6.7f,
            false,
            0f,
            wallMaterial,
            beamMaterial);

        // Side walls, each opened by a doorway into a room.
        Vector3 bunkRoomDoorway = CreateWallWithDoorway(
            corridor.transform,
            "Left Cabin Wall",
            new Vector3(-3.35f, WallHeight * 0.5f, 4f),
            26f,
            true,
            6f,
            wallMaterial,
            beamMaterial);

        Vector3 officeDoorway = CreateWallWithDoorway(
            corridor.transform,
            "Right Cabin Wall",
            new Vector3(3.35f, WallHeight * 0.5f, 4f),
            26f,
            true,
            10f,
            wallMaterial,
            beamMaterial);

        MarkNavigationStatic(CreateBlock(corridor.transform, "Far Cabin Wall", new Vector3(0f, 2f, 17f), new Vector3(6.7f, WallHeight, WallThickness), wallMaterial));
        CreateBlock(corridor.transform, "Cabin Ceiling", new Vector3(0f, 4.05f, 4f), new Vector3(6.7f, 0.25f, 26f), beamMaterial);

        for (int i = 0; i < 5; i++)
        {
            float z = -7f + i * 6f;
            MarkNavigationStatic(CreateBlock(corridor.transform, $"Left Support Beam {i + 1}", new Vector3(-3.05f, 1.95f, z), new Vector3(0.42f, 3.9f, 0.42f), beamMaterial));
            MarkNavigationStatic(CreateBlock(corridor.transform, $"Right Support Beam {i + 1}", new Vector3(3.05f, 1.95f, z), new Vector3(0.42f, 3.9f, 0.42f), beamMaterial));
        }

        CreateBunkRoom(parent, wallMaterial, beamMaterial);
        CreateCampOffice(parent, wallMaterial, beamMaterial);
        CreateDoorsAndKeys(parent, entranceDoorway, bunkRoomDoorway, officeDoorway, wallMaterial, beamMaterial);
    }

    /// <summary>Open side room off the corridor. The office key sits on the table inside.</summary>
    private static void CreateBunkRoom(Transform parent, Material wallMaterial, Material beamMaterial)
    {
        GameObject room = new GameObject("Bunk Room");
        room.transform.SetParent(parent);

        MarkNavigationStatic(CreateBlock(room.transform, "Bunk Room Outer Wall", new Vector3(-11.5f, 2f, 6f), new Vector3(WallThickness, WallHeight, 9.35f), wallMaterial));
        MarkNavigationStatic(CreateBlock(room.transform, "Bunk Room Near Wall", new Vector3(-7.42f, 2f, 1.5f), new Vector3(8.5f, WallHeight, WallThickness), wallMaterial));
        MarkNavigationStatic(CreateBlock(room.transform, "Bunk Room Far Wall", new Vector3(-7.42f, 2f, 10.5f), new Vector3(8.5f, WallHeight, WallThickness), wallMaterial));
        CreateBlock(room.transform, "Bunk Room Ceiling", new Vector3(-7.42f, 4.05f, 6f), new Vector3(8.5f, 0.25f, 9.35f), beamMaterial);

        // A pair of bunks and the table the key rests on.
        MarkNavigationStatic(CreateBlock(room.transform, "Bunk Frame Lower", new Vector3(-10.4f, 0.55f, 8.4f), new Vector3(1.7f, 0.24f, 3.4f), beamMaterial));
        MarkNavigationStatic(CreateBlock(room.transform, "Bunk Frame Upper", new Vector3(-10.4f, 1.75f, 8.4f), new Vector3(1.7f, 0.24f, 3.4f), beamMaterial));
        MarkNavigationStatic(CreateBlock(room.transform, "Bunk Frame Posts", new Vector3(-10.4f, 1.15f, 6.75f), new Vector3(1.7f, 2.3f, 0.16f), beamMaterial));
        MarkNavigationStatic(CreateBlock(room.transform, "Key Table", new Vector3(-7.4f, 0.45f, 5.2f), new Vector3(1.5f, 0.9f, 1.1f), beamMaterial));
    }

    /// <summary>Locked room behind the office door. Nothing in it yet beyond set dressing.</summary>
    private static void CreateCampOffice(Transform parent, Material wallMaterial, Material beamMaterial)
    {
        GameObject room = new GameObject("Camp Office");
        room.transform.SetParent(parent);

        MarkNavigationStatic(CreateBlock(room.transform, "Office Outer Wall", new Vector3(10.5f, 2f, 10f), new Vector3(WallThickness, WallHeight, 7.35f), wallMaterial));
        MarkNavigationStatic(CreateBlock(room.transform, "Office Near Wall", new Vector3(6.92f, 2f, 6.5f), new Vector3(7.5f, WallHeight, WallThickness), wallMaterial));
        MarkNavigationStatic(CreateBlock(room.transform, "Office Far Wall", new Vector3(6.92f, 2f, 13.5f), new Vector3(7.5f, WallHeight, WallThickness), wallMaterial));
        CreateBlock(room.transform, "Office Ceiling", new Vector3(6.92f, 4.05f, 10f), new Vector3(7.5f, 0.25f, 7.35f), beamMaterial);

        MarkNavigationStatic(CreateBlock(room.transform, "Office Desk", new Vector3(8.8f, 0.5f, 12.2f), new Vector3(2.6f, 1f, 1.3f), beamMaterial));
        MarkNavigationStatic(CreateBlock(room.transform, "Filing Cabinet", new Vector3(9.9f, 0.85f, 7.8f), new Vector3(1f, 1.7f, 0.7f), beamMaterial));
    }

    /// <summary>Places the three doors, the office key pickup and the foyer slam trigger.</summary>
    private static void CreateDoorsAndKeys(
        Transform parent,
        Vector3 entranceDoorway,
        Vector3 bunkRoomDoorway,
        Vector3 officeDoorway,
        Material wallMaterial,
        Material beamMaterial)
    {
        Material doorMaterial = CreateMaterial(
            "SummerOf94_Door_Material",
            new Color(0.075f, 0.06f, 0.05f),
            0f,
            0.22f);
        Material keyMaterial = CreateMaterial(
            "SummerOf94_Key_Material",
            new Color(0.42f, 0.33f, 0.12f),
            0.75f,
            0.62f);

        GameObject interactables = new GameObject("Cabin Interactables");
        interactables.transform.SetParent(parent);

        // Main entrance: unlocked on approach, slammed and locked once the player is inside.
        Door entranceDoor = CreateHingedDoor<Door>(
            interactables.transform,
            "Cabin Entrance Door",
            entranceDoorway,
            false,
            true,
            doorMaterial);

        CreateHingedDoor<Door>(
            interactables.transform,
            "Bunk Room Door",
            bunkRoomDoorway,
            true,
            true,
            doorMaterial);

        LockedDoor officeDoor = CreateHingedDoor<LockedDoor>(
            interactables.transform,
            "Camp Office Door (Locked)",
            officeDoorway,
            true,
            false,
            doorMaterial);
        SetSerializedString(officeDoor, "requiredKeyId", "OfficeKey");

        // The key that opens the office, resting on the bunk room table.
        GameObject key = GameObject.CreatePrimitive(PrimitiveType.Cube);
        key.name = "Office Key Pickup";
        key.transform.SetParent(interactables.transform);
        key.transform.position = new Vector3(-7.4f, 1.02f, 5.2f);
        key.transform.localScale = new Vector3(0.08f, 0.03f, 0.26f);
        key.GetComponent<Renderer>().sharedMaterial = keyMaterial;

        BoxCollider keyCollider = key.GetComponent<BoxCollider>();
        keyCollider.size = new Vector3(3.2f, 8f, 1.6f); // Generous grab volume around the small mesh.

        KeyItem keyItem = key.AddComponent<KeyItem>();
        SetSerializedString(keyItem, "itemId", "OfficeKey");
        SetSerializedString(keyItem, "displayName", "Tarnished Office Key");

        GameObject keyGlow = new GameObject("Key Glint");
        keyGlow.transform.SetParent(key.transform);
        keyGlow.transform.localPosition = Vector3.zero;
        Light keyLight = keyGlow.AddComponent<Light>();
        keyLight.type = LightType.Point;
        keyLight.color = new Color(1f, 0.82f, 0.45f);
        keyLight.intensity = 0.22f;
        keyLight.range = 2.6f;
        keyLight.shadows = LightShadows.None;

        // Foyer volume that shuts the player in behind them.
        GameObject slamTrigger = new GameObject("Foyer Slam Trigger");
        slamTrigger.transform.SetParent(interactables.transform);
        slamTrigger.transform.position = new Vector3(0f, 1.3f, -6.4f);

        BoxCollider triggerCollider = slamTrigger.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(6f, 2.6f, 2.4f);

        DoorSlamTrigger doorSlamTrigger = slamTrigger.AddComponent<DoorSlamTrigger>();
        SetSerializedReference(doorSlamTrigger, "targetDoor", entranceDoor);
        SetSerializedReference(doorSlamTrigger, "lightningEffect", Object.FindAnyObjectByType<LightningEffect>());
    }

    /// <summary>
    /// Builds a wall in two side segments plus a header, leaving a doorway gap.
    /// Returns the world-space centre of the doorway opening at floor level.
    /// </summary>
    private static Vector3 CreateWallWithDoorway(
        Transform parent,
        string wallName,
        Vector3 center,
        float length,
        bool alongZ,
        float doorwayCenterOnAxis,
        Material wallMaterial,
        Material beamMaterial)
    {
        float axisCenter = alongZ ? center.z : center.x;
        float start = axisCenter - length * 0.5f;
        float end = axisCenter + length * 0.5f;
        float doorStart = doorwayCenterOnAxis - DoorwayWidth * 0.5f;
        float doorEnd = doorwayCenterOnAxis + DoorwayWidth * 0.5f;

        float firstLength = Mathf.Max(0f, doorStart - start);
        float secondLength = Mathf.Max(0f, end - doorEnd);

        if (firstLength > 0.01f)
        {
            MarkNavigationStatic(CreateBlock(
                parent,
                $"{wallName} Segment A",
                SegmentCenter(center, alongZ, start + firstLength * 0.5f),
                SegmentSize(alongZ, firstLength, WallHeight),
                wallMaterial));
        }

        if (secondLength > 0.01f)
        {
            MarkNavigationStatic(CreateBlock(
                parent,
                $"{wallName} Segment B",
                SegmentCenter(center, alongZ, doorEnd + secondLength * 0.5f),
                SegmentSize(alongZ, secondLength, WallHeight),
                wallMaterial));
        }

        float headerHeight = WallHeight - DoorwayHeight;
        if (headerHeight > 0.01f)
        {
            Vector3 headerCenter = SegmentCenter(center, alongZ, doorwayCenterOnAxis);
            headerCenter.y = DoorwayHeight + headerHeight * 0.5f;
            MarkNavigationStatic(CreateBlock(
                parent,
                $"{wallName} Door Header",
                headerCenter,
                SegmentSize(alongZ, DoorwayWidth, headerHeight),
                beamMaterial));
        }

        Vector3 doorwayCenter = SegmentCenter(center, alongZ, doorwayCenterOnAxis);
        doorwayCenter.y = 0f;
        return doorwayCenter;
    }

    private static Vector3 SegmentCenter(Vector3 wallCenter, bool alongZ, float axisValue)
    {
        return alongZ
            ? new Vector3(wallCenter.x, wallCenter.y, axisValue)
            : new Vector3(axisValue, wallCenter.y, wallCenter.z);
    }

    private static Vector3 SegmentSize(bool alongZ, float axisLength, float height)
    {
        return alongZ
            ? new Vector3(WallThickness, height, axisLength)
            : new Vector3(axisLength, height, WallThickness);
    }

    /// <summary>
    /// Creates a hinge pivot with a panel child, so the Door component can rotate the pivot
    /// and swing the panel about its edge.
    /// </summary>
    private static TDoor CreateHingedDoor<TDoor>(
        Transform parent,
        string doorName,
        Vector3 doorwayCenter,
        bool alongZ,
        bool hingeAtPositiveEnd,
        Material doorMaterial) where TDoor : Door
    {
        GameObject pivot = new GameObject(doorName);
        pivot.transform.SetParent(parent);

        // Pivot forward points along the wall normal; pivot right runs along the doorway,
        // so the panel is offset by half its width on the local +X axis.
        float yaw;
        if (alongZ)
        {
            yaw = hingeAtPositiveEnd ? 90f : -90f;
        }
        else
        {
            yaw = hingeAtPositiveEnd ? 180f : 0f;
        }

        float hingeSign = hingeAtPositiveEnd ? 1f : -1f;
        Vector3 hingeOffset = alongZ
            ? new Vector3(0f, 0f, hingeSign * DoorwayWidth * 0.5f)
            : new Vector3(hingeSign * DoorwayWidth * 0.5f, 0f, 0f);

        pivot.transform.position = doorwayCenter + hingeOffset;
        pivot.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Door Panel";
        panel.transform.SetParent(pivot.transform);
        panel.transform.localPosition = new Vector3(DoorwayWidth * 0.5f, DoorwayHeight * 0.5f, 0f);
        panel.transform.localRotation = Quaternion.identity;
        panel.transform.localScale = new Vector3(DoorwayWidth - 0.04f, DoorwayHeight - 0.04f, 0.09f);
        panel.GetComponent<Renderer>().sharedMaterial = doorMaterial;

        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        handle.name = "Door Handle";
        handle.transform.SetParent(pivot.transform);
        handle.transform.localPosition = new Vector3(DoorwayWidth - 0.16f, 1.05f, 0f);
        handle.transform.localScale = new Vector3(0.11f, 0.11f, 0.16f);
        handle.GetComponent<Renderer>().sharedMaterial = doorMaterial;
        Object.DestroyImmediate(handle.GetComponent<Collider>());

        pivot.AddComponent<AudioSource>();
        return pivot.AddComponent<TDoor>();
    }

    /// <summary>Flags generated geometry so a NavMesh bake picks it up in Step 5.</summary>
    private static GameObject MarkNavigationStatic(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(target);
        GameObjectUtility.SetStaticEditorFlags(target, flags | StaticEditorFlags.NavigationStatic);
        return target;
    }

    private static GameObject CreateBlock(Transform parent, string objectName, Vector3 position, Vector3 size, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objectName;
        block.transform.SetParent(parent);
        block.transform.localPosition = position;
        block.transform.localScale = size;
        block.GetComponent<Renderer>().sharedMaterial = material;
        return block;
    }

    private static GameObject CreatePlayer(Material playerMaterial)
    {
        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 1.5f, -6.5f);

        CharacterController characterController = player.AddComponent<CharacterController>();
        characterController.height = 1.8f;
        characterController.radius = 0.34f;
        characterController.center = new Vector3(0f, 0.9f, 0f);
        characterController.slopeLimit = 45f;
        characterController.stepOffset = 0.3f;
        characterController.skinWidth = 0.03f;

        FirstPersonController controller = player.AddComponent<FirstPersonController>();
        player.AddComponent<VoidRespawn>();
        player.AddComponent<PlayerInventory>();

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Player Capsule Visual";
        visual.transform.SetParent(player.transform);
        visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = new Vector3(0.68f, 0.9f, 0.68f);
        visual.GetComponent<Renderer>().sharedMaterial = playerMaterial;
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.transform.SetParent(player.transform);
        cameraObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        cameraObject.transform.localRotation = Quaternion.identity;
        cameraObject.tag = "MainCamera";

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 72f;
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 85f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.allowHDR = true;
        cameraObject.AddComponent<AudioListener>();

        SetSerializedReference(controller, "cameraTransform", cameraObject.transform);
        return player;
    }

    private static void CreateFlashlight(Transform cameraTransform)
    {
        if (cameraTransform == null)
        {
            Debug.LogError("Could not find the generated Main Camera for the flashlight.");
            return;
        }

        GameObject flashlightObject = new GameObject("Flashlight");
        flashlightObject.transform.SetParent(cameraTransform);
        flashlightObject.transform.localPosition = new Vector3(0.2f, -0.16f, 0.3f);
        flashlightObject.transform.localRotation = Quaternion.identity;

        Light flashlightLight = flashlightObject.AddComponent<Light>();
        flashlightLight.type = LightType.Spot;
        flashlightLight.color = new Color(1f, 0.93f, 0.78f);
        flashlightLight.intensity = 4.2f;
        flashlightLight.range = 22f;
        flashlightLight.spotAngle = 52f;
        flashlightLight.innerSpotAngle = 32f;
        flashlightLight.shadows = LightShadows.Soft;
        flashlightLight.shadowStrength = 0.9f;

        Flashlight flashlight = flashlightObject.AddComponent<Flashlight>();
        SetSerializedReference(flashlight, "flashlightLight", flashlightLight);
    }

    private static void CreateMoonLight(Transform parent)
    {
        GameObject moonObject = new GameObject("Moonlight");
        moonObject.transform.SetParent(parent);
        moonObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        Light moonLight = moonObject.AddComponent<Light>();
        moonLight.type = LightType.Directional;
        moonLight.color = new Color(0.102f, 0.169f, 0.298f);
        moonLight.intensity = 0.08f;
        moonLight.shadows = LightShadows.Soft;
        moonLight.shadowStrength = 0.55f;
        moonLight.enabled = true;
    }

    private static void CreateStormLight(Transform parent)
    {
        GameObject lightningObject = new GameObject("Lightning");
        lightningObject.transform.SetParent(parent);
        lightningObject.transform.rotation = Quaternion.Euler(42f, 145f, 0f);

        Light lightningLight = lightningObject.AddComponent<Light>();
        lightningLight.type = LightType.Directional;
        lightningLight.color = new Color(0.62f, 0.76f, 1f);
        lightningLight.intensity = 0f;
        lightningLight.shadows = LightShadows.Soft;
        lightningLight.shadowStrength = 0.85f;
        lightningLight.enabled = false;

        LightningEffect lightningEffect = lightningObject.AddComponent<LightningEffect>();
        SetSerializedReference(lightningEffect, "lightningLight", lightningLight);
    }

    private static Material CreateMaterial(string materialName, Color color, float metallic, float smoothness)
    {
        EnsureFolder(MaterialFolder);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        string materialPath = $"{MaterialFolder}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = materialName
            };
            AssetDatabase.CreateAsset(material, materialPath);
        }

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetSerializedReference(Component component, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning($"Could not find serialized field '{propertyName}' on {component.GetType().Name}.");
            return;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedString(Component component, string propertyName, string value)
    {
        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning($"Could not find serialized field '{propertyName}' on {component.GetType().Name}.");
            return;
        }

        property.stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedObjectArray(Component component, string propertyName, Object[] values)
    {
        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !property.isArray)
        {
            Debug.LogWarning($"Could not find serialized object array '{propertyName}' on {component.GetType().Name}.");
            return;
        }

        property.arraySize = values == null ? 0 : values.Length;
        for (int i = 0; i < property.arraySize; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = "Assets";
        string[] segments = folderPath.Substring("Assets/".Length).Split('/');
        foreach (string segment in segments)
        {
            string current = $"{parent}/{segment}";
            if (!AssetDatabase.IsValidFolder(current))
            {
                AssetDatabase.CreateFolder(parent, segment);
            }

            parent = current;
        }
    }
}
