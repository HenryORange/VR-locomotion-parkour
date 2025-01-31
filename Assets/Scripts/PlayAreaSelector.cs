using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Evryway;
using Oculus.Interaction;
using Oculus.Interaction.Samples;
using Oculus.Platform.Models;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class PlayAreaSelector : MonoBehaviour
{
    public GameObject pointPrefab;
    public Transform groundPlane;
    private List<Vector3> playAreaPoints = new ();
    private List<GameObject> playAreaSpheres = new ();
    public GameObject redirectedUser;
    public GameObject speedSelector;

    public OVRInput.Controller controller;
    public LineRenderer laserPointer;
    public float laserMaxDistance = 10.0f;
    
    public GameObject environment;
    public GameObject parkourSystem;
    public GameObject taskUI;

    private State state;

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
        state = State.Setup;
        
        // playAreaPoints.Add(new Vector3(1.0f, 0.0f, 1.0f));
        // playAreaPoints.Add(new Vector3(-1.0f, 0.0f, 1.0f));
        // playAreaPoints.Add(new Vector3(-1.0f, 0.0f, -1.0f));
        // playAreaPoints.Add(new Vector3(1.0f, 0.0f, -1.0f));
        // StartGame();
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
        
        // stop this script from doing stuff
        state = State.Finished;
        laserPointer.enabled = false;
        foreach(var sphere in playAreaSpheres) sphere.SetActive(false);
        
        // enable redirected walking
        var redirection = redirectedUser.GetComponent<RedirectionManager>();
        redirection.enabled = true;
        var simulation = redirectedUser.GetComponent<SimulationManager>();
        simulation.enabled = true;
        speedSelector.SetActive(true);
        groundPlane.gameObject.SetActive(false);
    }
    
    void Update()
    {
        switch (state)
        {
            case State.Setup:
                Vector3 controllerPosition = OVRInput.GetLocalControllerPosition(controller);
                Quaternion controllerRotation = OVRInput.GetLocalControllerRotation(controller);
        
                Ray ray = new Ray(controllerPosition, controllerRotation * Vector3.forward);

                UnityEngine.Plane plane = new(groundPlane.up, groundPlane.position);
        
                bool result = plane.Raycast(ray, out float hit);
                var rayHit = ray.GetPoint(hit);
                UpdateLaserPointer(controllerPosition, ray.direction, result, rayHit);
        
                // TODO only do if there isn't a point yet, also add resetting the bound creation and button to confirm chosen bounds
                if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger))
                {
                    if (result)
                    {
                        playAreaPoints.Add(rayHit);
                        playAreaSpheres.Add(Instantiate(pointPrefab, rayHit, Quaternion.identity));
                    }
                }

                if (OVRInput.GetDown(OVRInput.Button.Two))
                {
                    if (CreateLargestInnerRectangle(playAreaPoints))
                    {
                        StartGame();
                    }
                }
                break;
            case State.Finished:
                break;
        }
    }

    public void UpdateLaserPointer(Vector3 origin, Vector3 direction, bool result,  Vector3 hit)
    {
        laserPointer.SetPosition(0, origin);
        if (result)
        {
            laserPointer.SetPosition(1, hit);
        }
        else
        {
            laserPointer.SetPosition(1, origin + direction * laserMaxDistance);
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
            Vector3 colliderSize = new Vector3(bounds.length_a, 2f, bounds.length_b);
            
            Vector3 center = new (bounds.centre.x, 0, bounds.centre.y);

            // Update plane's position and scale
            plane.transform.position = center;
            plane.transform.localScale = planeSize;
            plane.transform.rotation = Quaternion.Euler(0, angle, 0);
            plane.transform.parent = transform;
            
            plane.GetComponent<Renderer>().material.color = Color.green;
            plane.name = "TrackedSpacePlane";
            

            GameObject collider = new();
            collider.transform.position = center + new Vector3(0f, 1f, 0f);
            collider.transform.localScale = colliderSize;
            collider.transform.rotation = Quaternion.Euler(0, angle, 0);
            collider.transform.parent = transform;

            collider.AddComponent<BoxCollider>();
            collider.AddComponent<ResetTrigger>();
            var resetTrigger = collider.GetComponent<ResetTrigger>();
            resetTrigger.bodyCollider = collider.GetComponent<BoxCollider>();
            resetTrigger.RESET_TRIGGER_BUFFER = 0.0f;
            collider.name = "ResetCollider";
            return true;
        }
        else
        {
            // TODO add an visual error message and empty the list of points, also remove drawn points
            return false;
        }
        
    }
}
