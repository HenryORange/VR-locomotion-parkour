using System;
using System.Collections;
using UnityEditor;
using UnityEngine;

public class InteractionTechnique : MonoBehaviour
{
    public OVRHand leftHand;
    public OVRHand rightHand;

    private OVRSkeleton handSkeleton;

    // private bool isSelected = false;
    public Transform hmdTransform;
    private GameObject selectedObject;
    public SelectionTaskMeasure selectionTaskMeasure;
    private Vector3 rightHandPosition;
    private OVRBone palm;

    // private bool isRayActive = false;
    private RaycastHit hit;
    private bool canTap = true;

    public Material selectMaterial;
    public Material unselectMaterial;
    public SkinnedMeshRenderer handMeshRenderer;
    // public Transform trackingSpace;

    private GameObject selectedObj;

    void Start()
    {
        handSkeleton = rightHand.gameObject.GetComponent<OVRSkeleton>();
        StartCoroutine(WaitForSkeleton());
    }

    private IEnumerator WaitForSkeleton()
    {
        while (!handSkeleton.IsInitialized)
        {
            yield return null;
        }

        Debug.LogWarning("Skeleton Initialized");
        var bones = handSkeleton.Bones;
        Debug.LogWarning($"Number of Bones: {bones.Count}");
        palm = handSkeleton.Bones[(int)OVRSkeleton.BoneId.XRHand_Palm];
    }


    // Update is called once per frame
    void Update()
    {
        if (handSkeleton == null || selectionTaskMeasure == null || hmdTransform == null) return;

        var rightHandRotation = transform.rotation * OVRInput.GetLocalControllerRotation(OVRInput.Controller.RHand);
        // rightHandPosition =
        //     trackingSpace.TransformPoint(OVRInput.GetLocalControllerPosition(OVRInput.Controller.RHand));
        rightHandPosition = palm.Transform.position;
        // rightHandRotation * Vector3.forward * 0.025f + rightHandRotation * Vector3.down * 0.025f;
        var palmNormal = -(rightHandRotation * Vector3.up);

        // if (Math.Abs(Vector3.Dot(palmNormal, hmdTransform.up)) < 0.3f)
        // {
        // isRayActive = true;

        // lineRenderer.enabled = true;
        // lineRenderer.SetPosition(0, rightHandPosition + Vector3.up * 0.05f);
        // lineRenderer.SetPosition(1, rightHandPosition + palmNormal * 10);

        Physics.Raycast(rightHandPosition, palmNormal, out hit, 10f);

        // make the interactable objects not glow
        var tShapes = GameObject.FindGameObjectsWithTag("objectT");
        if (tShapes != null)
            foreach (var tShape in tShapes)
            {
                var outline = tShape.GetComponent<Outline>();
                if (outline != null) tShape.GetComponent<Outline>().enabled = false;
            }

        var start = GameObject.FindWithTag("selectionTaskStart");
        if (start != null) start.GetComponent<Outline>().enabled = false;
        var done = GameObject.FindWithTag("done");
        if (done != null) done.GetComponent<Outline>().enabled = false;

        if (hit.collider != null)
        {
            var obj = hit.collider.gameObject;
            if (obj.CompareTag("objectT") || obj.CompareTag("selectionTaskStart") || obj.CompareTag("done"))
            {
                var outline = obj.GetComponent<Outline>();
                if (outline == null) obj.AddComponent<Outline>();
                outline.enabled = true;
            }
        }
        // }
        // else
        // {
        //     isRayActive = false;
        //     lineRenderer.enabled = false;

        // }

        if (leftHand.GetFingerIsPinching(OVRHand.HandFinger.Index) &&
            leftHand.GetFingerConfidence(OVRHand.HandFinger.Index) == OVRHand.TrackingConfidence.High)
        {
            handMeshRenderer.material = selectMaterial;
            if (hit.collider != null)
            {
                InteractWithObject(hit.collider.gameObject);
            }
        }
        else
        {
            handMeshRenderer.material = unselectMaterial;
            // drop t
            if (selectedObj != null)
            {
                selectedObj.transform.parent.transform.parent = null;
                selectedObj = null;
            }
        }

        handMeshRenderer.material = handMeshRenderer.material;
    }

    private void InteractWithObject(GameObject obj)
    {
        if (!canTap) return;
        StartCoroutine(DebounceTapInput());

        if (obj.CompareTag("objectT"))
        {
            selectedObj = obj.gameObject;
            selectedObj.transform.parent.transform.parent = palm.Transform;
        }
        else if (obj.gameObject.CompareTag("selectionTaskStart"))
        {
            if (!selectionTaskMeasure.isCountdown)
            {
                selectionTaskMeasure.isTaskStart = true;
                selectionTaskMeasure.StartOneTask();
            }
        }
        else if (obj.gameObject.CompareTag("done"))
        {
            selectionTaskMeasure.isTaskStart = false;
            selectionTaskMeasure.EndOneTask();
        }

        Debug.LogError($"Interacted with: {obj.name}");
    }

    private IEnumerator DebounceTapInput()
    {
        canTap = false;
        yield return new WaitForSeconds(0.3f);
        canTap = true;
    }
}