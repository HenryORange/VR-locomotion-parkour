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

    public OVRInput.Controller controller;
    public LineRenderer laserPointer;
    public float laserMaxDistance = 10.0f;
    
    public GameObject environment;
    public GameObject parkourSystem;
    public GameObject taskUI;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        OVRManager.instance.isInsightPassthroughEnabled = true;
        environment.SetActive(false);
        parkourSystem.SetActive(false);
        taskUI.SetActive(false);
    }
    
    // TODO make laser pointer user meta controllers
    void Update()
    {
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
                OVRManager.instance.isInsightPassthroughEnabled = false;
                Camera.main.clearFlags = CameraClearFlags.Skybox;
                environment.SetActive(true);
                parkourSystem.SetActive(true);
                taskUI.SetActive(true);
            }
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
            // Create a new cube
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            
            // Set cube dimensions and position
            Vector3 size = new Vector3(
                bounds.length_a, // Width
                0.01f, // Height (thin for visualization)
                bounds.length_b // Depth
            );
            
            Vector3 center = new (bounds.centre.x, 0, bounds.centre.y);

            // Update cube's position and scale
            cube.transform.position = center;
            cube.transform.localScale = size;
            cube.transform.rotation = Quaternion.Euler(0, angle, 0);

            // Optional: Set cube color or material
            cube.GetComponent<Renderer>().material.color = Color.green;
            return true;
        }
        else
        {
            // TODO add an visual error message and empty the list of points, also remove drawn points
            return false;
        }
        
    }
}
