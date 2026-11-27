using UnityEngine;

public enum DinoSpecies
{
    Trex,
    Triceratops
}

/// <summary>
/// One cleanable specimen in the crate. The species stays hidden until the
/// player has cleaned the bone and identified it.
/// </summary>
public class GameBone : MonoBehaviour
{
    public string displayName;
    public string partName;
    public string identificationHint;
    public DinoSpecies species;
    public bool isSkull;
    public Vector3 prepEuler;

    public Renderer boneRenderer;
    public MeshFilter boneMesh;
    public Material boneMaterial;
    public SpecimenTag nameTag;

    [HideInInspector] public bool identified;

    BoxCollider pickCollider;

    public Bounds WorldBounds()
    {
        if (boneRenderer != null) return boneRenderer.bounds;
        var rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(transform.position, Vector3.one * 0.2f);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    public void Cache()
    {
        if (boneRenderer == null) boneRenderer = GetComponentInChildren<MeshRenderer>();
        if (boneMesh == null) boneMesh = GetComponentInChildren<MeshFilter>();
        if (boneMesh != null && boneRenderer == null)
            boneRenderer = boneMesh.GetComponent<MeshRenderer>();
        if (pickCollider == null) pickCollider = GetComponent<BoxCollider>();
        if (pickCollider == null) pickCollider = GetComponentInChildren<BoxCollider>();
        if (boneMaterial == null && boneRenderer != null)
            boneMaterial = boneRenderer.sharedMaterial;
    }

    public void SetPickable(bool on)
    {
        Cache();
        if (pickCollider != null) pickCollider.enabled = on;
    }
}
