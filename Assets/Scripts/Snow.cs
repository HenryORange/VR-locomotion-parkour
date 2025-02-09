using UnityEngine;

public class Snow : MonoBehaviour
{
    [SerializeField] private Material[] materials;

    public ParticleSystem snowParticles;
    
    [Range(0f, 1f)]
    public float startValue = 0.5f;
    private float elapsedTime = 0f;
    public float duration = 10f;
    
    private void Awake()
    {
        foreach (Material material in materials)
        {
            material.SetFloat("_SnowAmount", startValue);
        }
    }
    
    void Update()
    {
        var currentPosition = transform.position;
        currentPosition.y += 10;
        snowParticles.transform.position = currentPosition;
        
        if (!(elapsedTime < duration)) return;
        elapsedTime += Time.deltaTime;
        var t = elapsedTime / duration;
        var snowAmount = Mathf.Lerp(startValue, 1f, t);
        foreach (Material material in materials)
        {
            material.SetFloat("_SnowAmount", snowAmount);
        }
    }
}
