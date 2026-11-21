using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RHC7.EditorTools
{
    /// <summary>
    /// Rebuilds the cleaning-core prototype scene from scratch.
    /// Run from the RHC7 menu or via MCP execute_code -> PrototypeBuilder.BuildAll().
    /// </summary>
    public static class PrototypeBuilder
    {
        private const string ScenePath = "Assets/Scenes/CleaningCore.unity";
        private const string MeshPath = "Assets/Meshes/Femur.asset";
        private const string ShadeMeshPath = "Assets/Meshes/LampShade.asset";
        private const string PostProfilePath = "Assets/Settings/RHC7_Post.asset";

        private static Material boneMat;
        private static Material woodMat;
        private static Material woodDarkMat;
        private static Material feltMat;
        private static Material metalMat;
        private static Material bronzeMat;
        private static Material bulbMat;
        private static Material toolWoodMat;
        private static Material bristleMat;
        private static Material bonePlainMat;
        private static Material backdropMat;
        private static Material paperMat;
        private static Material amberMat;
        private static Material dustMoteMat;

        private static int boneLayer;
        private static int toolLayer;

        [MenuItem("RHC7/Rebuild Cleaning Core Scene")]
        public static void BuildMenu()
        {
            string report = BuildAll();
            Debug.Log(report);
        }

        public static string BuildAll()
        {
            EnsureFolders();
            EnsureLayers();
            PlayerSettings.runInBackground = true;

            GenerateTextures();
            BuildMaterials();

            float[] boneLengths = { 0.30f, 0.26f, 0.34f };
            float[] boneRadii = { 0.021f, 0.022f, 0.023f };
            int[] boneSeeds = { 7, 13, 21 };
            Mesh[] boneMeshes = BuildBoneMeshes(boneLengths, boneRadii, boneSeeds);
            float[] boneAspects = new float[3];
            float[] boneCircumferences = new float[3];
            for (int i = 0; i < 3; i++)
            {
                boneCircumferences[i] = 2f * Mathf.PI * boneRadii[i] * 0.95f;
                boneAspects[i] = boneLengths[i] / boneCircumferences[i];
            }

            BuildPostProfile();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupRenderSettings();
            var lab = new GameObject("Laboratory").transform;
            BuildRoom(lab);
            BuildTable(lab);
            BuildLamp(lab);
            BuildChart(lab);
            BuildTools(lab);
            BuildAtlasBook(lab);
            var bone = BuildBone(lab, boneMeshes[0]);
            bone.boneName = "Бедро";
            bone.rock = RockType.Sandstone;
            bone.uvAspect = boneAspects[0];
            bone.circumference = boneCircumferences[0];
            var shards = new GameObject("Shards").transform;
            shards.SetParent(lab, false);

            var camera = BuildCamera();
            var cine = BuildCinemachineCamera(camera);
            var hand = new GameObject("Hand").AddComponent<HandToolView>();
            var hud = new GameObject("HUD").AddComponent<CleaningHud>();
            BuildVolume();
            BuildAmbientDust(lab);

            var game = new GameObject("Game");
            var controller = game.AddComponent<CleaningController>();
            controller.bone = bone;
            controller.viewCamera = camera;
            controller.cineCamera = cine;
            controller.hud = hud;
            controller.handView = hand;
            controller.boneMask = 1 << boneLayer;
            controller.toolMask = 1 << toolLayer;
            controller.shardRoot = shards;
            controller.shardMaterial = bonePlainMat;
            controller.tools = ToolDefinition.CreateDefaultSet();
            controller.debugInput = false;
            controller.boneMeshes = boneMeshes;
            controller.boneAspects = boneAspects;
            controller.boneCircumferences = boneCircumferences;
            controller.boneRocks = new[] { RockType.Sandstone, RockType.Clay, RockType.Limestone };
            controller.boneNames = new[] { "Бедро", "Плечевая", "Большая берцовая" };
            controller.boneRotations = new[] { new Vector3(0f, 24f, 3f), new Vector3(0f, 32f, -4f), new Vector3(0f, 18f, 5f) };
            controller.currentBoneIndex = 0;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return "RHC7 cleaning core scene rebuilt: " + ScenePath +
                   " | bone layer " + boneLayer + " | tool layer " + toolLayer;
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/Meshes", "Assets/Textures", "Assets/Materials", "Assets/Scenes",
                "Assets/Settings", "Assets/Scripts", "Assets/Editor"
            };
            foreach (string folder in folders)
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                string name = Path.GetFileName(folder);
                if (!AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder("Assets", name);
                else AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void EnsureLayers()
        {
            boneLayer = EnsureLayer("Bone");
            toolLayer = EnsureLayer("Tool");
        }

        private static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return 0;

            var tagManager = new SerializedObject(assets[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var element = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(element.stringValue)) continue;
                element.stringValue = name;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                return i;
            }
            Debug.LogError("RHC7: no free user layer for " + name);
            return 0;
        }

        private static void SetupRenderSettings()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.125f, 0.10f, 1f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.35f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.045f, 0.038f, 0.031f, 1f);
            RenderSettings.fogDensity = 0.018f;
        }

        private static void BuildRoom(Transform lab)
        {
            var room = new GameObject("Room").transform;
            room.SetParent(lab, false);

            Box("Floor", room, new Vector3(0f, -0.03f, 0.5f), new Vector3(6f, 0.06f, 6f), woodDarkMat);
            Box("BackWall", room, new Vector3(0f, 1.6f, 1.75f), new Vector3(6f, 3.2f, 0.08f), backdropMat);
            Box("LeftWall", room, new Vector3(-2.2f, 1.5f, 0.5f), new Vector3(0.08f, 3f, 4f), backdropMat);
            Box("RightWall", room, new Vector3(2.2f, 1.5f, 0.5f), new Vector3(0.08f, 3f, 4f), backdropMat);

            var key = new GameObject("WindowFill").AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.86f, 0.68f);
            key.intensity = 0.18f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.7f;
            key.transform.rotation = Quaternion.Euler(42f, -38f, 0f);

            var cool = new GameObject("CoolBounce").AddComponent<Light>();
            cool.type = LightType.Directional;
            cool.color = new Color(0.55f, 0.62f, 0.75f);
            cool.intensity = 0.05f;
            cool.shadows = LightShadows.None;
            cool.transform.rotation = Quaternion.Euler(20f, 150f, 0f);
        }

        private static void BuildTable(Transform lab)
        {
            var table = new GameObject("WorkTable").transform;
            table.SetParent(lab, false);

            Box("Top", table, new Vector3(0f, 0.875f, 0.10f), new Vector3(1.50f, 0.05f, 0.86f), woodMat);
            Box("Apron", table, new Vector3(0f, 0.805f, 0.10f), new Vector3(1.36f, 0.10f, 0.72f), woodDarkMat);

            float[] xs = { -0.68f, 0.68f };
            float[] zs = { -0.24f, 0.44f };
            foreach (float x in xs)
            {
                foreach (float z in zs)
                {
                    Box("Leg", table, new Vector3(x, 0.38f, z), new Vector3(0.065f, 0.76f, 0.065f), woodDarkMat);
                }
            }

            Box("Crossbar", table, new Vector3(0f, 0.20f, 0.10f), new Vector3(1.30f, 0.05f, 0.05f), woodDarkMat);

            var tray = new GameObject("Tray").transform;
            tray.SetParent(table, false);
            Box("TrayBase", tray, new Vector3(0f, 0.907f, 0.00f), new Vector3(0.58f, 0.018f, 0.30f), feltMat);
            Box("TrayRimFront", tray, new Vector3(0f, 0.922f, -0.147f), new Vector3(0.60f, 0.028f, 0.018f), woodDarkMat);
            Box("TrayRimBack", tray, new Vector3(0f, 0.922f, 0.147f), new Vector3(0.60f, 0.028f, 0.018f), woodDarkMat);
            Box("TrayRimLeft", tray, new Vector3(-0.291f, 0.922f, 0f), new Vector3(0.018f, 0.028f, 0.30f), woodDarkMat);
            Box("TrayRimRight", tray, new Vector3(0.291f, 0.922f, 0f), new Vector3(0.018f, 0.028f, 0.30f), woodDarkMat);

            var block = new GameObject("ToolRack").transform;
            block.SetParent(table, false);
            Box("RackBase", block, new Vector3(0.48f, 0.917f, 0.30f), new Vector3(0.36f, 0.034f, 0.10f), woodDarkMat);
            Box("RackBack", block, new Vector3(0.48f, 0.96f, 0.345f), new Vector3(0.36f, 0.12f, 0.018f), woodDarkMat);
        }

        private static void BuildLamp(Transform lab)
        {
            var lamp = new GameObject("DeskLamp").transform;
            lamp.SetParent(lab, false);

            Cylinder("Base", lamp, new Vector3(-0.70f, 0.915f, 0.08f), 0.078f, 0.018f, bronzeMat);
            Sphere("BaseDome", lamp, new Vector3(-0.70f, 0.935f, 0.08f), new Vector3(0.094f, 0.029f, 0.094f), bronzeMat);

            Vector3 p0 = new Vector3(-0.70f, 0.945f, 0.08f);
            Vector3 p1 = new Vector3(-0.68f, 1.075f, 0.14f);
            Vector3 p2 = new Vector3(-0.58f, 1.14f, 0.19f);
            CylinderBetween("ArmLower", lamp, p0, p1, 0.0105f, bronzeMat);
            Sphere("Elbow", lamp, p1, Vector3.one * 0.024f, bronzeMat);
            CylinderBetween("ArmUpper", lamp, p1, p2, 0.0085f, bronzeMat);

            Vector3 mouth = p2 + new Vector3(0.010f, -0.020f, 0.008f);
            Vector3 target = new Vector3(0f, 0.95f, 0f);
            Vector3 dir = (target - mouth).normalized;
            Quaternion shadeRot = Quaternion.FromToRotation(Vector3.down, dir);
            Vector3 shadeCenter = mouth - dir * 0.050f;

            var shadeMesh = AssetDatabase.LoadAssetAtPath<Mesh>(ShadeMeshPath);
            var shade = new GameObject("Shade");
            shade.transform.SetParent(lamp, false);
            shade.transform.position = shadeCenter;
            shade.transform.rotation = shadeRot;
            shade.AddComponent<MeshFilter>().sharedMesh = shadeMesh;
            var shadeRenderer = shade.AddComponent<MeshRenderer>();
            shadeRenderer.sharedMaterial = bronzeMat;

            Sphere("Bulb", lamp, mouth + dir * 0.012f, Vector3.one * 0.032f, bulbMat);

            var bulbLight = new GameObject("BulbLight").AddComponent<Light>();
            bulbLight.transform.SetParent(lamp, false);
            bulbLight.transform.position = mouth + dir * 0.03f;
            bulbLight.type = LightType.Point;
            bulbLight.color = new Color(1f, 0.78f, 0.52f);
            bulbLight.intensity = 0.18f;
            bulbLight.range = 1.0f;
            bulbLight.shadows = LightShadows.None;

            var spot = new GameObject("KeySpot").AddComponent<Light>();
            spot.transform.SetParent(lamp, false);
            spot.transform.position = mouth + dir * 0.035f;
            spot.transform.rotation = Quaternion.LookRotation(dir);
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.79f, 0.56f);
            spot.intensity = 1.65f;
            spot.range = 3.0f;
            spot.spotAngle = 56f;
            spot.innerSpotAngle = 28f;
            spot.shadows = LightShadows.Soft;
            spot.shadowStrength = 0.9f;

            var fill = new GameObject("TableFill").AddComponent<Light>();
            fill.transform.SetParent(lamp, false);
            fill.transform.position = new Vector3(0.35f, 1.32f, -0.4f);
            fill.type = LightType.Point;
            fill.color = new Color(1f, 0.86f, 0.66f);
            fill.intensity = 0.10f;
            fill.range = 2.2f;
            fill.shadows = LightShadows.None;
        }

        private static void BuildChart(Transform lab)
        {
            var chart = new GameObject("AnatomyChart").transform;
            chart.SetParent(lab, false);
            chart.position = new Vector3(-0.92f, 1.17f, 1.665f);

            Box("ChartFrame", chart, Vector3.zero, new Vector3(0.46f, 0.58f, 0.03f), woodDarkMat);

            var paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
            paper.name = "Paper";
            paper.transform.SetParent(chart, false);
            paper.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            paper.transform.localRotation = Quaternion.identity;
            paper.transform.localScale = new Vector3(0.40f, 0.52f, 1f);
            Object.DestroyImmediate(paper.GetComponent<Collider>());
            var renderer = paper.GetComponent<Renderer>();
            renderer.sharedMaterial = paperMat;
        }

        private static void BuildTools(Transform lab)
        {
            Vector3 basePos = new Vector3(0.48f, 0.94f, 0.30f);
            float[] offsets = { -0.12f, 0f, 0.12f };
            ToolKind[] kinds = { ToolKind.Brush, ToolKind.Scraper, ToolKind.Chisel };
            string[] names = { "Tool_Brush", "Tool_Scraper", "Tool_Chisel" };

            for (int i = 0; i < 3; i++)
            {
                var root = new GameObject(names[i]);
                root.transform.SetParent(lab, false);
                root.transform.position = basePos + new Vector3(offsets[i], 0f, 0f);
                root.transform.rotation = Quaternion.Euler(-4f, 0f, 0f);
                root.layer = toolLayer;

                switch (kinds[i])
                {
                    case ToolKind.Brush: BuildBrushModel(root.transform); break;
                    case ToolKind.Scraper: BuildScraperModel(root.transform); break;
                    default: BuildChiselModel(root.transform); break;
                }

                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.115f, 0f);
                collider.size = new Vector3(0.10f, 0.27f, 0.10f);

                var pickup = root.AddComponent<ToolPickup>();
                pickup.kind = kinds[i];
                pickup.displayName = kinds[i] == ToolKind.Brush ? "Кисть" : (kinds[i] == ToolKind.Scraper ? "Скребок" : "Зубило");
            }
        }

        private static void BuildBrushModel(Transform root)
        {
            Cylinder("Handle", root, new Vector3(0f, 0.075f, 0f), 0.014f, 0.075f, toolWoodMat);
            Cylinder("Ferrule", root, new Vector3(0f, 0.157f, 0f), 0.016f, 0.012f, metalMat);
            var bristles = Box("Bristles", root, new Vector3(0f, 0.188f, 0f), new Vector3(0.075f, 0.045f, 0.028f), bristleMat);
            bristles.transform.localRotation = Quaternion.Euler(3f, 0f, 0f);
        }

        private static void BuildScraperModel(Transform root)
        {
            Cylinder("Handle", root, new Vector3(0f, 0.068f, 0f), 0.015f, 0.068f, toolWoodMat);
            Cylinder("Ferrule", root, new Vector3(0f, 0.142f, 0f), 0.017f, 0.010f, metalMat);
            var neck = Box("Neck", root, new Vector3(0f, 0.168f, 0f), new Vector3(0.012f, 0.045f, 0.008f), metalMat);
            neck.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            var blade = Box("Blade", root, new Vector3(0f, 0.202f, 0.012f), new Vector3(0.045f, 0.005f, 0.045f), metalMat);
            blade.transform.localRotation = Quaternion.Euler(24f, 0f, 0f);
        }

        private static void BuildChiselModel(Transform root)
        {
            Cylinder("Handle", root, new Vector3(0f, 0.055f, 0f), 0.017f, 0.055f, toolWoodMat);
            Cylinder("Shaft", root, new Vector3(0f, 0.155f, 0f), 0.0075f, 0.055f, metalMat);
            var tip = Box("Tip", root, new Vector3(0f, 0.215f, 0.006f), new Vector3(0.045f, 0.004f, 0.026f), metalMat);
            tip.transform.localRotation = Quaternion.Euler(26f, 0f, 0f);
        }

        private static void BuildAtlasBook(Transform lab)
        {
            var root = new GameObject("Atlas");
            root.transform.SetParent(lab, false);
            root.transform.position = new Vector3(-0.46f, 0.925f, 0.24f);
            root.transform.rotation = Quaternion.Euler(0f, 18f, 0f);
            root.layer = toolLayer;

            Box("Cover", root.transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.30f, 0.03f, 0.22f), woodDarkMat);
            Box("Pages", root.transform, new Vector3(0f, 0.038f, 0f), new Vector3(0.27f, 0.022f, 0.19f), paperMat);
            Box("Spine", root.transform, new Vector3(-0.145f, 0.04f, 0f), new Vector3(0.02f, 0.06f, 0.22f), woodDarkMat);
            Box("Bookmark", root.transform, new Vector3(0.10f, 0.052f, 0.02f), new Vector3(0.012f, 0.004f, 0.11f), metalMat);

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.03f, 0f);
            collider.size = new Vector3(0.32f, 0.09f, 0.25f);

            root.AddComponent<AtlasBook>();
        }

        private static BoneSurface BuildBone(Transform lab, Mesh mesh)
        {
            var boneGo = new GameObject("Bone");
            boneGo.transform.SetParent(lab, false);
            float y = 0.916f + (mesh != null ? mesh.bounds.extents.y : 0.043f) + 0.002f;
            boneGo.transform.position = new Vector3(0f, y, 0.01f);
            boneGo.transform.rotation = Quaternion.Euler(0f, 24f, 3f);
            boneGo.layer = boneLayer;

            var filter = boneGo.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = boneGo.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = boneMat;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            var collider = boneGo.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = false;

            var surface = boneGo.AddComponent<BoneSurface>();
            surface.boneRenderer = renderer;
            surface.rock = RockType.Sandstone;
            return surface;
        }

        private static Camera BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.028f, 0.024f, 0.020f, 1f);
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 60f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            go.AddComponent<AudioListener>();

            go.transform.position = new Vector3(0f, 1.335f, -0.86f);
            go.transform.rotation = LookRotation(new Vector3(0f, 0.945f, 0.015f) - go.transform.position);

            var brain = go.AddComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.SmartUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            var data = go.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            data.antialiasing = AntialiasingMode.None;

            return cam;
        }

        private static CinemachineCamera BuildCinemachineCamera(Camera camera)
        {
            var go = new GameObject("CM Static View");
            go.transform.position = camera.transform.position;
            go.transform.rotation = camera.transform.rotation;
            var cm = go.AddComponent<CinemachineCamera>();
            cm.Priority = 20;
            var lens = cm.Lens;
            lens.FieldOfView = 38f;
            lens.NearClipPlane = 0.03f;
            lens.FarClipPlane = 60f;
            cm.Lens = lens;
            return cm;
        }

        private static void BuildVolume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
            var go = new GameObject("Global Volume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
        }

        private static void BuildAmbientDust(Transform lab)
        {
            var go = new GameObject("AmbientDust");
            go.transform.SetParent(lab, false);
            go.transform.position = new Vector3(0f, 1.25f, 0.1f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 12f;
            main.startSpeed = 0.008f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.002f, 0.006f);
            main.startColor = new Color(1f, 0.88f, 0.70f, 0.16f);
            main.maxParticles = 220;
            main.gravityModifier = 0.0005f;
            main.prewarm = true;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 7f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.4f, 1.1f, 1.1f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.03f;
            noise.frequency = 0.15f;
            noise.scrollSpeed = 0.04f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.material = dustMoteMat;
            ps.Play();
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
            {
                if (s.path == scenePath) return;
            }
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------------------------------------------------------------- assets

        private static void GenerateTextures()
        {
            string texDir = Application.dataPath + "/Textures/";
            if (!Directory.Exists(texDir)) Directory.CreateDirectory(texDir);

            WriteTexture(texDir + "Wood.png", "Assets/Textures/Wood.png", MakeWood(512), TextureWrapMode.Repeat);
            WriteTexture(texDir + "WoodDark.png", "Assets/Textures/WoodDark.png", MakeWoodDark(256), TextureWrapMode.Repeat);
            WriteTexture(texDir + "Felt.png", "Assets/Textures/Felt.png", MakeFelt(256), TextureWrapMode.Repeat);
            WriteTexture(texDir + "Backdrop.png", "Assets/Textures/Backdrop.png", MakeBackdrop(512), TextureWrapMode.Clamp);
            WriteTexture(texDir + "Paper.png", "Assets/Textures/Paper.png", MakePaperChart(256, 320), TextureWrapMode.Clamp);
            WriteTexture(texDir + "Dust.png", "Assets/Textures/Dust.png", MakeSoftCircle(64), TextureWrapMode.Clamp);
        }

        private static void WriteTexture(string fullPath, string assetPath, Texture2D tex, TextureWrapMode wrap)
        {
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = wrap;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
        }

        private static Texture2D MakeWood(int size)
        {
            var tex = NewTex(size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.018f + 3.1f, y * 0.075f + 7.7f);
                    float rings = Mathf.Sin(y * 0.17f + n * 9f) * 0.5f + 0.5f;
                    float fine = Mathf.PerlinNoise(x * 0.55f, y * 0.06f);
                    float streak = Mathf.PerlinNoise(x * 0.02f + 11f, y * 0.5f + 2f);
                    Color baseC = Color.Lerp(new Color(0.135f, 0.075f, 0.045f), new Color(0.235f, 0.145f, 0.085f), rings * 0.65f + fine * 0.35f);
                    baseC *= 0.9f + streak * 0.22f;
                    px[y * size + x] = baseC;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeWoodDark(int size)
        {
            var tex = NewTex(size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.03f + 2f, y * 0.09f + 5f);
                    float rings = Mathf.Sin(y * 0.22f + n * 7f) * 0.5f + 0.5f;
                    Color c = Color.Lerp(new Color(0.075f, 0.045f, 0.030f), new Color(0.135f, 0.085f, 0.055f), rings * 0.6f + n * 0.4f);
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeFelt(int size)
        {
            var tex = NewTex(size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.35f, y * 0.35f);
                    float speck = Mathf.PerlinNoise(x * 1.9f, y * 1.9f);
                    Color c = new Color(0.055f, 0.085f, 0.062f) * (0.8f + n * 0.35f + speck * 0.1f);
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeBackdrop(int size)
        {
            var tex = NewTex(size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;
                    float grad = Mathf.Lerp(0.028f, 0.085f, v);
                    float dx = (u - 0.5f) * 1.6f;
                    float dy = (v - 0.62f) * 2.4f;
                    float glow = Mathf.Exp(-(dx * dx + dy * dy) * 4.5f) * 0.045f;
                    float n = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.018f;
                    float g = grad + glow + n;
                    Color c = new Color(g * 1.02f, g * 0.88f, g * 0.72f);
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakePaperChart(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[width * height];
            var paper = new Color(0.76f, 0.70f, 0.56f);
            var ink = new Color(0.19f, 0.14f, 0.09f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.045f, y * 0.045f);
                    float stain = Mathf.PerlinNoise(x * 0.012f + 8f, y * 0.012f + 3f);
                    Color c = paper * (0.85f + n * 0.16f - stain * 0.12f);
                    px[y * width + x] = c;
                }
            }

            // simple femur diagram
            float cx = width * 0.5f;
            float topY = height * 0.80f;
            float bottomY = height * 0.22f;
            for (int y = (int)bottomY; y <= topY; y++)
            {
                float t = Mathf.InverseLerp(bottomY, topY, y);
                float bulge = Mathf.Pow(Mathf.Abs(t * 2f - 1f), 2.2f);
                float r = width * (0.045f + 0.06f * bulge);
                for (int x = (int)(cx - r); x <= (int)(cx + r); x++)
                {
                    float dx = (x - cx) / r;
                    if (Mathf.Abs(dx) > 1f) continue;
                    float edge = Mathf.Clamp01((1f - Mathf.Abs(dx)) * 7f);
                    float line = Mathf.Clamp01((r - Mathf.Abs(x - cx)) * 1.1f);
                    if (Mathf.Abs(dx) > 0.72f || Mathf.Abs(dx) < 0.10f)
                    {
                        BlendPixel(px, width, height, x, y, ink, edge * 0.85f);
                    }
                }
            }

            // annotation lines
            for (int i = 0; i < 5; i++)
            {
                int y = (int)(height * (0.14f + i * 0.035f));
                int x0 = (int)(width * 0.12f);
                int x1 = (int)(width * (0.55f + (i % 3) * 0.08f));
                for (int x = x0; x < x1; x++) BlendPixel(px, width, height, x, y, ink, 0.5f);
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static void BlendPixel(Color32[] px, int width, int height, int x, int y, Color ink, float a)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            int i = y * width + x;
            Color c = px[i];
            c = Color.Lerp(c, ink, Mathf.Clamp01(a));
            px[i] = c;
        }

        private static Texture2D MakeSoftCircle(int size)
        {
            var tex = NewTex(size);
            var px = new Color32[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxD = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / maxD;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D NewTex(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        }

        private static void BuildMaterials()
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            var boneShader = Shader.Find("RHC7/Bone Cleaning");
            if (boneShader == null) Debug.LogError("RHC7 bone shader not found");

            var woodTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Wood.png");
            var woodDarkTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/WoodDark.png");
            var feltTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Felt.png");
            var backdropTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Backdrop.png");
            var paperTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Paper.png");
            var dustTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Dust.png");

            boneMat = GetMaterial("Assets/Materials/Bone.mat", boneShader);
            woodMat = GetMaterial("Assets/Materials/Wood.mat", urpLit);
            woodDarkMat = GetMaterial("Assets/Materials/WoodDark.mat", urpLit);
            feltMat = GetMaterial("Assets/Materials/Felt.mat", urpLit);
            metalMat = GetMaterial("Assets/Materials/Metal.mat", urpLit);
            bronzeMat = GetMaterial("Assets/Materials/Bronze.mat", urpLit);
            bulbMat = GetMaterial("Assets/Materials/Bulb.mat", urpLit);
            toolWoodMat = GetMaterial("Assets/Materials/ToolWood.mat", urpLit);
            bristleMat = GetMaterial("Assets/Materials/Bristles.mat", urpLit);
            bonePlainMat = GetMaterial("Assets/Materials/BonePlain.mat", urpLit);
            backdropMat = GetMaterial("Assets/Materials/Backdrop.mat", urpLit);
            paperMat = GetMaterial("Assets/Materials/Paper.mat", urpLit);
            amberMat = GetMaterial("Assets/Materials/Amber.mat", urpLit);
            dustMoteMat = GetMaterial("Assets/Materials/DustMote.mat", Shader.Find("Universal Render Pipeline/Particles/Unlit"));

            SetLit(woodMat, woodTex, new Color(0.92f, 0.84f, 0.72f), 0f, 0.28f);
            SetLit(woodDarkMat, woodDarkTex, new Color(0.85f, 0.78f, 0.68f), 0f, 0.22f);
            SetLit(feltMat, feltTex, new Color(0.85f, 0.9f, 0.85f), 0f, 0.08f);
            SetLit(metalMat, null, new Color(0.56f, 0.57f, 0.58f), 0.85f, 0.62f);
            SetLit(bronzeMat, null, new Color(0.19f, 0.14f, 0.085f), 0.45f, 0.28f);
            SetLit(toolWoodMat, null, new Color(0.40f, 0.27f, 0.16f), 0f, 0.32f);
            SetLit(bristleMat, null, new Color(0.62f, 0.50f, 0.33f), 0f, 0.15f);
            SetLit(bonePlainMat, null, new Color(0.80f, 0.75f, 0.62f), 0f, 0.24f);
            SetLit(backdropMat, backdropTex, new Color(0.85f, 0.8f, 0.72f), 0f, 0.05f);
            SetLit(paperMat, paperTex, new Color(0.95f, 0.92f, 0.84f), 0f, 0.05f);
            SetLit(amberMat, null, new Color(0.35f, 0.20f, 0.08f), 0.15f, 0.8f);

            boneMat.SetColor("_BoneColor", new Color(0.88f, 0.82f, 0.68f));
            boneMat.SetColor("_LooseDirtColor", new Color(0.28f, 0.21f, 0.13f));
            boneMat.SetColor("_PackedDirtColor", new Color(0.16f, 0.12f, 0.07f));
            boneMat.SetColor("_ContactDirtColor", new Color(0.07f, 0.055f, 0.035f));
            boneMat.SetColor("_WearColor", new Color(0.38f, 0.19f, 0.09f));
            boneMat.SetColor("_CrackColor", new Color(0.06f, 0.04f, 0.03f));
            boneMat.SetFloat("_Smoothness", 0.24f);
            EditorUtility.SetDirty(boneMat);

            bulbMat.SetColor("_BaseColor", new Color(1f, 0.72f, 0.40f));
            bulbMat.SetColor("_EmissionColor", new Color(1f, 0.58f, 0.26f) * 1.15f);
            bulbMat.EnableKeyword("_EMISSION");
            bulbMat.SetFloat("_Smoothness", 0.4f);
            EditorUtility.SetDirty(bulbMat);

            if (dustMoteMat != null && dustTex != null)
            {
                dustMoteMat.SetTexture("_BaseMap", dustTex);
                dustMoteMat.SetColor("_BaseColor", new Color(1f, 0.88f, 0.70f, 0.35f));
                dustMoteMat.SetFloat("_Surface", 1f);
                dustMoteMat.SetFloat("_Blend", 0f);
                dustMoteMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                dustMoteMat.renderQueue = 3000;
                EditorUtility.SetDirty(dustMoteMat);
            }

            foreach (var mat in new[] { woodMat, woodDarkMat, feltMat, metalMat, bronzeMat, toolWoodMat, bristleMat, bonePlainMat, backdropMat, paperMat, amberMat })
            {
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        private static Material GetMaterial(string path, Shader shader)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null)
            {
                mat.shader = shader;
            }
            return mat;
        }

        private static void SetLit(Material mat, Texture2D tex, Color color, float metallic, float smoothness)
        {
            if (mat == null) return;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
        }

        private static Mesh[] BuildBoneMeshes(float[] lengths, float[] radii, int[] seeds)
        {
            int count = Mathf.Min(lengths.Length, Mathf.Min(radii.Length, seeds.Length));
            var meshes = new Mesh[count];

            for (int i = 0; i < count; i++)
            {
                var mesh = BoneMeshFactory.CreateLongBone(lengths[i], radii[i], 72, 36, seeds[i]);
                mesh.name = "Bone_" + (i + 1);
                string path = "Assets/Meshes/Bone" + (i + 1) + ".asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                meshes[i] = mesh;
            }

            var shade = CreateLampShadeMesh(0.028f, 0.085f, 0.075f, 40);
            if (AssetDatabase.LoadAssetAtPath<Mesh>(ShadeMeshPath) != null) AssetDatabase.DeleteAsset(ShadeMeshPath);
            AssetDatabase.CreateAsset(shade, ShadeMeshPath);

            AssetDatabase.SaveAssets();
            return meshes;
        }

        private static Mesh CreateLampShadeMesh(float topRadius, float bottomRadius, float height, int segments)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(a);
                float z = Mathf.Sin(a);
                verts.Add(new Vector3(x * topRadius, height * 0.5f, z * topRadius));
                uvs.Add(new Vector2(i / (float)segments, 1f));
                verts.Add(new Vector3(x * bottomRadius, -height * 0.5f, z * bottomRadius));
                uvs.Add(new Vector2(i / (float)segments, 0f));
            }

            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                Tris(tris, a, a + 1, a + 2);
                Tris(tris, a + 1, a + 3, a + 2);
            }

            // top cap
            int center = verts.Count;
            verts.Add(new Vector3(0f, height * 0.5f, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                Tris(tris, center, a + 2, a);
            }

            // duplicate reversed for the inner face
            int count = verts.Count;
            for (int i = 0; i < count; i++)
            {
                verts.Add(verts[i]);
                uvs.Add(uvs[i]);
            }
            int triCount = tris.Count;
            for (int i = 0; i < triCount; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                tris.Add(count + a);
                tris.Add(count + c);
                tris.Add(count + b);
            }

            var mesh = new Mesh { name = "LampShade" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Tris(List<int> list, int a, int b, int c)
        {
            list.Add(a);
            list.Add(b);
            list.Add(c);
        }

        private static void BuildPostProfile()
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath) != null)
                AssetDatabase.DeleteAsset(PostProfilePath);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostProfilePath);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.02f);
            color.contrast.Override(9f);
            color.saturation.Override(-16f);
            color.colorFilter.Override(new Color(1.04f, 0.97f, 0.87f));

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.68f);
            bloom.tint.Override(new Color(1f, 0.88f, 0.72f));

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.30f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.02f, 0.015f, 0.01f));

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.18f);
            grain.response.Override(0.7f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------- primitives

        private static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;
            renderer.receiveShadows = true;
            return go;
        }

        private static GameObject Cylinder(string name, Transform parent, Vector3 localPos, float radius, float halfHeight, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(radius, halfHeight, radius);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;
            renderer.receiveShadows = true;
            return go;
        }

        private static GameObject Sphere(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;
            renderer.receiveShadows = true;
            return go;
        }

        private static GameObject CylinderBetween(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material mat)
        {
            var go = Cylinder(name, parent, (a + b) * 0.5f, radius, (b - a).magnitude * 0.5f, mat);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            return go;
        }

        private static Quaternion LookRotation(Vector3 dir)
        {
            return Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }
}
