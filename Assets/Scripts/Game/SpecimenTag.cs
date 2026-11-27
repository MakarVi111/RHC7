using UnityEngine;

/// <summary>
/// Small paper identification tag that appears after a specimen is identified.
/// It follows the bone, faces the camera and stays connected by a short thread.
/// </summary>
public class SpecimenTag : MonoBehaviour
{
    public GameBone bone;
    public float rise = 0.11f;
    public float side = 0.10f;

    Camera cam;
    LineRenderer thread;
    Transform card;

    void Awake()
    {
        cam = Camera.main;
        thread = GetComponent<LineRenderer>();
        card = transform.Find("TagCard");
    }

    void LateUpdate()
    {
        if (bone == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Bounds b = bone.WorldBounds();
        Vector3 attach = b.center + Vector3.up * (b.extents.y * 0.72f);
        Vector3 right = cam.transform.right;
        Vector3 up = cam.transform.up;
        Vector3 pos = attach + up * rise + right * side - cam.transform.forward * 0.03f;

        transform.position = pos;
        transform.rotation = cam.transform.rotation;

        if (thread != null)
        {
            thread.useWorldSpace = true;
            thread.positionCount = 2;
            thread.SetPosition(0, attach);
            Vector3 cardEdge = pos - right * 0.108f + up * 0.052f;
            thread.SetPosition(1, cardEdge);
        }
    }
}
