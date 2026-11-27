using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the complete three-specimen prep room from scratch. Run from
/// Tools/RHC7/Build Prep Game.
/// </summary>
public static class PrepGameBuilder
{
    const string ScenePath = "Assets/Scenes/RHC7_PrepGame.unity";
    const string BoneDir = "Assets/Models/GameBones";
    const string MeshDir = "Assets/Meshes/Game";
    const string MatDir = "Assets/Materials/Game";
    const string TexDir = "Assets/Textures/Game";
    const string ShaderPath = "Assets/Shaders/Game/BoneDirtUV.shader";

    enum BoneKind { TrexSkull, TrikeSkull, TrexFemur }

    [MenuItem("Tools/RHC7/Build Prep Game")]
    public static void Build()
    {
        EnsureFolders();
        ConfigureBoneImporters();
        EnsurePeriodTextures();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.34f, 0.33f, 0.30f);
        RenderSettings.fog = false;

        var floorMat = MakeTexturedLit("RoomFloor", new Color(0.95f, 0.91f, 0.86f),
            TexDir + "/Parquet.png", 0f, 0.30f, new Vector2(4f, 3.2f));
        var wallMat = MakeTexturedLit("RoomWall", new Color(0.80f, 0.76f, 0.68f),
            TexDir + "/Plaster.png", 0f, 0.08f, new Vector2(6f, 3f));
        var trimMat = MakeTexturedLit("RoomTrim", new Color(0.96f, 0.90f, 0.82f),
            "Assets/Textures/WoodDark.png", 0f, 0.32f, new Vector2(2f, 1f));
        var tableMat = MakeTexturedLit("PrepWood", new Color(1f, 0.94f, 0.86f),
            "Assets/Textures/Wood.png", 0f, 0.34f, new Vector2(1.5f, 1.5f));
        var darkMat = MakeTexturedLit("CrateWood", new Color(0.96f, 0.91f, 0.85f),
            "Assets/Textures/WoodDark.png", 0f, 0.26f, Vector2.one);
        var crateMat = MakeTexturedLit("CrateWoodLight", new Color(1f, 0.97f, 0.91f),
            "Assets/Textures/Wood.png", 0f, 0.25f, new Vector2(2f, 2f));
        var feltMat = MakeLit("PrepFelt", new Color(0.075f, 0.085f, 0.075f), 0f, 0.10f);
        var hayMat = MakeLit("Hay", new Color(0.62f, 0.47f, 0.20f), 0f, 0.05f);
        var metalMat = MakeLit("PrepMetal", new Color(0.48f, 0.35f, 0.17f), 0.78f, 0.48f);
        var boneMat = MakeBoneMaterial();

        BuildRoom(floorMat, wallMat, trimMat);
        var pad = BuildPrepTable(tableMat, darkMat, feltMat);
        var crateRoot = BuildCrateTableAndCrate(tableMat, crateMat, darkMat, feltMat, hayMat,
            out Transform lid, out Light crateLight);
        var finishedCrate = BuildFinishedCrate(tableMat, crateMat, darkMat, feltMat, hayMat,
            out List<Transform> finishedSlots);

        var bones = new List<GameBone>();
        bones.Add(SpawnBone(BoneDir + "/trex_skull.fbx", "TrexSkull",
            "Череп", "Крупный череп с тяжёлой челюстью и зубами — тираннозавр.",
            DinoSpecies.Trex, true, boneMat,
            new Vector3(1.95f, 0.875f, 0.25f), new Vector3(0f, 205f, 10f),
            new Vector3(0f, 25f, 4f), crateRoot));
        bones.Add(SpawnTrikeComplete("Assets/Meshes/Game/TrikeSkullComplete.asset",
            "TrikeSkull", "Череп с воротником",
            "Воротник и три рога — трицератопс.",
            DinoSpecies.Triceratops, true, boneMat,
            new Vector3(2.80f, 0.875f, 0.55f), new Vector3(0f, 180f, 4f),
            new Vector3(0f, 180f, 270f), crateRoot));
        bones.Add(SpawnBone(BoneDir + "/trex_femur.fbx", "TrexFemur",
            "Бедренная кость", "Длинная массивная бедренная кость — тираннозавр.",
            DinoSpecies.Trex, false, boneMat,
            new Vector3(2.35f, 0.88f, 0.0f), new Vector3(0f, 90f, 0f),
            new Vector3(0f, 70f, 0f), crateRoot));

        var tagPaper = MakeLit("NameTagPaper", new Color(0.88f, 0.82f, 0.66f), 0f, 0.06f);
        var tagThread = MakeLit("NameTagThread", new Color(0.14f, 0.09f, 0.05f), 0f, 0.1f);
        AddNameTag(bones[0], "ТИРАННОЗАВР\nЧЕРЕП", tagPaper, tagThread);
        AddNameTag(bones[1], "ТРИЦЕРАТОПС\nЧЕРЕП", tagPaper, tagThread);
        AddNameTag(bones[2], "ТИРАННОЗАВР\nБЕДРО", tagPaper, tagThread);

        var shelf = BuildTools(tableMat, metalMat, out ToolShelfInteraction toolComp);
        var referenceBook = BuildReferenceBook(darkMat, trimMat, metalMat, out Transform bookLid);
        var cleaning = BuildCleaningRig();
        var cam = BuildCamera("MainCamera");

        var flowGo = new GameObject("GameFlow");
        var flow = flowGo.AddComponent<GameFlow>();
        flow.cam = cam;

        flow.crateRoot = crateRoot;
        flow.crateLid = lid;
        flow.prepPad = pad;
        flow.referenceBook = referenceBook;
        flow.bookLid = bookLid;
        flow.finishedSlots = finishedSlots.ToArray();
        flow.finishedViewPosition = new Vector3(-2.00f, 3.80f, -0.90f);
        flow.finishedViewTarget = new Vector3(-2.30f, 1.00f, 0.42f);
        flow.crateLight = crateLight;
        flow.bones = bones.ToArray();
        flow.cleaning = cleaning;
        flow.tools = toolComp;
        // A slightly wider work view keeps the larger 1930s hall readable
        // without making the specimen too small for exact cursor cleaning.
        flow.prepViewFov = 50f;
        toolComp.cleaning = cleaning;
        toolComp.cam = cam;

        NoteImageBuilder.BuildAll(bones.ToArray(), shelf);
        var noteGo = new GameObject("PrepNotePaper");
        var noteUi = noteGo.AddComponent<PrepNoteUI>();
        noteUi.enabled = true;
        noteUi.flow = flow;
        noteUi.cleaning = cleaning;
        flow.bookTrexSkull = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Textures/Notes/NoteTrexSkull.png");
        flow.bookTrikeSkull = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Textures/Notes/NoteTrikeSkull.png");
        flow.bookTrexFemur = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Textures/Notes/NoteTrexFemur.png");
        noteUi.brushIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Textures/Notes/NoteBrush.png");
        noteUi.scraperIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Textures/Notes/NoteScraper.png");
        noteUi.chiselIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Textures/Notes/NoteChiselHammer.png");

