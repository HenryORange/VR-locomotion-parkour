using UnityEngine;
using System.Collections.Generic;

public class ObjectTEmission : MonoBehaviour
{
    public Color emissionColor = Color.white; // Desired emission color
    public float emissionIntensity = 1.0f; // Emission intensity
    private List<Material> materials = new List<Material>();

    public void SetUpRenderers(GameObject obj)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var rdr in renderers)
        foreach (var mat in rdr.sharedMaterials)
        {
            materials.Add(mat);
        }
    }

    public void SetEmission(bool enableEmission, GameObject obj)
    {
        if (obj == null) return;
        SetUpRenderers(obj);
        foreach (var material in materials)
        {

            if (enableEmission)
            {
                // Enable emission and set desired color
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    var finalEmissionColor = emissionColor * emissionIntensity;
                    material.SetColor("_EmissionColor", finalEmissionColor);
                }
            }
            else
            {
                // Revert to original emission state
                material.DisableKeyword("_EMISSION");
            }
        }
    }
}