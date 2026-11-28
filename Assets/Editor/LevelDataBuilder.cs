using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the editable data assets used by the game. The three-bone campaign
/// and the four-bone demo differ only by their LevelDefinition lists.
/// </summary>
public static class LevelDataBuilder
{
    const string DataDir = "Assets/Data";
    const string SpecimenDir = DataDir + "/Specimens";
    const string SpeciesDir = DataDir + "/Species";
    const string LevelDir = DataDir + "/Levels";
    const string JsonDir = DataDir + "/JSON";

    public const string BaseLevelPath = LevelDir + "/BaseLevel.asset";
    public const string DemoLevelPath = LevelDir + "/DemoLevel.asset";
    public const string InvalidLevelPath = LevelDir + "/InvalidLevel.asset";
    public const string TrexSpeciesPath = SpeciesDir + "/Trex.asset";
    public const string TrikeSpeciesPath = SpeciesDir + "/Triceratops.asset";

    public static LevelDefinition EnsureBaseLevel()
    {
        EnsureFolders();
        var existingBase = AssetDatabase.LoadAssetAtPath<LevelDefinition>(BaseLevelPath);
        var existingDemo = AssetDatabase.LoadAssetAtPath<LevelDefinition>(DemoLevelPath);
        var existingInvalid = AssetDatabase.LoadAssetAtPath<LevelDefinition>(InvalidLevelPath);
        if (existingBase != null && existingDemo != null && existingInvalid != null &&
            existingBase.dataVersion >= 1 && existingDemo.dataVersion >= 1 &&
            existingInvalid.dataVersion >= 1)
        {
            // Data assets are the source of truth. Do not overwrite a designer's
            // edits when the scene is rebuilt.
            WriteJson(existingBase);
            WriteJson(existingDemo);
            WriteJson(existingInvalid);
            return existingBase;
        }

        var trex = EnsureSpecies(TrexSpeciesPath, "trex", "ТИРАННОЗАВР", DinoSpecies.Trex,
            "ХИЩНЫЕ ДИНОЗАВРЫ", "ТИРАННОЗАВР",
            "Крупный череп с тяжёлой челюстью и зубами.\n" +
            "Длинная массивная бедренная кость.",
            "Assets/Textures/Notes/NoteTrexSkull.png",
            "Assets/Textures/Notes/NoteTrexFemur.png");
        var trike = EnsureSpecies(TrikeSpeciesPath, "triceratops", "ТРИЦЕРАТОПС",
            DinoSpecies.Triceratops, "РОГАТЫЕ ДИНОЗАВРЫ", "ТРИЦЕРАТОПС",
            "Широкий костяной воротник\nи три рога на морде.",
            "Assets/Textures/Notes/NoteTrikeSkull.png", null);

        var trexSkull = EnsureSpecimen("TrexSkull", "trex_skull", "Череп",
            "Крупный череп с тяжёлой челюстью и зубами — тираннозавр.",
            DinoSpecies.Trex, true, "Assets/Models/GameBones/trex_skull.fbx", null,
            new Vector3(1.95f, 0.875f, 0.25f), new Vector3(0f, 205f, 10f),
            new Vector3(0f, 25f, 4f), "ТИРАННОЗАВР\nЧЕРЕП");
        var trikeSkull = EnsureSpecimen("TrikeSkull", "trike_skull", "Череп с воротником",
            "Воротник и три рога — трицератопс.",
            DinoSpecies.Triceratops, true, null,
            "Assets/Meshes/Game/TrikeSkullComplete.asset",
            new Vector3(2.80f, 0.875f, 0.55f), new Vector3(0f, 180f, 4f),
            new Vector3(0f, 180f, 270f), "ТРИЦЕРАТОПС\nЧЕРЕП");
        var femur = EnsureSpecimen("TrexFemur", "trex_femur", "Бедренная кость",
            "Длинная массивная бедренная кость — тираннозавр.",
            DinoSpecies.Trex, false, "Assets/Models/GameBones/trex_femur.fbx", null,
            new Vector3(2.35f, 0.88f, 0f), new Vector3(0f, 90f, 0f),
            new Vector3(0f, 70f, 0f), "ТИРАННОЗАВР\nБЕДРО");

        var pelvis = EnsureSpecimen("TrexPelvis", "trex_pelvis", "Таз",
            "Массивный таз с подвздошной костью — тираннозавр.",
            DinoSpecies.Trex, false, "Assets/Models/Bones_Real/Trex_Pelvis.fbx", null,
            new Vector3(1.65f, 0.88f, 0.82f), new Vector3(0f, 155f, 8f),
            new Vector3(0f, 120f, 0f), "ТИРАННОЗАВР\nТАЗ");
        pelvis.visualScale = 1.6f;

        var baseLevel = EnsureLevel(BaseLevelPath, "РХЦ-7 · базовая смена");
        baseLevel.specimens = new List<SpecimenDefinition> { trexSkull, trikeSkull, femur };
        baseLevel.species = new List<SpeciesDefinition> { trex, trike };
        baseLevel.cleanThreshold = 0.6f;
        baseLevel.dataVersion = 1;
        EditorUtility.SetDirty(baseLevel);

        var demo = EnsureLevel(DemoLevelPath, "РХЦ-7 · демо-расширение");
        demo.specimens = new List<SpecimenDefinition> { trexSkull, trikeSkull, femur, pelvis };
        demo.species = new List<SpeciesDefinition> { trex, trike };
        demo.cleanThreshold = 0.6f;
        demo.dataVersion = 1;
        EditorUtility.SetDirty(demo);

        var invalid = EnsureLevel(InvalidLevelPath, "РХЦ-7 · проверка ошибок");
        var empty = EnsureSpecimen("BrokenSpecimen", "broken", "Пустой образец", "",
            DinoSpecies.Trex, false, null, null, Vector3.zero, Vector3.zero, Vector3.zero, "");
        invalid.specimens = new List<SpecimenDefinition>
        {
            trexSkull, empty, trikeSkull, trexSkull
        };
        invalid.species = new List<SpeciesDefinition> { trex };
        invalid.cleanThreshold = 0.6f;
        invalid.dataVersion = 1;
        EditorUtility.SetDirty(invalid);

        AssetDatabase.SaveAssets();
        WriteJson(baseLevel);
        WriteJson(demo);
        WriteJson(invalid);
        return baseLevel;
    }

