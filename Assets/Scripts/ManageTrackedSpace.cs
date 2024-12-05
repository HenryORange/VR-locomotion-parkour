using System;
using System.Collections.Generic;
using System.Linq;
using Meta.XR.MRUtilityKit;
using Unity.XR.Oculus;
using UnityEngine;
using UnityEngine.XR;

public class ManageTrackedSpace : MonoBehaviour
{
    private OVRBoundary boundary;
    private Vector3[] points;

    void Start()
    {
        boundary = OVRManager.boundary;
        points = boundary.GetGeometry(OVRBoundary.BoundaryType.PlayArea);
        foreach (var point in points)
        {
            Debug.LogError(point);
        }
        
        foreach (var point in points)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = point;
            cube.transform.localScale = new Vector3(0.1f, 1f, 0.1f);
            cube.GetComponent<MeshRenderer>().material.color = Color.red;
        }
    }
    

    // Update is called once per frame
    void Update()
    { // maybe with room setup?
        // var room = MRUK.Instance?.GetCurrentRoom();
        // foreach (var point in room.GetRoomOutline())
        // {
        //     Debug.LogError(point.ToString());
        // }
        // foreach (var point in room.GetRoomOutline())
        // {
        //     var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        //     cube.transform.position = point;
        //     cube.transform.localScale = new Vector3(0.1f, 1f, 0.1f);
        //     cube.GetComponent<MeshRenderer>().material.color = Color.red;
        // }
    }
}