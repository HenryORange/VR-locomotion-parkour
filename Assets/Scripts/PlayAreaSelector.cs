using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Evryway;
using Oculus.Interaction;
using Oculus.Interaction.PoseDetection;
using Oculus.Interaction.Samples;
using Oculus.Platform.Models;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class PlayAreaSelector : MonoBehaviour
{
    public GameObject pointPrefab;
    public GameObject pointPreviewPrefab;
    public Transform groundPlane;
    private List<Vector3> playAreaPoints = new ();
    private List<GameObject> playAreaSpheres = new ();
    public GameObject redirectedUser;
    
    public GameObject speedSelector;
    public SelectorOnArm speedSelectorScript;
    public GameObject environment;
    public GameObject parkourSystem;
    public GameObject taskUI;
    public GameObject taskUICanvas;
    public Snow snowGenerator;

    private State state;
    private bool canTap = true;

    public OVRHand leftHand;
    public OVRHand rightHand;
    private OVRSkeleton handSkeleton;
    private OVRBone palm;
    public ActiveStateSelector startGamePose;
    public ActiveStateSelector resetAreaSelectionPose;
    public Material planeMaterial;

    private enum State
    {
        Setup,
        Finished
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        OVRManager.instance.isInsightPassthroughEnabled = true;
        environment.SetActive(false);
        parkourSystem.SetActive(false);
        taskUI.SetActive(false);
        speedSelector.SetActive(false);
        speedSelectorScript.enabled = false;
        snowGenerator.enabled = false;
        taskUICanvas.SetActive(false);
        state = State.Setup;

        startGamePose.WhenSelected += TryToStartGame;
        resetAreaSelectionPose.WhenSelected += ClearAreaPoints;
        handSkeleton = rightHand.gameObject.GetComponent<OVRSkeleton>();
        StartCoroutine(WaitForSkeleton());
    }
    
    private IEnumerator WaitForSkeleton()
    {
        while (!handSkeleton.IsInitialized)
        {
            yield return null;
        }

        palm = handSkeleton.Bones[(int)OVRSkeleton.BoneId.XRHand_Palm];
    }

    private void ClearAreaPoints()
    {
        playAreaPoints.Clear();
        foreach (var playAreaSphere in playAreaSpheres)
        {
            Destroy(playAreaSphere);
        }
        playAreaSpheres.Clear();
    }

    private void StartGame()
    {
        // disable passthrough and change back to skybox
        OVRManager.instance.isInsightPassthroughEnabled = false;
        Camera.main.clearFlags = CameraClearFlags.Skybox;
        
        // turn on the parkour, ui and environment
        environment.SetActive(true);
        parkourSystem.SetActive(true);
        taskUI.SetActive(true);
        speedSelector.SetActive(true);
        speedSelectorScript.enabled = true;
        snowGenerator.enabled = true;
        taskUICanvas.SetActive(true);
        
        resetAreaSelectionPose.WhenSelected -= ClearAreaPoints;
        
        // stop this script from doing stuff
        state = State.Finished;
        foreach(var sphere in playAreaSpheres) sphere.SetActive(false);
        pointPreviewPrefab.SetActive(false);
        
        // enable redirected walking
        var redirection = redirectedUser.GetComponent<RedirectionManager>();
        redirection.enabled = true;
        var simulation = redirectedUser.GetComponent<SimulationManager>();
        simulation.enabled = true;
        groundPlane.gameObject.SetActive(false);
    }
    
    void Update()
    {
        switch (state)
        {
            case State.Setup:
                if (palm == null) return;
                var rightHandPosition = palm.Transform.position;
                var rightHandRotation = transform.rotation * OVRInput.GetLocalControllerRotation(OVRInput.Controller.RHand);
                var palmNormal = -(rightHandRotation * Vector3.up);
                
                var ray = new Ray(rightHandPosition, palmNormal);

                var plane = new UnityEngine.Plane(groundPlane.up, groundPlane.position);
        
                var result = plane.Raycast(ray, out var hit);
                var rayHit = ray.GetPoint(hit);
                ShowBallPreview(rayHit, result);
        
                // TODO only do if there isn't a point yet, also add resetting the bound creation and button to confirm chosen bounds
                if (leftHand.GetFingerIsPinching(OVRHand.HandFinger.Index) &&
                    leftHand.GetFingerConfidence(OVRHand.HandFinger.Index) == OVRHand.TrackingConfidence.High)
                {
                    if (result)
                    {
                        if (!canTap) return;
                        StartCoroutine(DebounceTapInput());
                        playAreaPoints.Add(rayHit);
                        playAreaSpheres.Add(Instantiate(pointPrefab, rayHit, Quaternion.identity));
                    }
                }
                break;
            case State.Finished:
                break;
        }
    }
    
    private IEnumerator DebounceTapInput()
    {
        canTap = false;
        yield return new WaitForSeconds(0.3f);
        canTap = true;
    }

    private void TryToStartGame()
    {
        if (CreateLargestInnerRectangle(playAreaPoints))
        {
            StartGame();
        }
    }

    private void ShowBallPreview(Vector3 position, bool result)
    {
        if (result)
        {
            pointPreviewPrefab.SetActive(true);
            pointPreviewPrefab.transform.position = position;
        }
        else
        {
            pointPreviewPrefab.SetActive(false);
        }
    }
    
    private bool CreateLargestInnerRectangle(List<Vector3> points)
    {
        if (points.Count < 3) return false;
        
        // project points onto the plane
        List<Vector2> projectedPoints = points.Select(p => new Vector2(p.x, p.z)).ToList();
        
        // find the largest rectangle using https://github.com/Evryway/lir/tree/master
        Bound2D bounds;
        float angle;
        if (LargestInteriorRectangle.CalculateLargestInteriorRectangleWithAngleSweep(projectedPoints.ToArray(), 1, out bounds, out angle))
        {
            // Create a new plane
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            
            // Set plane dimensions and position
            Vector3 planeSize = new Vector3(bounds.length_a / 10f, 1f, bounds.length_b / 10f);
            Vector3 colliderSize = new Vector3(bounds.length_a, Camera.main.transform.position.y + 0.5f, bounds.length_b);
            
            Vector3 center = new (bounds.centre.x, 0, bounds.centre.y);

            // Update plane's position and scale
            plane.transform.position = center;
            plane.transform.localScale = planeSize;
            plane.transform.rotation = Quaternion.Euler(0, angle, 0);
            plane.transform.SetParent(GameObject.Find("OVRCameraRig").transform);
            
            plane.GetComponent<Renderer>().material = planeMaterial;
            plane.name = "TrackedSpacePlane";
            // plane.GetComponent<MeshCollider>().enabled = false;
            // plane.layer = LayerMask.GetMask("locomotion");
            

            GameObject collider = new();
            collider.transform.position = center + new Vector3(0, colliderSize.y / 2, 0);
            collider.transform.rotation = Quaternion.Euler(0, angle, 0);
            collider.transform.localScale = new Vector3(1f, 1f, 1f);
            collider.transform.SetParent(GameObject.Find("OVRCameraRig").transform);
            collider.layer = LayerMask.NameToLayer("locomotion");

            collider.AddComponent<BoxCollider>();
            collider.GetComponent<BoxCollider>().size = colliderSize;
            collider.AddComponent<ResetTrigger>();
            var resetTrigger = collider.GetComponent<ResetTrigger>();
            resetTrigger.bodyCollider = collider.GetComponent<BoxCollider>();
            resetTrigger.RESET_TRIGGER_BUFFER = 0.05f;
            resetTrigger.Initialize();
            collider.name = "ResetCollider";
            return true;
        }
        return false;
    }
}
