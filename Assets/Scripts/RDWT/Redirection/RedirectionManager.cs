using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Oculus.Interaction;
using Redirection;
using TMPro;
using Unity.Mathematics;
using Random = UnityEngine.Random;

public class RedirectionManager : MonoBehaviour
{
    public enum MovementController
    {
        Keyboard,
        AutoPilot,
        Tracker
    };

    [Tooltip("Select if you wish to run simulation from commandline in Unity batchmode.")]
    public bool runInTestMode = false;

    [Tooltip("How user movement is controlled.")]
    public MovementController MOVEMENT_CONTROLLER = MovementController.Tracker;

    [Tooltip("Maximum translation gain applied")] [Range(0, 5)]
    public float MAX_TRANS_GAIN = 0.26F;

    [Tooltip("Minimum translation gain applied")] [Range(-0.99F, 0)]
    public float MIN_TRANS_GAIN = -0.14F;

    [Tooltip("Maximum rotation gain applied")] [Range(0, 5)]
    public float MAX_ROT_GAIN = 0.49F;

    [Tooltip("Minimum rotation gain applied")] [Range(-0.99F, 0)]
    public float MIN_ROT_GAIN = -0.2F;

    [Tooltip("Radius applied by curvature gain")] [Range(1, 23)]
    public float CURVATURE_RADIUS = 7.5F;

    [Tooltip("The game object that is being physically tracked (probably user's head)")]
    public Transform headTransform;

    [Tooltip("Use simulated framerate in auto-pilot mode")]
    public bool useManualTime = false;

    [Tooltip("Target simulated framerate in auto-pilot mode")]
    public float targetFPS = 60;


    [HideInInspector] public Transform body;
    [HideInInspector] public Transform trackedSpace;

    [HideInInspector] public Redirector redirector;
    [HideInInspector] public Resetter resetter;
    [HideInInspector] public ResetTrigger resetTrigger;
    [HideInInspector] public TrailDrawer trailDrawer;
    [HideInInspector] public SimulationManager simulationManager;
    [HideInInspector] public SimulatedWalker simulatedWalker;
    [HideInInspector] public KeyboardController keyboardController;
    [HideInInspector] public SnapshotGenerator snapshotGenerator;
    [HideInInspector] public StatisticsLogger statisticsLogger;

    [HideInInspector] public Vector3 currPos, currPosReal, prevPos, prevPosReal;
    [HideInInspector] public Vector3 currDir, currDirReal, prevDir, prevDirReal;
    [HideInInspector] public Vector3 deltaPos;
    [HideInInspector] public float deltaDir;
    [HideInInspector] public Transform targetWaypoint;


    [HideInInspector] public bool inReset = false;

    private bool canReset = true;

    [HideInInspector] public string startTimeOfProgram;

    private float simulatedTime = 0;

    public float velocity = 0;
    
    private const float GroundFriction = (float)(0.005 * 9.81);

    private float[,] velocityRange = { { 3, 5 }, { 8, 10 }, { 12, 15 } };

    private class VelocityVector
    {
        public Vector3 Direction;
        public float Velocity;

        public VelocityVector(Vector3 direction, float velocity)
        {
            Direction = direction;
            Velocity = velocity;
        }
    }
    
    private List<VelocityVector> velocities = new List<VelocityVector>();

    private Camera camera;

    public enum SpeedStates
    {
        Slow,
        Medium,
        Fast
    }

    private SpeedStates currentSpeedState = SpeedStates.Medium;

    private Color selectedColor;
    private Color unselectedColor;

    public GameObject slowButtonText;
    public GameObject mediumButtonText;
    public GameObject fastButtonText;
    public GameObject spinButtonText;

    public ActiveStateSelector thumbsDown;
    public ParkourCounter parkourCounter;
    public OVRCameraRig rig;

    void Awake()
    {
    }

    // Use this for initialization
    void Start()
    {
        startTimeOfProgram = System.DateTime.Now.ToString("yyyy MM dd HH:mm:ss");

        GetBody();
        GetTrackedSpace();

        GetSimulationManager();
        SetReferenceForSimulationManager();
        simulationManager.Initialize();

        GetRedirector();
        GetResetter();
        GetResetTrigger();
        GetTrailDrawer();

        GetSnapshotGenerator();
        GetStatisticsLogger();
        SetReferenceForRedirector();
        SetReferenceForResetter();
        SetReferenceForResetTrigger();
        SetBodyReferenceForResetTrigger();
        SetReferenceForTrailDrawer();

        SetReferenceForSimulatedWalker();
        SetReferenceForKeyboardController();
        SetReferenceForSnapshotGenerator();
        camera = Camera.main;

        selectedColor = new Color(1, 31 / 255f, 63 / 255f);
        unselectedColor = new Color(1, 1, 1);

        SetReferenceForStatisticsLogger();

        // The rule is to have RedirectionManager call all "Awake"-like functions that rely on RedirectionManager as an "Initialize" call.
        resetTrigger.Initialize();
        // Resetter needs ResetTrigger to be initialized before initializing itself
        if (resetter != null)
            resetter.Initialize();

        if (runInTestMode)
        {
            MOVEMENT_CONTROLLER = MovementController.AutoPilot;
        }

        thumbsDown.WhenSelected += () =>
        {
            if (!parkourCounter.parkourStart) return;
            rig.transform.position = parkourCounter.currentRespawnPos;
            velocities.Clear();
        };
    }