    [MenuItem("Tools/RHC7/Export Level JSON")]
    public static void ExportLevelJson()
    {
        EnsureBaseLevel();
        AssetDatabase.SaveAssets();
        Debug.Log("[RHC7] Exported level JSON to " + JsonDir);
    }

    public static LevelDefinition EnsureDemoLevel()
    {
        EnsureBaseLevel();
        var demo = AssetDatabase.LoadAssetAtPath<LevelDefinition>(DemoLevelPath);
        WriteJson(demo);
        AssetDatabase.SaveAssets();
        return demo;
    }

    public static LevelDefinition EnsureInvalidLevel()
    {
        EnsureBaseLevel();
        var invalid = AssetDatabase.LoadAssetAtPath<LevelDefinition>(InvalidLevelPath);
        WriteJson(invalid);
        AssetDatabase.SaveAssets();
        return invalid;
    }

    static void EnsureFolders()
    {
        EnsureFolder("Assets", "Data");
        EnsureFolder(DataDir, "Specimens");
        EnsureFolder(DataDir, "Species");
        EnsureFolder(DataDir, "Levels");
        EnsureFolder(DataDir, "JSON");
    }

    static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    static SpeciesDefinition EnsureSpecies(string path, string id, string displayName,
        DinoSpecies value, string eyebrow, string pageTitle, string description,
        string mainArt, string extraArt)
    {
        var s = AssetDatabase.LoadAssetAtPath<SpeciesDefinition>(path);
        if (s == null)
        {
            s = ScriptableObject.CreateInstance<SpeciesDefinition>();
            AssetDatabase.CreateAsset(s, path);
        }
        s.id = id;
        s.displayName = displayName;
        s.speciesValue = value;
        s.pageEyebrow = eyebrow;
        s.pageTitle = pageTitle;
        s.description = description;
        s.mainArt = AssetDatabase.LoadAssetAtPath<Texture2D>(mainArt);
        s.extraArt = extraArt != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(extraArt) : null;
        EditorUtility.SetDirty(s);
        return s;
    }

