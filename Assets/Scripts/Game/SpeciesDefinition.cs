using UnityEngine;

[CreateAssetMenu(menuName = "RHC7/Species Definition", fileName = "Species")]
public class SpeciesDefinition : ScriptableObject
{
    public string id;
    public string displayName;
    public DinoSpecies speciesValue;
    public string pageEyebrow;
    public string pageTitle;
    [TextArea] public string description;
    public Texture2D mainArt;
    public Texture2D extraArt;
}