    // Update is called once per frame
    void Update()
    {
        var forward = camera.transform.forward;
        forward.y = 0;
        transform.position += forward.normalized * (Time.deltaTime * velocity);
        var resultingTransform = Vector3.zero;
        // foreach (var v in velocities)
        // {
        //     resultingTransform += v.Direction * (v.Velocity * Time.deltaTime);
        //     v.Velocity = v.Velocity > 0 ? v.Velocity - GroundFriction : 0;
        //     Debug.LogWarning(v.Velocity);
        // }
        // velocities.RemoveAll(v => v.Velocity == 0);
        //
        // transform.position += resultingTransform;

        velocity = velocity > 0 ? velocity - GroundFriction : 0;
    }

    public void RedirectionToggle()
    {
        spinButtonText.GetComponent<TextMeshPro>().color = canReset ? unselectedColor : selectedColor;
        canReset = !canReset;
    }

    public void OnSlowSelected()
    {
        currentSpeedState = SpeedStates.Slow;
        ChangeSelected("Slow");
    }

    public void OnMediumSelected()
    {
        currentSpeedState = SpeedStates.Medium;
        ChangeSelected("Medium");
    }

    public void OnFastSelected()
    {
        currentSpeedState = SpeedStates.Fast;
        ChangeSelected("Fast");
    }

    private void ChangeSelected(string state)
    {
        switch (state)
        {
            case "Slow":
                slowButtonText.GetComponent<TextMeshPro>().color = selectedColor;
                mediumButtonText.GetComponent<TextMeshPro>().color = unselectedColor;
                fastButtonText.GetComponent<TextMeshPro>().color = unselectedColor;
                break;
            case "Medium":
                slowButtonText.GetComponent<TextMeshPro>().color = unselectedColor;
                mediumButtonText.GetComponent<TextMeshPro>().color = selectedColor;
                fastButtonText.GetComponent<TextMeshPro>().color = unselectedColor;
                break;
            case "Fast":
                slowButtonText.GetComponent<TextMeshPro>().color = unselectedColor;
                mediumButtonText.GetComponent<TextMeshPro>().color = unselectedColor;
                fastButtonText.GetComponent<TextMeshPro>().color = selectedColor;
                break;
        }
    }

    void LateUpdate()
    {
        simulatedTime += 1.0f / targetFPS;

        //if (MOVEMENT_CONTROLLER == MovementController.AutoPilot)
        //    simulatedWalker.WalkUpdate();

        UpdateCurrentUserState();
        CalculateStateChanges();

        // BACK UP IN CASE UNITY TRIGGERS FAILED TO COMMUNICATE RESET (Can happen in high speed simulations)
        if (resetter != null && !inReset && resetter.IsUserOutOfBounds())
        {
            Debug.LogWarning("Reset Aid Helped!");
            OnResetTrigger();
        }

        if (inReset)
        {
            if (resetter != null)
            {
                resetter.ApplyResetting();
            }
        }
        else
        {
            if (redirector != null)
            {
                redirector.ApplyRedirection();
            }
        }

        // statisticsLogger.UpdateStats();

        UpdatePreviousUserState();

        //UpdateBodyPose();
    }

    public float GetDeltaTime()
    {
        if (useManualTime)
            return 1.0f / targetFPS;
        else
            return Time.deltaTime;
    }

    public float GetTime()
    {
        if (useManualTime)
            return simulatedTime;
        else
            return Time.time;
    }

    void UpdateBodyPose()
    {
        body.position = Utilities.FlattenedPos3D(headTransform.position);
        body.rotation = Quaternion.LookRotation(Utilities.FlattenedDir3D(headTransform.forward), Vector3.up);
    }

    void SetReferenceForRedirector()
    {
        if (redirector != null)
            redirector.redirectionManager = this;
    }

    void SetReferenceForResetter()
    {
        if (resetter != null)
            resetter.redirectionManager = this;
    }

    void SetReferenceForResetTrigger()
    {
        if (resetTrigger != null)
            resetTrigger.redirectionManager = this;
    }

    void SetBodyReferenceForResetTrigger()
    {
        if (resetTrigger != null && body != null)
        {
            // NOTE: This requires that getBody gets called before this
            resetTrigger.bodyCollider = body.GetComponentInChildren<CapsuleCollider>();
        }
    }

    void SetReferenceForTrailDrawer()
    {
        if (trailDrawer != null)
        {
            trailDrawer.redirectionManager = this;
        }
    }

    void SetReferenceForSimulationManager()
    {
        if (simulationManager != null)
        {
            simulationManager.redirectionManager = this;
        }
    }