    static SpecimenDefinition EnsureSpecimen(string assetName, string id, string part,
        string hint, DinoSpecies species, bool isSkull, string prefabPath, string meshPath,
        Vector3 cratePos, Vector3 crateEuler, Vector3 prepEuler, string tagText)
    {
        string path = SpecimenDir + "/" + assetName + ".asset";
        var d = AssetDatabase.LoadAssetAtPath<SpecimenDefinition>(path);
        if (d == null)
        {
            d = ScriptableObject.CreateInstance<SpecimenDefinition>();
            AssetDatabase.CreateAsset(d, path);
        }
        d.id = id;
        d.partName = part;
        d.identificationHint = hint;
        d.species = species;
        d.isSkull = isSkull;
        d.bonePrefab = prefabPath != null
            ? AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) : null;
        d.completeMesh = meshPath != null
            ? AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) : null;
        d.cratePosition = cratePos;
        d.crateEuler = crateEuler;
        d.prepEuler = prepEuler;
        d.visualScale = 1f;
        d.tagText = tagText;
        EditorUtility.SetDirty(d);
        return d;
    }

    static LevelDefinition EnsureLevel(string path, string name)
    {
        var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
        if (level == null)
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            AssetDatabase.CreateAsset(level, path);
        }
        level.levelName = name;
        EditorUtility.SetDirty(level);
        return level;
    }

    static void WriteJson(LevelDefinition level)
    {
        if (level == null) return;
        var dto = new LevelDto
        {
            levelName = level.levelName,
            cleanThreshold = level.cleanThreshold,
            specimens = new List<SpecimenDto>(),
            species = new List<SpeciesDto>()
        };
        if (level.specimens != null)
        {
            foreach (var s in level.specimens)
            {
                if (s == null)
                {
                    dto.specimens.Add(null);
                    continue;
                }
                dto.specimens.Add(new SpecimenDto
                {
                    id = s.id,
                    partName = s.partName,
                    hint = s.identificationHint,
                    species = s.species.ToString(),
                    isSkull = s.isSkull,
                    prefab = s.bonePrefab != null ? AssetDatabase.GetAssetPath(s.bonePrefab) : null,
                    mesh = s.completeMesh != null ? AssetDatabase.GetAssetPath(s.completeMesh) : null,
                    cratePosition = s.cratePosition,
                    crateEuler = s.crateEuler,
                    prepEuler = s.prepEuler,
                    visualScale = s.visualScale,
                    tagText = s.tagText
                });
            }
        }
        if (level.species != null)
        {
            foreach (var s in level.species)
            {
                if (s == null)
                {
                    dto.species.Add(null);
                    continue;
                }
                dto.species.Add(new SpeciesDto
                {
                    id = s.id,
                    displayName = s.displayName,
                    species = s.speciesValue.ToString(),
                    eyebrow = s.pageEyebrow,
                    title = s.pageTitle,
                    description = s.description,
                    mainArt = s.mainArt != null ? AssetDatabase.GetAssetPath(s.mainArt) : null,
                    extraArt = s.extraArt != null ? AssetDatabase.GetAssetPath(s.extraArt) : null
                });
            }
        }
        string fileName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(level)) + ".json";
        File.WriteAllText(JsonDir + "/" + fileName, JsonUtility.ToJson(dto, true));
    }

    [Serializable] class LevelDto
    {
        public string levelName;
        public float cleanThreshold;
        public List<SpecimenDto> specimens;
        public List<SpeciesDto> species;
    }

    [Serializable] class SpecimenDto
    {
        public string id;
        public string partName;
        public string hint;
        public string species;
        public bool isSkull;
        public string prefab;
        public string mesh;
        public Vector3 cratePosition;
        public Vector3 crateEuler;
        public Vector3 prepEuler;
        public float visualScale;
        public string tagText;
    }

    [Serializable] class SpeciesDto
    {
        public string id;
        public string displayName;
        public string species;
        public string eyebrow;
        public string title;
        public string description;
        public string mainArt;
        public string extraArt;
    }
}
