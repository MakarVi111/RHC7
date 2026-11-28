using UnityEngine;

[CreateAssetMenu(menuName = "RHC7/Specimen Definition", fileName = "Specimen")]
public class SpecimenDefinition : ScriptableObject
{
    public string id;
    public string partName;
    [TextArea] public string identificationHint;
    public DinoSpecies species;
    public bool isSkull;

    [Header("Visual source")]
    public GameObject bonePrefab;
    public Mesh completeMesh;
    public Material boneMaterial;

    [Header("Placement")]
    public Vector3 cratePosition;
    public Vector3 crateEuler;
    public Vector3 prepEuler;
    public float visualScale = 1f;

    [Header("Collection tag")]
    [TextArea] public string tagText;
}