    void SetReferenceForSimulatedWalker()
    {
        if (simulatedWalker != null)
        {
            simulatedWalker.redirectionManager = this;
        }
    }

    void SetReferenceForKeyboardController()
    {
        if (keyboardController != null)
        {
            keyboardController.redirectionManager = this;
        }
    }

    void SetReferenceForSnapshotGenerator()
    {
        if (snapshotGenerator != null)
        {
            snapshotGenerator.redirectionManager = this;
        }
    }

    void SetReferenceForStatisticsLogger()
    {
        if (statisticsLogger != null)
        {
            statisticsLogger.redirectionManager = this;
        }
    }

    void GetRedirector()
    {
        redirector = this.gameObject.GetComponent<Redirector>();
        if (redirector == null)
            this.gameObject.AddComponent<NullRedirector>();
        redirector = this.gameObject.GetComponent<Redirector>();
    }

    void GetResetter()
    {
        resetter = this.gameObject.GetComponent<Resetter>();
        if (resetter == null)
            this.gameObject.AddComponent<NullResetter>();
        resetter = this.gameObject.GetComponent<Resetter>();
    }

    void GetResetTrigger()
    {
        resetTrigger = this.gameObject.GetComponentInChildren<ResetTrigger>();
    }

    void GetTrailDrawer()
    {
        trailDrawer = this.gameObject.GetComponent<TrailDrawer>();
    }

    void GetSimulationManager()
    {
        simulationManager = this.gameObject.GetComponent<SimulationManager>();
    }

    void GetSnapshotGenerator()
    {
        snapshotGenerator = this.gameObject.GetComponent<SnapshotGenerator>();
    }

    void GetStatisticsLogger()
    {
        statisticsLogger = this.gameObject.GetComponent<StatisticsLogger>();
    }

    void GetBody()
    {
        body = transform.Find("Body");
    }

    void GetTrackedSpace()
    {
        trackedSpace = transform.Find("Tracked Space");
    }

    void GetTargetWaypoint()
    {
        targetWaypoint = transform.Find("Target Waypoint").gameObject.transform;
    }

    void UpdateCurrentUserState()
    {
        currPos = Utilities.FlattenedPos3D(headTransform.position);
        currPosReal = Utilities.GetRelativePosition(currPos, this.transform);
        currDir = Utilities.FlattenedDir3D(headTransform.forward);
        currDirReal = Utilities.FlattenedDir3D(Utilities.GetRelativeDirection(currDir, this.transform));
    }

    void UpdatePreviousUserState()
    {
        prevPos = Utilities.FlattenedPos3D(headTransform.position);
        prevPosReal = Utilities.GetRelativePosition(prevPos, this.transform);
        prevDir = Utilities.FlattenedDir3D(headTransform.forward);
        prevDirReal = Utilities.FlattenedDir3D(Utilities.GetRelativeDirection(prevDir, this.transform));
    }

    void CalculateStateChanges()
    {
        deltaPos = currPos - prevPos;
        deltaDir = Utilities.GetSignedAngle(prevDir, currDir);
    }

    public void OnResetTrigger()
    {
        //print("RESET TRIGGER");
        if (inReset || !canReset)
            return;
        //print("NOT IN RESET");
        //print("Is Resetter Null? " + (resetter == null));
        if (resetter != null && resetter.IsResetRequired())
        {
            //print("RESET WAS REQUIRED");
            resetter.InitializeReset();
            inReset = true;
        }
    }

    public void OnResetEnd()
    {
        //print("RESET END");
        resetter.FinalizeReset();
        velocity = Random.Range(velocityRange[(int)currentSpeedState, 0], velocityRange[(int)currentSpeedState, 1]);
        // velocities.Add(new VelocityVector(camera.transform.forward.normalized, velocity));
        inReset = false;
    }

    public void RemoveRedirector()
    {
        this.redirector = this.gameObject.GetComponent<Redirector>();
        if (this.redirector != null)
            Destroy(redirector);
        redirector = null;
    }

    public void UpdateRedirector(System.Type redirectorType)
    {
        RemoveRedirector();
        this.redirector = (Redirector)this.gameObject.AddComponent(redirectorType);
        //this.redirector = this.gameObject.GetComponent<Redirector>();
        SetReferenceForRedirector();
    }

    public void RemoveResetter()
    {
        this.resetter = this.gameObject.GetComponent<Resetter>();
        if (this.resetter != null)
            Destroy(resetter);
        resetter = null;
    }

    public void UpdateResetter(System.Type resetterType)
    {
        RemoveResetter();
        if (resetterType != null)
        {
            this.resetter = (Resetter)this.gameObject.AddComponent(resetterType);
            //this.resetter = this.gameObject.GetComponent<Resetter>();
            SetReferenceForResetter();
            if (this.resetter != null)
                this.resetter.Initialize();
        }
    }

    public void UpdateTrackedSpaceDimensions(float x, float z)
    {
        trackedSpace.localScale = new Vector3(x, 1, z);
        resetTrigger.Initialize();
        if (this.resetter != null)
            this.resetter.Initialize();
    }
}