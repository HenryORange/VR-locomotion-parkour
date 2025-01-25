using UnityEngine;

public class SelectorOnArm : MonoBehaviour
{
    public Transform leftHandTransform; // Left hand reference
    public GameObject selectorUI;       // Selector UI prefab
    public Transform headTransform;
    private const float AngleThreshold = 45f;
    private const float MaxDistance = 1.0f;

    void Update()
    {
        // attach the selector to the left hand
        if (true)
        {
            // place on offset above the arm
            selectorUI.transform.position = leftHandTransform.position + leftHandTransform.up * 0.01f + leftHandTransform.right * 0.075f;
            selectorUI.transform.rotation = leftHandTransform.rotation * Quaternion.Euler(80f, -90f, 0f);

            if (!(Vector3.Distance(leftHandTransform.position, headTransform.position) <= MaxDistance)) return;
            var handToHeadDirection = (headTransform.position - leftHandTransform.position).normalized;
            var angle = Vector3.Angle(leftHandTransform.up, handToHeadDirection);
            selectorUI.SetActive(angle <= AngleThreshold);
        }
    }
}