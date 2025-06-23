using UnityEngine;

[RequireComponent(typeof(OVRLipSyncContext))]

public class OVRLipSyncBlendshapeMapper : MonoBehaviour
{
    public SkinnedMeshRenderer skinnedMeshRenderer;
    public int[] visemeToBlendShape = new int[15];

    [Range(0.0f, 5.0f)]
    public float visemeAmplifier = 1.5f; // 👈 increase this to boost effect

    private OVRLipSyncContext context;

    void Start()
    {
        context = GetComponent<OVRLipSyncContext>();
    }

    void LateUpdate()
    {
        if (context == null || context.CurrentFrame == null || skinnedMeshRenderer == null)
            return;

        var frame = context.CurrentFrame;

        // Reset all weights
        for (int i = 0; i < skinnedMeshRenderer.sharedMesh.blendShapeCount; i++)
        {
            skinnedMeshRenderer.SetBlendShapeWeight(i, 0f);
        }

        // Apply viseme weights scaled to 100
        for (int i = 0; i < frame.Visemes.Length; i++)
        {
            int blendShapeIndex = visemeToBlendShape[i];
            if (blendShapeIndex >= 0)
            {
                float weight = frame.Visemes[i] * 100f;
                skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, weight);
            }
        }
    }

}