        lighting(crateLight);
        EditorSceneManager.MarkSceneDirty(scene);
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);

        // Only the new scene remains in the build and in the project.
        string[] oldScenes = { "Assets/Scenes/CleaningCore.unity", "Assets/Scenes/RHC7_SimpleTable.unity" };
        foreach (var old in oldScenes)
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(old) != null)
                AssetDatabase.DeleteAsset(old);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PrepGame] Built " + ScenePath + " with " + bones.Count + " bones.");
    }

    // ------------------------------------------------------------- scene

    static void BuildRoom(Material floorMat, Material wallMat, Material trimMat)
    {
        // A full museum hall rather than a close-up box: 10 x 8 m, 4.6 m high.
        // Gameplay furniture keeps its tested positions; only the architecture
        // and period dressing move outward.
        const float halfW = 5f;
        const float backZ = 4.4f;
        const float frontZ = -3.6f;
        const float height = 4.6f;
        const float midZ = 0.4f;
        const float depth = 8f;

        var root = new GameObject("MuseumHall1930s").transform;
        var panelMat = MakeTexturedLit("WallPanelOak", new Color(0.91f, 0.85f, 0.76f),
            "Assets/Textures/Wood.png", 0f, 0.30f, new Vector2(1.5f, 1f));
        var ceilingMat = MakeTexturedLit("CeilingPlaster", new Color(0.67f, 0.65f, 0.60f),
            TexDir + "/Plaster.png", 0f, 0.06f, new Vector2(6f, 4f));
        var brass = MakeLit("RoomBrass", new Color(0.48f, 0.34f, 0.15f), 0.82f, 0.46f);
        var bronze = MakeLit("RoomBronze", new Color(0.13f, 0.105f, 0.07f), 0.72f, 0.38f);
        var iron = MakeLit("RadiatorIron", new Color(0.19f, 0.18f, 0.16f), 0.68f, 0.32f);
        var frameMat = MakeLit("WindowSteel", new Color(0.12f, 0.13f, 0.12f), 0.70f, 0.42f);
        var windowMat = MakeLit("WindowGlow", new Color(0.74f, 0.77f, 0.74f), 0f, 0.18f,
            new Color(0.48f, 0.51f, 0.47f) * 1.25f);
        var opal = MakeLit("LampGlow", new Color(0.90f, 0.82f, 0.66f), 0f, 0.12f,
            new Color(1.0f, 0.72f, 0.38f) * 1.15f);

        // The shell does not cast shadows: the directional daylight still fills
        // the room through the (visually solid) roof and window openings.
        ShellCube("Floor", new Vector3(0f, -0.06f, midZ), new Vector3(10f, 0.12f, depth), floorMat, root);
        ShellCube("BackWall", new Vector3(0f, height * 0.5f, backZ + 0.08f),
            new Vector3(10f, height, 0.16f), wallMat, root);
        ShellCube("FrontWall", new Vector3(0f, height * 0.5f, frontZ - 0.08f),
            new Vector3(10f, height, 0.16f), wallMat, root);
        ShellCube("LeftWall", new Vector3(-halfW - 0.08f, height * 0.5f, midZ),
            new Vector3(0.16f, height, depth), wallMat, root);
        ShellCube("RightWall", new Vector3(halfW + 0.08f, height * 0.5f, midZ),
            new Vector3(0.16f, height, depth), wallMat, root);
        ShellCube("Ceiling", new Vector3(0f, height + 0.06f, midZ),
            new Vector3(10f, 0.12f, depth), ceilingMat, root);

        // Oak wainscot, inset panels, baseboards and chair rails on all walls.
        Cube("WainscotBack", new Vector3(0f, 0.58f, backZ - 0.06f),
            new Vector3(10f, 1.16f, 0.08f), trimMat, root, false);
        Cube("WainscotFront", new Vector3(0f, 0.58f, frontZ + 0.06f),
            new Vector3(10f, 1.16f, 0.08f), trimMat, root, false);
        Cube("ChairRailBack", new Vector3(0f, 1.17f, backZ - 0.14f),
            new Vector3(10f, 0.09f, 0.14f), trimMat, root, false);
        Cube("ChairRailFront", new Vector3(0f, 1.17f, frontZ + 0.14f),
            new Vector3(10f, 0.09f, 0.14f), trimMat, root, false);
        Cube("BaseboardBack", new Vector3(0f, 0.09f, backZ - 0.13f),
            new Vector3(10f, 0.18f, 0.10f), bronze, root, false);
        Cube("BaseboardFront", new Vector3(0f, 0.09f, frontZ + 0.13f),
            new Vector3(10f, 0.18f, 0.10f), bronze, root, false);

        for (int i = 0; i < 8; i++)
        {
            float x = -4.375f + i * 1.25f;
            Cube("OakPanelBack", new Vector3(x, 0.62f, backZ - 0.115f),
                new Vector3(1.02f, 0.74f, 0.035f), panelMat, root, false);
        }
        for (int i = 0; i <= 8; i++)
        {
            float x = -5f + i * 1.25f;
            Cube("OakStileBack", new Vector3(x, 0.61f, backZ - 0.14f),
                new Vector3(0.055f, 1.02f, 0.05f), trimMat, root, false);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * halfW;
            Cube("WainscotSide", new Vector3(x - side * 0.06f, 0.58f, midZ),
                new Vector3(0.08f, 1.16f, depth), trimMat, root, false);
            Cube("ChairRailSide", new Vector3(x - side * 0.14f, 1.17f, midZ),
                new Vector3(0.14f, 0.09f, depth), trimMat, root, false);
            Cube("BaseboardSide", new Vector3(x - side * 0.13f, 0.09f, midZ),
                new Vector3(0.10f, 0.18f, depth), bronze, root, false);
            for (int i = 0; i < 6; i++)
            {
                float z = frontZ + (i + 0.5f) * (depth / 6f);
                Cube("OakPanelSide", new Vector3(x - side * 0.115f, 0.62f, z),
                    new Vector3(0.035f, 0.74f, 1.08f), panelMat, root, false);
            }
            for (int i = 0; i <= 6; i++)
            {
                float z = frontZ + i * (depth / 6f);
                Cube("OakStileSide", new Vector3(x - side * 0.14f, 0.61f, z),
                    new Vector3(0.05f, 1.02f, 0.055f), trimMat, root, false);
            }
        }

        // Stepped plaster cornice and a dark picture rail: restrained Art Deco.
        for (int i = 0; i < 3; i++)
        {
            float y = 4.18f + i * 0.13f;
            float d = 0.12f + i * 0.07f;
            Cube("CorniceBack", new Vector3(0f, y, backZ - d * 0.5f),
                new Vector3(10f, 0.12f, d), ceilingMat, root, false);
            Cube("CorniceFront", new Vector3(0f, y, frontZ + d * 0.5f),
                new Vector3(10f, 0.12f, d), ceilingMat, root, false);
            for (int side = -1; side <= 1; side += 2)
                Cube("CorniceSide", new Vector3(side * (halfW - d * 0.5f), y, midZ),
                    new Vector3(d, 0.12f, depth), ceilingMat, root, false);
        }
        Cube("PictureRailBack", new Vector3(0f, 4.04f, backZ - 0.07f),
            new Vector3(10f, 0.06f, 0.10f), trimMat, root, false);
        for (int side = -1; side <= 1; side += 2)
            Cube("PictureRailSide", new Vector3(side * (halfW - 0.07f), 4.04f, midZ),
                new Vector3(0.10f, 0.06f, depth), trimMat, root, false);

        // Fluted pilasters between the tall windows.
        foreach (float x in new[] { -4.62f, -1.52f, 1.52f, 4.62f })
        {
            Cube("Pilaster", new Vector3(x, 2.65f, backZ - 0.10f),
                new Vector3(0.18f, 2.56f, 0.12f), trimMat, root, false);
            Cube("PilasterBase", new Vector3(x, 1.31f, backZ - 0.13f),
                new Vector3(0.30f, 0.14f, 0.18f), trimMat, root, false);
            Cube("PilasterCapital", new Vector3(x, 3.96f, backZ - 0.13f),
                new Vector3(0.32f, 0.14f, 0.18f), trimMat, root, false);
            for (int f = -1; f <= 1; f++)
                Cube("PilasterFlute", new Vector3(x + f * 0.045f, 2.65f, backZ - 0.17f),
                    new Vector3(0.012f, 2.30f, 0.018f), brass, root, false);
        }

        foreach (float x in new[] { -3.05f, 0f, 3.05f })
        {
            BuildPeriodWindow(root, x, backZ, trimMat, frameMat, windowMat);
            BuildRadiator(root, x, backZ - 0.34f, iron);
        }

        BuildPeriodDoor(root, -halfW, 2.35f, trimMat, panelMat, brass, windowMat, bronze);
        BuildPendant(root, new Vector3(0.05f, 3.12f, -0.05f), brass, opal);
        BuildPendant(root, new Vector3(-2.25f, 3.12f, 0.65f), brass, opal);

        Cube("MuseumPlaque", new Vector3(3.05f, 3.91f, backZ - 0.15f),
            new Vector3(2.25f, 0.34f, 0.05f), bronze, root, false);
        AddWallText(root, "MuseumPlaqueTitle", "РХЦ-7",
            new Vector3(3.05f, 3.965f, backZ - 0.185f), Quaternion.identity,
            0.040f, new Color(0.88f, 0.80f, 0.62f));
        AddWallText(root, "MuseumPlaqueSubtitle", "ПАЛЕОНТОЛОГИЯ · 1934",
            new Vector3(3.05f, 3.845f, backZ - 0.185f), Quaternion.identity,
            0.018f, new Color(0.88f, 0.80f, 0.62f));
    }

    static GameObject ShellCube(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        var go = Cube(name, pos, scale, mat, parent, false);
        go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    static void BuildPeriodWindow(Transform root, float x, float backZ,
                                  Material trim, Material steel, Material glass)
    {
        const float y = 2.56f;
        const float w = 1.45f;
        const float h = 2.30f;
        const float z = 4.28f;

        Cube("WindowCasingL", new Vector3(x - w * 0.5f - 0.055f, y, z),
            new Vector3(0.11f, h + 0.22f, 0.12f), trim, root, false);
        Cube("WindowCasingR", new Vector3(x + w * 0.5f + 0.055f, y, z),
            new Vector3(0.11f, h + 0.22f, 0.12f), trim, root, false);
        Cube("WindowCasingT", new Vector3(x, y + h * 0.5f + 0.055f, z),
            new Vector3(w + 0.22f, 0.11f, 0.12f), trim, root, false);
        Cube("WindowCasingB", new Vector3(x, y - h * 0.5f - 0.055f, z),
            new Vector3(w + 0.22f, 0.11f, 0.12f), trim, root, false);
        Cube("WindowGlass", new Vector3(x, y, backZ - 0.10f),
            new Vector3(w, h, 0.025f), glass, root, false);

        for (int side = -1; side <= 1; side += 2)
            Cube("WindowSteelSide", new Vector3(x + side * w * 0.5f, y, z - 0.07f),
                new Vector3(0.035f, h, 0.04f), steel, root, false);
        for (int side = -1; side <= 1; side += 2)
            Cube("WindowSteelHorizontal", new Vector3(x, y + side * h * 0.5f, z - 0.07f),
                new Vector3(w, 0.035f, 0.04f), steel, root, false);
        for (int i = -1; i <= 1; i += 2)
            Cube("WindowMuntinV", new Vector3(x + i * w / 6f, y, z - 0.09f),
                new Vector3(0.026f, h, 0.035f), steel, root, false);
        for (int i = 1; i < 4; i++)
            Cube("WindowMuntinH", new Vector3(x, y - h * 0.5f + i * h / 4f, z - 0.09f),
                new Vector3(w, 0.026f, 0.035f), steel, root, false);
        Cube("WindowSill", new Vector3(x, y - h * 0.5f - 0.14f, backZ - 0.22f),
            new Vector3(w + 0.36f, 0.10f, 0.32f), trim, root, false);
    }

    static void BuildRadiator(Transform root, float x, float z, Material iron)
    {
        for (int i = 0; i < 10; i++)
            Cube("RadiatorFin", new Vector3(x - 0.52f + i * 0.115f, 0.55f, z),
                new Vector3(0.065f, 0.62f, 0.18f), iron, root, false);
        var top = Cylinder("RadiatorTopPipe", new Vector3(x, 0.88f, z), 0.035f, 1.20f, iron, root);
        Object.DestroyImmediate(top.GetComponent<Collider>());
        var bottom = Cylinder("RadiatorBottomPipe", new Vector3(x, 0.24f, z), 0.030f, 1.20f, iron, root);
        Object.DestroyImmediate(bottom.GetComponent<Collider>());
        for (int side = -1; side <= 1; side += 2)
            Cube("RadiatorFoot", new Vector3(x + side * 0.45f, 0.12f, z),
                new Vector3(0.08f, 0.24f, 0.12f), iron, root, false);
        var valve = Cylinder("RadiatorValve", new Vector3(x + 0.65f, 0.82f, z), 0.045f, 0.14f, iron, root);
        Object.DestroyImmediate(valve.GetComponent<Collider>());
    }

    static void BuildPeriodDoor(Transform root, float leftX, float z,
                                Material trim, Material panel, Material brass,
                                Material glass, Material bronze)
    {
        float x = leftX + 0.09f;
        Cube("DoorSlab", new Vector3(x, 1.25f, z), new Vector3(0.08f, 2.50f, 1.50f), trim, root, false);
        Cube("DoorFrameFront", new Vector3(leftX + 0.18f, 1.29f, z - 0.81f),
            new Vector3(0.14f, 2.58f, 0.12f), trim, root, false);
        Cube("DoorFrameBack", new Vector3(leftX + 0.18f, 1.29f, z + 0.81f),
            new Vector3(0.14f, 2.58f, 0.12f), trim, root, false);
        Cube("DoorFrameTop", new Vector3(leftX + 0.18f, 2.56f, z),
            new Vector3(0.14f, 0.12f, 1.74f), trim, root, false);
        Cube("DoorCenterStile", new Vector3(leftX + 0.15f, 1.25f, z),
            new Vector3(0.06f, 2.50f, 0.035f), bronze, root, false);
        for (int side = -1; side <= 1; side += 2)
        {
            for (int row = 0; row < 2; row++)
                Cube("DoorRaisedPanel", new Vector3(leftX + 0.145f, 0.67f + row * 1.03f, z + side * 0.38f),
                    new Vector3(0.035f, 0.76f, 0.58f), panel, root, false);
            var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            knob.name = "DoorBrassHandle";
            knob.transform.SetParent(root, false);
            knob.transform.localPosition = new Vector3(leftX + 0.23f, 1.17f, z + side * 0.10f);
            knob.transform.localScale = Vector3.one * 0.055f;
            knob.GetComponent<Renderer>().sharedMaterial = brass;
            Object.DestroyImmediate(knob.GetComponent<Collider>());
        }
        Cube("DoorTransom", new Vector3(leftX + 0.12f, 2.86f, z),
            new Vector3(0.035f, 0.42f, 1.48f), glass, root, false);
        Cube("DoorTransomBar", new Vector3(leftX + 0.17f, 2.86f, z),
            new Vector3(0.04f, 0.42f, 0.035f), bronze, root, false);
        Cube("DoorTransomFrame", new Vector3(leftX + 0.17f, 3.10f, z),
            new Vector3(0.10f, 0.08f, 1.62f), trim, root, false);
        AddWallText(root, "DoorSign", "ПРЕПАРАЦИЯ",
            new Vector3(leftX + 0.24f, 3.30f, z), Quaternion.Euler(0f, -90f, 0f),
            0.024f, new Color(0.74f, 0.65f, 0.47f));
    }

    static void BuildPendant(Transform root, Vector3 pos, Material brass, Material opal)
    {
        float rodBottom = pos.y + 0.14f;
        const float rodTop = 4.57f;
        Cube("PendantRod", new Vector3(pos.x, (rodBottom + rodTop) * 0.5f, pos.z),
            new Vector3(0.025f, rodTop - rodBottom, 0.025f), brass, root, false);
        var canopy = Cylinder("PendantCanopy", new Vector3(pos.x, 4.55f, pos.z), 0.10f, 0.06f, brass, root);
        canopy.transform.localRotation = Quaternion.identity;
        Object.DestroyImmediate(canopy.GetComponent<Collider>());
        var rim = Cylinder("PendantRim", pos + Vector3.up * 0.10f, 0.18f, 0.05f, brass, root);
        rim.transform.localRotation = Quaternion.identity;
        Object.DestroyImmediate(rim.GetComponent<Collider>());

        var shade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shade.name = "OpalPendantShade";
        shade.transform.SetParent(root, false);
        shade.transform.localPosition = pos;
        shade.transform.localScale = new Vector3(0.46f, 0.30f, 0.46f);
        shade.GetComponent<Renderer>().sharedMaterial = opal;
        Object.DestroyImmediate(shade.GetComponent<Collider>());

        var finial = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        finial.name = "PendantFinial";
        finial.transform.SetParent(root, false);
        finial.transform.localPosition = pos - Vector3.up * 0.17f;
        finial.transform.localScale = Vector3.one * 0.05f;
        finial.GetComponent<Renderer>().sharedMaterial = brass;
        Object.DestroyImmediate(finial.GetComponent<Collider>());
    }

    static void AddWallText(Transform parent, string name, string text, Vector3 pos,
                            Quaternion rotation, float characterSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rotation;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 72;
        tm.characterSize = characterSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.richText = false;
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    static Transform BuildPrepTable(Material topMat, Material darkMat, Material feltMat)
    {
        var table = new GameObject("PrepTable").transform;
        Cube("Top", new Vector3(0f, 0.89f, 0f), new Vector3(2.12f, 0.08f, 1.12f), topMat, table);
        for (int i = 0; i < 4; i++)
        {
            float x = (i & 1) == 0 ? -0.94f : 0.94f;
            float z = (i & 2) == 0 ? -0.46f : 0.46f;
            Cube("Leg", new Vector3(x, 0.44f, z), new Vector3(0.08f, 0.88f, 0.08f), darkMat, table);
        }
        // 1930s laboratory joinery: deep apron, stretchers and a brass-pull drawer.
        Cube("ApronFront", new Vector3(0f, 0.79f, -0.50f), new Vector3(1.90f, 0.16f, 0.06f), darkMat, table, false);
        Cube("ApronBack", new Vector3(0f, 0.79f, 0.50f), new Vector3(1.90f, 0.16f, 0.06f), darkMat, table, false);
        Cube("ApronLeft", new Vector3(-0.94f, 0.79f, 0f), new Vector3(0.06f, 0.16f, 0.92f), darkMat, table, false);
        Cube("ApronRight", new Vector3(0.94f, 0.79f, 0f), new Vector3(0.06f, 0.16f, 0.92f), darkMat, table, false);
        Cube("StretcherFront", new Vector3(0f, 0.22f, -0.46f), new Vector3(1.88f, 0.06f, 0.06f), darkMat, table, false);
        Cube("StretcherBack", new Vector3(0f, 0.22f, 0.46f), new Vector3(1.88f, 0.06f, 0.06f), darkMat, table, false);
        Cube("StretcherLeft", new Vector3(-0.94f, 0.22f, 0f), new Vector3(0.06f, 0.06f, 0.92f), darkMat, table, false);
        Cube("StretcherRight", new Vector3(0.94f, 0.22f, 0f), new Vector3(0.06f, 0.06f, 0.92f), darkMat, table, false);
        Cube("DrawerFront", new Vector3(0.50f, 0.79f, -0.545f), new Vector3(0.62f, 0.12f, 0.025f), topMat, table, false);
        var brass = MakeLit("TableBrass", new Color(0.48f, 0.34f, 0.15f), 0.82f, 0.46f);
        var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        knob.name = "DrawerBrassPull";
        knob.transform.SetParent(table, false);
        knob.transform.localPosition = new Vector3(0.50f, 0.79f, -0.575f);
        knob.transform.localScale = new Vector3(0.045f, 0.035f, 0.035f);
        knob.GetComponent<Renderer>().sharedMaterial = brass;
        Object.DestroyImmediate(knob.GetComponent<Collider>());
        Cube("Pad", new Vector3(0f, 0.945f, 0f), new Vector3(0.98f, 0.028f, 0.78f), feltMat, table)
            .name = "BonePadV2";
        Cube("PadRim", new Vector3(0f, 0.935f, 0f), new Vector3(1.04f, 0.018f, 0.84f), darkMat, table, false);
        return table.Find("BonePadV2");
    }

    static Transform BuildCrateTableAndCrate(Material tableMat, Material crateMat, Material darkMat,
                                             Material feltMat, Material hayMat,
                                             out Transform lid, out Light crateLight)
    {
        var table = new GameObject("CrateTable").transform;
        Cube("Top", new Vector3(2.30f, 0.71f, 0.40f), new Vector3(2.45f, 0.08f, 1.55f), tableMat, table);
        for (int i = 0; i < 4; i++)
        {
            float x = 2.30f + ((i & 1) == 0 ? -1.05f : 1.05f);
            float z = 0.40f + ((i & 2) == 0 ? -0.65f : 0.65f);
            Cube("Leg", new Vector3(x, 0.34f, z), new Vector3(0.09f, 0.68f, 0.09f), darkMat, table);
        }

        var crate = new GameObject("SpecimenCrate").transform;
        crate.position = new Vector3(2.30f, 0.75f, 0.40f);
        Cube("CrateFloor", new Vector3(0f, 0.03f, 0f), new Vector3(2.04f, 0.06f, 1.34f), crateMat, crate);
        Cube("CrateFloorIn", new Vector3(0f, 0.07f, 0f), new Vector3(1.90f, 0.03f, 1.20f), feltMat, crate, false);
        Cube("WallFront", new Vector3(0f, 0.45f, -0.68f), new Vector3(2.08f, 0.84f, 0.06f), crateMat, crate);
        Cube("WallBack", new Vector3(0f, 0.45f, 0.68f), new Vector3(2.08f, 0.84f, 0.06f), crateMat, crate);
        Cube("WallLeft", new Vector3(-1.01f, 0.45f, 0f), new Vector3(0.06f, 0.84f, 1.34f), crateMat, crate);
        Cube("WallRight", new Vector3(1.01f, 0.45f, 0f), new Vector3(0.06f, 0.84f, 1.34f), crateMat, crate);
        Cube("RailFront", new Vector3(0f, 0.87f, -0.70f), new Vector3(2.16f, 0.05f, 0.09f), darkMat, crate, false);
        Cube("RailBack", new Vector3(0f, 0.87f, 0.70f), new Vector3(2.16f, 0.05f, 0.09f), darkMat, crate, false);
        Cube("RailLeft", new Vector3(-1.04f, 0.87f, 0f), new Vector3(0.09f, 0.05f, 1.40f), darkMat, crate, false);
        Cube("RailRight", new Vector3(1.04f, 0.87f, 0f), new Vector3(0.09f, 0.05f, 1.40f), darkMat, crate, false);

        BuildHayBedding(crate, hayMat);

        var pivot = new GameObject("LidPivot").transform;
        pivot.SetParent(crate, false);
        pivot.localPosition = new Vector3(0f, 0.90f, 0.68f);
        Cube("LidPanel", new Vector3(0f, 0f, -0.68f), new Vector3(2.06f, 0.06f, 1.36f), crateMat, pivot);
        Cube("LidBattenA", new Vector3(-0.62f, 0.055f, -0.68f), new Vector3(0.07f, 0.05f, 1.36f), darkMat, pivot, false);
        Cube("LidBattenB", new Vector3(0.62f, 0.055f, -0.68f), new Vector3(0.07f, 0.05f, 1.36f), darkMat, pivot, false);
        var strapMat = MakeLit("Strap", new Color(0.25f, 0.25f, 0.24f), 0.7f, 0.5f);
        Cube("LidStrap", new Vector3(0f, 0.06f, -0.68f), new Vector3(1.0f, 0.02f, 0.05f), strapMat, pivot, false);
        lid = pivot;

        var lightGo = new GameObject("CrateLight");
        lightGo.transform.SetParent(crate, false);
        lightGo.transform.localPosition = new Vector3(0f, 1.85f, 0f);
        crateLight = lightGo.AddComponent<Light>();
        crateLight.type = LightType.Point;
        crateLight.range = 2.8f;
        crateLight.intensity = 5.0f;
        crateLight.color = new Color(1f, 0.78f, 0.50f);
        crateLight.enabled = false;

        return crate;
    }

    static void BuildHayBedding(Transform crate, Material hayMat)
    {
        EnsureHayTexture();
        var hayTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/Hay.png");
        if (hayTexture != null)
        {
            hayMat.SetTexture("_BaseMap", hayTexture);
            hayMat.SetTextureScale("_BaseMap", new Vector2(2f, 1f));
            hayMat.SetColor("_BaseColor", new Color(0.95f, 0.90f, 0.78f));
            hayMat.SetFloat("_Smoothness", 0.05f);
        }
        Cube("HayBed", new Vector3(0f, 0.095f, 0f), new Vector3(1.86f, 0.025f, 1.16f), hayMat, crate, false);

        var hayRoot = new GameObject("Hay").transform;
        hayRoot.SetParent(crate, false);
        hayRoot.localPosition = new Vector3(0f, 0.112f, 0f);

        // One mesh, five straw tones. Every stem is a slightly bent, tapered
        // triangular tube, so the bedding reads as fibres rather than rods.
        var palette = new[]
        {
            new Color(0.55f, 0.45f, 0.28f),
            new Color(0.66f, 0.56f, 0.36f),
            new Color(0.45f, 0.36f, 0.22f),
            new Color(0.74f, 0.65f, 0.45f),
            new Color(0.58f, 0.49f, 0.32f)
        };
        var materials = new Material[palette.Length];
        var triangles = new List<int>[palette.Length];
        for (int i = 0; i < palette.Length; i++)
        {
            materials[i] = MakeLit("HayStraw" + i, palette[i], 0f, 0.08f);
            triangles[i] = new List<int>(9000);
        }

        var vertices = new List<Vector3>(22000);
        var uv = new List<Vector2>(22000);
        var rng = new System.Random(1934);
        var clusterCenters = new Vector2[24];
        var clusterAngles = new float[24];
        for (int i = 0; i < clusterCenters.Length; i++)
        {
            clusterCenters[i] = new Vector2(HayRandom(rng, -0.76f, 0.76f), HayRandom(rng, -0.44f, 0.44f));
            clusterAngles[i] = HayRandom(rng, 0f, Mathf.PI * 2f);
        }

        const int strandCount = 1400;
        for (int s = 0; s < strandCount; s++)
        {
            float cx, cz, angle;
            if (rng.NextDouble() < 0.68)
            {
                int cluster = rng.Next(clusterCenters.Length);
                cx = Mathf.Clamp(clusterCenters[cluster].x + HayRandom(rng, -0.18f, 0.18f), -0.86f, 0.86f);
                cz = Mathf.Clamp(clusterCenters[cluster].y + HayRandom(rng, -0.14f, 0.14f), -0.52f, 0.52f);
                angle = clusterAngles[cluster] + HayRandom(rng, -0.85f, 0.85f);
            }
            else
            {
                cx = HayRandom(rng, -0.86f, 0.86f);
                cz = HayRandom(rng, -0.52f, 0.52f);
                angle = HayRandom(rng, 0f, Mathf.PI * 2f);
            }
            float cy = Mathf.Pow((float)rng.NextDouble(), 1.7f) * 0.075f;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 side = new Vector3(-dir.z, 0f, dir.x);
            float length = HayRandom(rng, 0.075f, 0.26f);
            if (rng.NextDouble() < 0.10) length = HayRandom(rng, 0.26f, 0.35f);
            float bend = HayRandom(rng, -0.045f, 0.045f);
            float lift = HayRandom(rng, 0.006f, 0.028f);
            if (rng.NextDouble() < 0.12) lift = HayRandom(rng, 0.035f, 0.075f);
            float tilt = HayRandom(rng, -0.018f, 0.018f);
            float radius = HayRandom(rng, 0.0010f, 0.0020f);
            float roll = HayRandom(rng, 0f, Mathf.PI * 2f);
            int tone = rng.Next(palette.Length);
            int first = vertices.Count;

            for (int j = 0; j <= 4; j++)
            {
                float t = j / 4f;
                float arc = Mathf.Sin(t * Mathf.PI);
                Vector3 center = new Vector3(cx, cy, cz)
                    + dir * (length * (t - 0.5f))
                    + side * (bend * arc)
                    + Vector3.up * (lift * arc + tilt * (t - 0.5f));
                center.x = Mathf.Clamp(center.x, -0.91f, 0.91f);
                center.z = Mathf.Clamp(center.z, -0.57f, 0.57f);
                center.y = Mathf.Max(0.002f, center.y);
                float r = radius * (1f - 0.68f * t);
                for (int k = 0; k < 3; k++)
                {
                    float a = roll + k * Mathf.PI * 2f / 3f;
                    Vector3 offset = side * (Mathf.Cos(a) * r)
                        + Vector3.up * (Mathf.Sin(a) * r * 0.28f);
                    vertices.Add(center + offset);
                    uv.Add(Vector2.zero);
                }
            }

            for (int j = 0; j < 4; j++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a = first + j * 3 + k;
                    int b = first + j * 3 + (k + 1) % 3;
                    int c = a + 3;
                    int d = b + 3;
                    triangles[tone].Add(a);
                    triangles[tone].Add(c);
                    triangles[tone].Add(b);
                    triangles[tone].Add(b);
                    triangles[tone].Add(c);
                    triangles[tone].Add(d);
                }
            }
        }

        var mesh = new Mesh { name = "HayStrandsMesh" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.subMeshCount = materials.Length;
        for (int i = 0; i < materials.Length; i++) mesh.SetTriangles(triangles[i], i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("HayStrands");
        go.transform.SetParent(hayRoot, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
    }

    static float HayRandom(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    static void EnsureHayTexture()
    {
        const int size = 512;
        var rng = new System.Random(7331);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.035f, y * 0.035f);
                float grain = HashDirt(x, y, 411);
                float shade = 0.82f + n * 0.24f + (grain - 0.5f) * 0.12f;
                pixels[y * size + x] = new Color(0.30f * shade, 0.25f * shade, 0.16f * shade, 1f);
            }
        }

        var tones = new[]
        {
            new Color(0.55f, 0.45f, 0.28f),
            new Color(0.68f, 0.57f, 0.37f),
            new Color(0.43f, 0.35f, 0.22f),
            new Color(0.74f, 0.65f, 0.45f)
        };
        for (int s = 0; s < 1500; s++)
        {
            float x0 = (float)rng.NextDouble() * size;
            float y0 = (float)rng.NextDouble() * size;
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float dx = Mathf.Cos(angle);
            float dy = Mathf.Sin(angle);
            int length = 8 + rng.Next(38);
            Color color = tones[rng.Next(tones.Length)] * HayRandom(rng, 0.72f, 1.12f);
            color.a = 1f;
            for (int i = 0; i < length; i++)
            {
                int x = ((int)(x0 + dx * i) % size + size) % size;
                int y = ((int)(y0 + dy * i) % size + size) % size;
                pixels[y * size + x] = color;
                if ((i & 1) == 0)
                {
                    int sideX = (x + (dy > 0f ? 1 : -1) + size) % size;
                    pixels[y * size + sideX] = color * 0.88f;
                }
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply();
        WritePeriodTexture(TexDir + "/Hay.png", texture);
    }

    static Transform BuildFinishedCrate(Material tableMat, Material crateMat, Material darkMat,
                                        Material feltMat, Material hayMat, out List<Transform> slots)
    {
        var table = new GameObject("FinishedCrateTable").transform;
        Cube("Top", new Vector3(-2.30f, 0.71f, 0.40f), new Vector3(2.45f, 0.08f, 1.55f), tableMat, table);
        for (int i = 0; i < 4; i++)
        {
            float x = -2.30f + ((i & 1) == 0 ? -1.05f : 1.05f);
            float z = 0.40f + ((i & 2) == 0 ? -0.65f : 0.65f);
            Cube("Leg", new Vector3(x, 0.34f, z), new Vector3(0.09f, 0.68f, 0.09f), darkMat, table);
        }

        var crate = new GameObject("FinishedCrate").transform;
        crate.position = new Vector3(-2.30f, 0.75f, 0.40f);
        Cube("CrateFloor", new Vector3(0f, 0.03f, 0f), new Vector3(2.04f, 0.06f, 1.34f), crateMat, crate);
        Cube("CrateFloorIn", new Vector3(0f, 0.07f, 0f), new Vector3(1.90f, 0.03f, 1.20f), feltMat, crate, false);
        Cube("WallFront", new Vector3(0f, 0.45f, -0.68f), new Vector3(2.08f, 0.84f, 0.06f), crateMat, crate);
        Cube("WallBack", new Vector3(0f, 0.45f, 0.68f), new Vector3(2.08f, 0.84f, 0.06f), crateMat, crate);
        Cube("WallLeft", new Vector3(-1.01f, 0.45f, 0f), new Vector3(0.06f, 0.84f, 1.34f), crateMat, crate);
        Cube("WallRight", new Vector3(1.01f, 0.45f, 0f), new Vector3(0.06f, 0.84f, 1.34f), crateMat, crate);
        Cube("RailFront", new Vector3(0f, 0.87f, -0.70f), new Vector3(2.16f, 0.05f, 0.09f), darkMat, crate, false);
        Cube("RailBack", new Vector3(0f, 0.87f, 0.70f), new Vector3(2.16f, 0.05f, 0.09f), darkMat, crate, false);
        Cube("RailLeft", new Vector3(-1.04f, 0.87f, 0f), new Vector3(0.09f, 0.05f, 1.40f), darkMat, crate, false);
        Cube("RailRight", new Vector3(1.04f, 0.87f, 0f), new Vector3(0.09f, 0.05f, 1.40f), darkMat, crate, false);
        Cube("HayBed", new Vector3(0f, 0.095f, 0f), new Vector3(1.86f, 0.025f, 1.16f), hayMat, crate, false);

        var hayRoot = new GameObject("Hay").transform;
        hayRoot.SetParent(crate, false);
        hayRoot.localPosition = new Vector3(0f, 0.11f, 0f);
        for (int i = 0; i < 90; i++)
        {
            var s = Cylinder("Straw", Vector3.zero, 0.0035f, Random.Range(0.10f, 0.24f),
                hayMat, hayRoot);
            s.transform.localPosition = new Vector3(Random.Range(-0.90f, 0.90f),
                Random.Range(-0.01f, 0.08f), Random.Range(-0.56f, 0.56f));
            s.transform.localRotation = Quaternion.Euler(Random.Range(-6f, 6f),
                Random.Range(0f, 180f), Random.Range(-12f, 12f));
            Object.DestroyImmediate(s.GetComponent<Collider>());
        }

        var pivot = new GameObject("LidPivot").transform;
        pivot.SetParent(crate, false);
        pivot.localPosition = new Vector3(0f, 0.90f, 0.68f);
        pivot.localRotation = Quaternion.Euler(105f, 0f, 0f);
        Cube("LidPanel", new Vector3(0f, 0f, -0.68f), new Vector3(2.06f, 0.06f, 1.36f), crateMat, pivot);
        Cube("LidBattenA", new Vector3(-0.62f, 0.055f, -0.68f), new Vector3(0.07f, 0.05f, 1.36f), darkMat, pivot, false);
        Cube("LidBattenB", new Vector3(0.62f, 0.055f, -0.68f), new Vector3(0.07f, 0.05f, 1.36f), darkMat, pivot, false);

        slots = new List<Transform>();
        for (int i = 0; i < 3; i++)
        {
            var slot = new GameObject("FinishedSlot" + (i + 1)).transform;
            slot.SetParent(crate, false);
            slot.localPosition = new Vector3(
                i == 0 ? -0.35f : (i == 1 ? 0.50f : 0.05f),
                i == 2 ? 0.13f : 0.125f,
                i == 0 ? -0.15f : (i == 1 ? 0.15f : -0.40f));
            slot.localRotation = i == 0
                ? Quaternion.Euler(0f, 205f, 10f)
                : (i == 1 ? Quaternion.Euler(0f, 180f, 4f) : Quaternion.Euler(0f, 90f, 0f));
            slots.Add(slot);
        }
        return crate;
    }

    static void AddNameTag(GameBone bone, string text, Material paper, Material threadMat)
    {
        if (bone == null) return;
        var go = new GameObject(bone.name + "_NameTag");
        var tag = go.AddComponent<SpecimenTag>();
        tag.bone = bone;
        tag.rise = 0.13f;
        tag.side = 0.12f;

        var card = GameObject.CreatePrimitive(PrimitiveType.Cube);
        card.name = "TagCard";
        card.transform.SetParent(go.transform, false);
        card.transform.localPosition = Vector3.zero;
        card.transform.localScale = new Vector3(0.27f, 0.14f, 0.004f);
        card.GetComponent<Renderer>().sharedMaterial = paper;
        Object.DestroyImmediate(card.GetComponent<Collider>());

        var hole = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hole.name = "TagHole";
        hole.transform.SetParent(go.transform, false);
        hole.transform.localPosition = new Vector3(-0.11f, 0.052f, -0.004f);
        hole.transform.localScale = Vector3.one * 0.014f;
        hole.GetComponent<Renderer>().sharedMaterial = threadMat;
        Object.DestroyImmediate(hole.GetComponent<Collider>());

        AddWallText(go.transform, "TagText", text, new Vector3(0f, 0f, -0.004f),
            Quaternion.identity, 0.0043f, new Color(0.18f, 0.11f, 0.06f));

        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = 0.0032f;
        line.sharedMaterial = threadMat;
        line.startColor = new Color(0.20f, 0.13f, 0.08f);
        line.endColor = line.startColor;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;

        go.SetActive(false);
        bone.nameTag = tag;
    }

    static Transform BuildReferenceBook(Material leather, Material trimMat,
                                        Material brass, out Transform lid)
    {
        var root = new GameObject("ReferenceBook").transform;
        root.position = new Vector3(-0.78f, 0.934f, 0.02f);
        root.rotation = Quaternion.Euler(0f, -7f, 0f);

        var pages = MakeLit("BookPages", new Color(0.84f, 0.79f, 0.65f), 0f, 0.08f);
        var ribbon = MakeLit("BookRibbon", new Color(0.42f, 0.10f, 0.07f), 0f, 0.16f);

        Cube("BookBackCover", new Vector3(0f, 0.014f, 0f),
            new Vector3(0.36f, 0.028f, 0.28f), leather, root, false);
        Cube("BookPages", new Vector3(0.005f, 0.035f, 0f),
            new Vector3(0.33f, 0.026f, 0.25f), pages, root, false);
        Cube("BookSpine", new Vector3(0f, 0.036f, 0.14f),
            new Vector3(0.36f, 0.05f, 0.025f), leather, root, false);
        Cube("BookRibbon", new Vector3(0.10f, 0.055f, -0.24f),
            new Vector3(0.028f, 0.005f, 0.11f), ribbon, root, false);

        var lidPivot = new GameObject("BookLid").transform;
        lidPivot.SetParent(root, false);
        lidPivot.localPosition = new Vector3(0f, 0.052f, 0.14f);
        Cube("BookFrontCover", new Vector3(0f, 0f, -0.14f),
            new Vector3(0.36f, 0.018f, 0.28f), leather, lidPivot, false);
        Cube("BookFrontPages", new Vector3(0.005f, -0.013f, -0.14f),
            new Vector3(0.33f, 0.012f, 0.25f), pages, lidPivot, false);
        for (int x = -1; x <= 1; x += 2)
        {
            Cube("BookCorner", new Vector3(x * 0.157f, 0.012f, -0.14f + x * 0.115f),
                new Vector3(0.045f, 0.014f, 0.045f), brass, lidPivot, false);
        }
        AddWallText(lidPivot, "BookTitle", "ОПРЕДЕЛИТЕЛЬ",
            new Vector3(0f, 0.012f, -0.145f), Quaternion.Euler(-90f, 0f, 0f),
            0.0045f, new Color(0.78f, 0.65f, 0.38f));

        var collider = root.gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.04f, 0f);
        collider.size = new Vector3(0.38f, 0.10f, 0.30f);
        lid = lidPivot;
        return root;
    }

    static List<Transform> BuildDisplayShelf(Material darkMat, Material trimMat, Material metalMat)
    {
        var root = new GameObject("DisplayShelf").transform;
        root.position = new Vector3(-2.25f, 0f, 1.05f);
        Cube("Back", new Vector3(0f, 1.55f, 0.35f), new Vector3(2.2f, 1.3f, 0.06f), darkMat, root);
        Cube("Base", new Vector3(0f, 0.75f, 0f), new Vector3(2.2f, 0.08f, 0.55f), trimMat, root);
        Cube("Top", new Vector3(0f, 2.22f, 0f), new Vector3(2.2f, 0.06f, 0.55f), trimMat, root);
        Cube("SideL", new Vector3(-1.08f, 1.48f, 0f), new Vector3(0.04f, 1.35f, 0.48f), trimMat, root, false);
        Cube("SideR", new Vector3(1.08f, 1.48f, 0f), new Vector3(0.04f, 1.35f, 0.48f), trimMat, root, false);
        Cube("DecoTopStepA", new Vector3(0f, 2.28f, 0f), new Vector3(2.28f, 0.06f, 0.59f), trimMat, root, false);
        Cube("DecoTopStepB", new Vector3(0f, 2.34f, 0f), new Vector3(2.36f, 0.06f, 0.63f), trimMat, root, false);
        Cube("DecoBaseStep", new Vector3(0f, 0.69f, 0f), new Vector3(2.30f, 0.06f, 0.61f), trimMat, root, false);
        for (int side = -1; side <= 1; side += 2)
            for (int f = -1; f <= 1; f++)
                Cube("DecoFlute", new Vector3(side * 1.105f, 1.52f, f * 0.10f),
                    new Vector3(0.018f, 1.22f, 0.018f), metalMat, root, false);
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 0.68f;
            Cube("Stand", new Vector3(x, 0.82f, 0.02f), new Vector3(0.40f, 0.04f, 0.32f), metalMat, root, false);
            var slot = new GameObject("DisplaySlot" + (i + 1)).transform;
            slot.SetParent(root, false);
            slot.localPosition = new Vector3(x, 0.84f, 0.02f);
            slot.localRotation = i == 0
                ? Quaternion.Euler(0f, 20f, 4f)
                : (i == 1 ? Quaternion.Euler(0f, 180f, 4f) : Quaternion.Euler(0f, 70f, 0f));
        }
        var slots = new List<Transform>();
        for (int i = 1; i <= 3; i++) slots.Add(root.Find("DisplaySlot" + i));
        return slots;
    }

    static Transform BuildTools(Material woodMat, Material metalMat, out ToolShelfInteraction toolComp)
    {
        var shelf = new GameObject("ToolShelf").transform;
        shelf.position = new Vector3(0.73f, 0.955f, -0.16f);
        Cube("TrayBase", Vector3.zero, new Vector3(0.62f, 0.025f, 0.34f), woodMat, shelf);
        Cube("TrayFelt", new Vector3(0f, 0.018f, 0f), new Vector3(0.58f, 0.008f, 0.30f),
            MakeLit("ToolFelt", new Color(0.07f, 0.08f, 0.07f), 0f, 0.1f), shelf, false);
        Cube("TrayRimF", new Vector3(0f, 0.02f, -0.17f), new Vector3(0.62f, 0.04f, 0.015f), woodMat, shelf, false);
        Cube("TrayRimB", new Vector3(0f, 0.02f, 0.17f), new Vector3(0.62f, 0.04f, 0.015f), woodMat, shelf, false);
        Cube("TrayRimL", new Vector3(-0.31f, 0.02f, 0f), new Vector3(0.015f, 0.04f, 0.34f), woodMat, shelf, false);
        Cube("TrayRimR", new Vector3(0.31f, 0.02f, 0f), new Vector3(0.015f, 0.04f, 0.34f), woodMat, shelf, false);

        // All roots sit near the back of the felt so every tool lies fully
        // inside the tray instead of hanging over the front rim.
        var brush = MakeToolRoot(shelf, "PrepBrush", new Vector3(-0.23f, 0.04f, 0.114f),
            Quaternion.Euler(0f, 180f, 90f), "Assets/Models/Tools/Tool_Brush.fbx",
            new[] { "Assets/Materials/PrepTools/Wood.mat", "Assets/Materials/PrepTools/Brass.mat",
                    "Assets/Materials/PrepTools/Bristle.mat" });
        var scraper = MakeToolRoot(shelf, "PrepScraper", new Vector3(-0.09f, 0.046f, 0.114f),
            Quaternion.Euler(0f, 180f, 0f), "Assets/Models/Tools/Tool_Scraper.fbx",
            new[] { "Assets/Materials/PrepTools/Wood.mat", "Assets/Materials/PrepTools/Steel.mat",
                    "Assets/Materials/PrepTools/Brass.mat" });
        var chisel = MakeToolRoot(shelf, "PrepChisel", new Vector3(0.05f, 0.068f, 0.114f),
            Quaternion.Euler(0f, 180f, 0f), "Assets/Models/Tools/Tool_Chisel.fbx",
            new[] { "Assets/Materials/PrepTools/Steel.mat" });
        AddHammerPair(chisel);

        toolComp = shelf.gameObject.AddComponent<ToolShelfInteraction>();
        toolComp.pickDistance = 8f;
        toolComp.chiselHammer = chisel.Find("HammerPivot");
        return shelf;
    }

    static Transform MakeToolRoot(Transform parent, string name, Vector3 localPos,
                                  Quaternion localRot, string prefabPath, string[] matPaths)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.localPosition = localPos;
        root.localRotation = localRot;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return root;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
        go.name = name + "_Visual";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = prefab.transform.localScale;

        foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        var mats = new List<Material>();
        foreach (var p in matPaths)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m != null) mats.Add(m);
        }
        if (mats.Count > 0)
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterials = mats.ToArray();

        var bc = root.gameObject.AddComponent<BoxCollider>();
        FitCollider(root, bc);
        return root;
    }

    static void AddHammerPair(Transform chiselRoot)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Tools/Tool_Hammer.fbx");
        if (prefab == null)
        {
            Debug.LogWarning("[PrepGame] Tool_Hammer.fbx missing; chisel will stay unpaired.");
            return;
        }

        var pivot = new GameObject("HammerPivot").transform;
        pivot.SetParent(chiselRoot, false);
        pivot.localPosition = new Vector3(-0.145f, -0.021f, 0f);
        pivot.localRotation = Quaternion.identity;
        pivot.localScale = Vector3.one * 0.8f;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pivot);
        go.name = "HammerTool";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = prefab.transform.localScale;
        foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

        var mats = new List<Material>();
        var steel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PrepTools/Steel.mat");
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PrepTools/Wood.mat");
        if (steel != null) mats.Add(steel);
        if (wood != null) mats.Add(wood);
        if (mats.Count > 0)
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterials = mats.ToArray();

        var hbc = pivot.gameObject.GetComponent<BoxCollider>();
        if (hbc == null) hbc = pivot.gameObject.AddComponent<BoxCollider>();
        FitCollider(pivot, hbc);
    }

    static void FitCollider(Transform root, BoxCollider bc)
    {
        bool has = false;
        Bounds b = default;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            var bd = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = new Vector3(
                    (i & 1) == 0 ? bd.min.x : bd.max.x,
                    (i & 2) == 0 ? bd.min.y : bd.max.y,
                    (i & 4) == 0 ? bd.min.z : bd.max.z);
                Vector3 p = root.InverseTransformPoint(c);
                if (!has) { b = new Bounds(p, Vector3.zero); has = true; }
                else b.Encapsulate(p);
            }
        }
        if (has)
        {
            bc.center = b.center;
            bc.size = b.size + new Vector3(0.008f, 0.008f, 0.008f);
        }
    }

    static BoneCleaningV2 BuildCleaningRig()
    {
        var go = new GameObject("CleaningRig");
        var c = go.AddComponent<BoneCleaningV2>();
        c.showHud = false;
        c.tool = BoneCleaningV2.Tool.Dust;
        c.radiusWorld = 0.06f;
        c.strength = 10f;
        c.minDistance = 0.28f;
        c.maxDistanceZoom = 3.0f;
        c.autoCleanThreshold = 0.10f;
        c.enabled = false;
        return c;
    }

    static Camera BuildCamera(string name)
    {
        var go = new GameObject(name);
        go.tag = "MainCamera";
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.03f;
        cam.farClipPlane = 30f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.075f, 0.065f, 0.055f);
        go.AddComponent<AudioListener>();
        cam.transform.position = new Vector3(2.00f, 3.80f, -0.90f);
        cam.transform.rotation = Quaternion.LookRotation(
            (new Vector3(2.30f, 1.00f, 0.42f) - cam.transform.position).normalized, Vector3.up);
        return cam;
    }

    static void lighting(Light crateLight)
    {
        var sunGo = new GameObject("Sun");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.65f;
        sun.color = new Color(1f, 0.93f, 0.82f);
        sun.shadows = LightShadows.Soft;
        sunGo.transform.rotation = Quaternion.Euler(42f, -28f, 0f);

        var fillGo = new GameObject("FillLight");
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.35f;
        fill.color = new Color(0.78f, 0.84f, 0.95f);
        fill.shadows = LightShadows.None;
        fillGo.transform.rotation = Quaternion.Euler(18f, 160f, 0f);

        AddPoint("PrepLamp", new Vector3(0.05f, 2.85f, -0.05f), new Color(1f, 0.88f, 0.72f), 6.5f, 5.0f);
        AddPoint("CrateLamp", new Vector3(2.30f, 2.25f, 0.4f), new Color(1f, 0.86f, 0.68f), 5.0f, 3.8f);
        AddPoint("DisplayLamp", new Vector3(-2.25f, 2.85f, 0.65f), new Color(1f, 0.91f, 0.78f), 4.5f, 3.4f);
        if (crateLight != null) crateLight.intensity = 3.0f;
    }

    static void AddPoint(string name, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.Soft;
    }

    // ------------------------------------------------------------- bones

    static GameBone SpawnBone(string path, string name, string part, string hint,
                              DinoSpecies species, bool isSkull, Material mat,
                              Vector3 cratePos, Vector3 crateEuler, Vector3 prepEuler,
                              Transform crate)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError("[PrepGame] Missing bone " + path);
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        go.transform.SetParent(crate, true);
        go.transform.position = cratePos;
        go.transform.rotation = Quaternion.Euler(crateEuler);

        var mf = go.GetComponentInChildren<MeshFilter>();
        var mr = mf != null ? mf.GetComponent<MeshRenderer>() : go.GetComponentInChildren<MeshRenderer>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError("[PrepGame] Bone has no mesh: " + name);
            return null;
        }

        var src = mf.sharedMesh;
        var copy = Object.Instantiate(src);
        copy.name = name + "_Mesh";

        // FBX imports arrive with a 100x root scale. Bake it into the mesh so
        // the cleaning ray test works in metre-scale local space (tiny local
        // triangles otherwise fall below the triangle epsilon).
        float inheritedScale = go.transform.lossyScale.x;
        if (Mathf.Abs(inheritedScale - 1f) > 0.001f)
        {
            var verts = copy.vertices;
            for (int i = 0; i < verts.Length; i++) verts[i] *= inheritedScale;
            copy.vertices = verts;
            copy.RecalculateBounds();
            go.transform.localScale = Vector3.one;
        }

        PrepareMesh(copy, go.transform, name);
        string assetPath = MeshDir + "/" + name + ".asset";
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(copy, assetPath);
        mf.sharedMesh = copy;
        if (mr != null) mr.sharedMaterial = mat;

        foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        var bc = go.AddComponent<BoxCollider>();
        bc.center = copy.bounds.center;
        bc.size = copy.bounds.size * 1.03f;

        var gb = go.AddComponent<GameBone>();
        gb.displayName = part;
        gb.partName = part;
        gb.identificationHint = hint;
        gb.species = species;
        gb.isSkull = isSkull;
        gb.prepEuler = prepEuler;
        gb.boneRenderer = mr;
        gb.boneMesh = mf;
        gb.boneMaterial = mat;
        gb.Cache();

        // Sit the bone on the hay, not with its pivot at the hay height.
        float bottomTarget = crate != null ? crate.position.y + 0.13f : 0.88f;
        Bounds placed = gb.WorldBounds();
        Vector3 p = go.transform.position;
        p.y += bottomTarget - placed.min.y;
        go.transform.position = p;
        return gb;
    }

    static GameBone SpawnTrikeComplete(string meshPath, string name, string part, string hint,
                                        DinoSpecies species, bool isSkull, Material mat,
                                        Vector3 cratePos, Vector3 crateEuler, Vector3 prepEuler,
                                        Transform crate)
    {
        var src = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (src == null)
        {
            Debug.LogError("[PrepGame] Missing complete Trike skull mesh " + meshPath);
            return null;
        }

        var go = new GameObject(name);
        go.transform.SetParent(crate, true);
        go.transform.position = cratePos;
        go.transform.rotation = Quaternion.Euler(crateEuler);
        go.transform.localScale = Vector3.one;

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        var copy = Object.Instantiate(src);
        copy.name = name + "_Mesh";

        var verts = copy.vertices;
        Vector3 center = copy.bounds.center;
        Vector3 size = copy.bounds.size;
        float maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float scale = maxDim > 0.0001f ? 0.68f / maxDim : 1f;
        for (int i = 0; i < verts.Length; i++)
            verts[i] = (verts[i] - center) * scale;
        copy.vertices = verts;
        copy.RecalculateBounds();
        PrepareMesh(copy, go.transform, name);
        string assetPath = MeshDir + "/" + name + ".asset";
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(copy, assetPath);
        mf.sharedMesh = copy;
        mr.sharedMaterial = mat;

        var bc = go.AddComponent<BoxCollider>();
        bc.center = copy.bounds.center;
        bc.size = copy.bounds.size * 1.03f;

        var gb = go.AddComponent<GameBone>();
        gb.displayName = part;
        gb.partName = part;
        gb.identificationHint = hint;
        gb.species = species;
        gb.isSkull = isSkull;
        gb.prepEuler = prepEuler;
        gb.boneRenderer = mr;
        gb.boneMesh = mf;
        gb.boneMaterial = mat;
        gb.Cache();

        float bottomTarget = crate != null ? crate.position.y + 0.13f : 0.88f;
        Bounds placed = gb.WorldBounds();
        Vector3 p = go.transform.position;
        p.y += bottomTarget - placed.min.y;
        go.transform.position = p;
        return gb;
    }

    static void PrepareMesh(Mesh mesh, Transform tr, string seedKey)
    {
        var verts = mesh.vertices;
        var normals = mesh.normals;
        if (normals == null || normals.Length != verts.Length)
        {
            mesh.RecalculateNormals();
            normals = mesh.normals;
        }
        if (mesh.uv == null || mesh.uv.Length != verts.Length)
            GenerateUv0(mesh, verts, normals);

        Vector3 size = mesh.bounds.size;
        float maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        if (maxDim < 0.0001f) maxDim = 1f;
        Vector3 center = mesh.bounds.center;

        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < verts.Length; i++)
        {
            float y = tr.TransformPoint(verts[i]).y;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        int seed = 17;
        unchecked
        {
            for (int i = 0; i < seedKey.Length; i++) seed = seed * 31 + seedKey[i];
        }
        float offBase = (seed & 1023) * 0.0137f;
        Vector3 off = new Vector3(offBase, offBase * 0.73f, offBase * 1.31f);

        var colors = new Color32[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 q = (verts[i] - center) / maxDim;
            Vector3 wp = tr.TransformPoint(verts[i]);
            Vector3 wn = tr.TransformDirection(normals[i]).normalized;
            float up = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(wn.y * 0.5f + 0.5f));
            float down = 1f - up;
            float height = Mathf.InverseLerp(minY, maxY, wp.y);

            float n1 = Fbm(q * 14f + off + new Vector3(3.1f, 7.7f, 1.3f), 4);
            float n2 = Fbm(q * 30f + off + new Vector3(8.4f, 2.9f, 5.6f), 4);
            float n3 = Fbm(q * 10f + off + new Vector3(11.2f, 5.5f, 9.1f), 3);
            float n4 = Fbm(q * 66f + off + new Vector3(1.7f, 4.3f, 6.8f), 2);

            float dustGate = DirtStep(0.36f, 0.54f, n1);
            float dust = dustGate * (0.42f + 0.18f * n4) * (0.85f + 0.15f * up);
            dust *= 1f - 0.18f * DirtStep(0.40f, 0.90f, height) * up;

            float clayGate = DirtStep(0.40f, 0.60f, n2);
            float clay = clayGate * (0.45f + 0.40f * DirtStep(0.52f, 0.74f, n2))
                * (0.76f + 0.24f * (1f - up));
            clay += 0.06f * down * clayGate;

            float crustGate = DirtStep(0.44f, 0.64f, n3);
            float crust = crustGate * (0.35f + 0.50f * DirtStep(0.56f, 0.76f, n3))
                * (0.42f + 0.58f * down);
            crust += 0.08f * down * DirtStep(0.52f, 0.72f, n2);

            colors[i] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(dust * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(clay * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(crust * 255f), 0, 255),
                255);
        }
        mesh.colors32 = colors;
        mesh.RecalculateTangents();
        EditorUtility.SetDirty(mesh);
    }

    static void GenerateUv0(Mesh mesh, Vector3[] verts, Vector3[] normals)
    {
        Vector3 size = mesh.bounds.size;
        float maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        if (maxDim < 0.0001f) maxDim = 1f;
        var uv = new Vector2[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 n = normals[i];
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            float u, v;
            if (ay >= ax && ay >= az) { u = verts[i].x; v = verts[i].z; }
            else if (ax >= az) { u = verts[i].z; v = verts[i].y; }
            else { u = verts[i].x; v = verts[i].y; }
            uv[i] = new Vector2(u / maxDim + 0.5f, v / maxDim + 0.5f);
        }
        mesh.SetUVs(0, uv);
    }

    // ------------------------------------------------------------- helpers

    static Material MakeBoneMaterial()
    {
        var shader = Shader.Find("RHC7/Bone Dirt UV");
        if (shader == null)
        {
            Debug.LogError("[PrepGame] Bone dirt shader not found.");
            return MakeLit("BoneFallback", new Color(0.78f, 0.74f, 0.64f), 0f, 0.25f);
        }
        string path = MatDir + "/BoneCleanable.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetColor("_CleanBoneColor", new Color(0.78f, 0.74f, 0.63f));
        mat.SetColor("_DustColor", new Color(0.38f, 0.35f, 0.29f));
        mat.SetColor("_ClayColor", new Color(0.25f, 0.16f, 0.09f));
        mat.SetColor("_CrustColor", new Color(0.12f, 0.11f, 0.085f));
        mat.SetFloat("_CleanBoneBlend", 0.94f);
        mat.SetFloat("_BoneSmoothness", 0.28f);
        mat.SetFloat("_DustOpacity", 0.55f);
        mat.SetFloat("_DirtNormalStrength", 0f);
        mat.SetFloat("_ProceduralDirtBump", 0.15f);
        mat.SetTexture("_BoneAlbedo", null);
        mat.SetTexture("_BoneNormal", null);
        mat.SetTexture("_BoneAO", null);
        mat.SetTexture("_DirtMask", null);
        mat.SetTexture("_DirtNormal", null);
        mat.SetFloat("_DebugMode", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material MakeTexturedLit(string name, Color color, string texturePath,
                                    float metallic, float smoothness, Vector2 tiling)
    {
        var mat = MakeLit(name, color, metallic, smoothness);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture != null)
        {
            mat.SetTexture("_BaseMap", texture);
            mat.SetTextureScale("_BaseMap", tiling);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void EnsurePeriodTextures()
    {
        Directory.CreateDirectory(TexDir);
        const int plasterSize = 512;
        var plaster = new Texture2D(plasterSize, plasterSize, TextureFormat.RGBA32, false);
        var plasterPixels = new Color[plasterSize * plasterSize];
        for (int y = 0; y < plasterSize; y++)
        {
            for (int x = 0; x < plasterSize; x++)
            {
                float coarse = Mathf.PerlinNoise(x * 0.018f, y * 0.018f);
                float fine = HashDirt(x, y, 31);
                float shade = 0.80f + coarse * 0.10f + (fine - 0.5f) * 0.055f;
                plasterPixels[y * plasterSize + x] = new Color(
                    shade * 1.02f, shade, shade * 0.96f, 1f);
            }
        }
        plaster.SetPixels(plasterPixels);
        plaster.Apply();
        WritePeriodTexture(TexDir + "/Plaster.png", plaster);

        const int parquetSize = 1024;
        const int rowHeight = 64;
        const int plankLength = 256;
        var parquet = new Texture2D(parquetSize, parquetSize, TextureFormat.RGBA32, false);
        var parquetPixels = new Color[parquetSize * parquetSize];
        for (int y = 0; y < parquetSize; y++)
        {
            int row = y / rowHeight;
            int py = y % rowHeight;
            int offset = (row & 1) * (plankLength / 2);
            for (int x = 0; x < parquetSize; x++)
            {
                int shifted = x + offset;
                int plank = (shifted / plankLength) & 3;
                int px = shifted % plankLength;
                float variation = HashDirt(plank, row, 91);
                float grain = Mathf.PerlinNoise(px * 0.018f,
                    py * 0.10f + plank * 3.7f + row * 1.3f);
                float fine = HashDirt(x, y, 13);
                float tone = 0.84f + variation * 0.28f +
                             (grain - 0.5f) * 0.30f + (fine - 0.5f) * 0.08f;
                int edge = Mathf.Min(Mathf.Min(px, plankLength - 1 - px),
                    Mathf.Min(py, rowHeight - 1 - py));
                if (edge < 2) tone *= 0.42f;
                else if (edge < 7) tone *= Mathf.Lerp(0.76f, 1f, (edge - 2) / 5f);

                float r = 0.42f + variation * 0.10f;
                float g = 0.265f + variation * 0.065f;
                float b = 0.145f + variation * 0.035f;
                parquetPixels[y * parquetSize + x] = new Color(r * tone, g * tone, b * tone, 1f);
            }
        }
        parquet.SetPixels(parquetPixels);
        parquet.Apply();
        WritePeriodTexture(TexDir + "/Parquet.png", parquet);
    }

    static void WritePeriodTexture(string path, Texture2D texture)
    {
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
    }

    static Material MakeLit(string name, Color color, float metallic, float smoothness, Color? emission = null)
    {
        string path = MatDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static GameObject Cube(string name, Vector3 pos, Vector3 scale, Material mat,
                           Transform parent = null, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Cylinder(string name, Vector3 pos, float radius, float height,
                               Material mat, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static float DirtStep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / Mathf.Max(0.00001f, edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    static float HashDirt(int x, int y, int z)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u +
                     (uint)z * 1274126177u + 1337u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / 16777215f;
        }
    }

    static float ValueNoiseDirt(Vector3 p)
    {
        int ix = Mathf.FloorToInt(p.x);
        int iy = Mathf.FloorToInt(p.y);
        int iz = Mathf.FloorToInt(p.z);
        float fx = p.x - ix, fy = p.y - iy, fz = p.z - iz;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        fz = fz * fz * (3f - 2f * fz);
        float n000 = HashDirt(ix, iy, iz);
        float n100 = HashDirt(ix + 1, iy, iz);
        float n010 = HashDirt(ix, iy + 1, iz);
        float n110 = HashDirt(ix + 1, iy + 1, iz);
        float n001 = HashDirt(ix, iy, iz + 1);
        float n101 = HashDirt(ix + 1, iy, iz + 1);
        float n011 = HashDirt(ix, iy + 1, iz + 1);
        float n111 = HashDirt(ix + 1, iy + 1, iz + 1);
        float nx00 = Mathf.Lerp(n000, n100, fx);
        float nx10 = Mathf.Lerp(n010, n110, fx);
        float nx01 = Mathf.Lerp(n001, n101, fx);
        float nx11 = Mathf.Lerp(n011, n111, fx);
        return Mathf.Lerp(Mathf.Lerp(nx00, nx10, fy), Mathf.Lerp(nx01, nx11, fy), fz);
    }

    static float Fbm(Vector3 p, int octaves)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += ValueNoiseDirt(p) * amp;
            norm += amp;
            p = p * 2.03f + new Vector3(17.1f, 9.2f, 13.7f);
            amp *= 0.5f;
        }
        return sum / Mathf.Max(0.0001f, norm);
    }

    static void ConfigureBoneImporters()
    {
        string[] paths =
        {
            BoneDir + "/trex_skull.fbx",
            BoneDir + "/trike_skull.fbx",
            BoneDir + "/trex_femur.fbx"
        };
        foreach (var path in paths)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;
            bool changed = false;
            if (!importer.isReadable) { importer.isReadable = true; changed = true; }
            if (importer.globalScale > 101f) { importer.globalScale = 100f; changed = true; }
            if (changed) importer.SaveAndReimport();
        }
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Models")) AssetDatabase.CreateFolder("Assets", "Models");
        if (!AssetDatabase.IsValidFolder(BoneDir)) AssetDatabase.CreateFolder("Assets/Models", "GameBones");
        if (!AssetDatabase.IsValidFolder("Assets/Meshes")) AssetDatabase.CreateFolder("Assets", "Meshes");
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/Meshes", "Game");
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Materials", "Game");
        if (!AssetDatabase.IsValidFolder("Assets/Textures")) AssetDatabase.CreateFolder("Assets", "Textures");
        if (!AssetDatabase.IsValidFolder(TexDir)) AssetDatabase.CreateFolder("Assets/Textures", "Game");
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
    }
}
