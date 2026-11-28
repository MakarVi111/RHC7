using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RHC7/Level Definition", fileName = "Level")]
public class LevelDefinition : ScriptableObject
{
    public string levelName;
    [HideInInspector] public int dataVersion;
    [Range(0.1f, 1f)] public float cleanThreshold = 0.6f;
    public List<SpecimenDefinition> specimens = new List<SpecimenDefinition>();
    public List<SpeciesDefinition> species = new List<SpeciesDefinition>();
}
