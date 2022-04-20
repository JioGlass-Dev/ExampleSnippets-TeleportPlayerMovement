using JMRSDK.InputModule;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Example for movement by teleportation. 
/// Press fn1 button to start ray, swipe to change distance, press trigger to teleport or press fn1 again to stop ray
/// </summary>
/// <remarks>
/// Stop any other movement while teleportation
/// </remarks>
public class TeleportBasedMovement : MonoBehaviour, IFn1Handler, ISelectClickHandler, ITouchHandler
{
    [Header("Player Scene References")]
    [SerializeField] GameObject player;

    [Header("Prefab References")]
    [SerializeField] GameObject projectionPositionPrefab;

    [Header("Destination position properties")]
    [SerializeField, Tooltip("Default distance to start with.")] float safeStartDistance;
    [SerializeField, Tooltip("Range in which projection should be clamped to")] float minProjection, maxProjection;
    [SerializeField, Tooltip("On getting the position, the offset at which destination should be spawned at")] float yOffset;
    [SerializeField, Tooltip("Touchpad to projection distance modify multiplier")] float distanceModifyMultiplier;
    
    [Header("Projection Laser Properties")]
    [SerializeField, Tooltip("Number of elements with which projection ray should be made")] int projectionPointCount;
    [SerializeField, Tooltip("Reference to line renderer for generating trajectory")] LineRenderer rayLineRenderer;
    [SerializeField, Tooltip("Shape of the trajectory")] AnimationCurve projectionCurve;

    /// <summary> Destination projection will be shown if player can reach destination </summary>
    GameObject _destinationProjection;

    /// <summary> The distance at which the current trajectory is ending </summary>
    float _projectionDistance;

    /// <summary> Is the projection currently started </summary>
    bool _startedProjection;

    /// <summary> cached jmr ray </summary>
    Ray _jmrRay;

    /// <summary> cached pointer references </summary>
    private GameObject _laserPointer, _cursorPointer;

    /// <summary> cached destination position </summary>
    Vector3 _destinationPosition;

    /// <summary> cached projection position holder of line renderer </summary>
    Vector3 _assignedPos;
    

    /// <summary> Switch between projection and non projection mode </summary>
    public void OnFn1Action()
    {
        if (!_startedProjection)  //start projection if not started
        {
            StartTeleportRay();       
        }
        else //stop projection as already projection started
        {
            StopTeleportRay();
        }
    }
    
    /// <summary> If a valid projection is present, and player presses the trigger button, start teleport action </summary>
    /// <param name="eventData"> Click related event data </param>
    public void OnSelectClicked(SelectClickEventData eventData)
    {
        if (_startedProjection)
        {
            Teleport();
        }
    }

    /// <summary> Cache references to JMR pointers </summary>
    private void CacheLaserReferences()
    {
        _laserPointer = JMRInputManager.Instance.transform.Find("JMRLaserPointer(Clone)").gameObject;
        _cursorPointer = JMRInputManager.Instance.transform.Find("JMRPointer(Clone)").gameObject;
    }

    /// <summary> Show/Hide jmr laser </summary>
    /// <param name="visible">Visibility of JMR Laser to be set</param>
    void LaserVisibility(bool visible)
    {
        _laserPointer.SetActive(visible);
        _cursorPointer.SetActive(visible);
    }

    /// <summary> Generate a laser projection while the projection is visible </summary>
    private void LateUpdate()
    {
        if (_startedProjection)
        {
            MoveTeleportRay();
        }
    }

    /// <summary> Move the ray destination to a valid travel-able destination </summary>
    void MoveTeleportRay()
    {
        _jmrRay = JMRPointerManager.Instance.GetCurrentRay();
        _destinationPosition = _jmrRay.GetPoint(_projectionDistance);
        _destinationPosition.y = yOffset;

        //finding closest navmesh position to set travel destination travel
        if (NavMesh.SamplePosition(_destinationPosition, out var hit, _projectionDistance, NavMesh.AllAreas))
        {
            _destinationPosition = hit.position;
            _destinationProjection.transform.position = _destinationPosition;
            CreateRay(_jmrRay.origin, _destinationProjection.transform.position);
        }
    }

    /// <summary> Create a ray by marking positions on the line renderer along the way of the projection curve. </summary>
    /// <param name="origin"> start position of the ray </param>
    /// <param name="destination"> end position of the ray </param>
    private void CreateRay(Vector3 origin, Vector3 destination)
    {
        //set number of points in projection ray
        rayLineRenderer.positionCount = projectionPointCount;

        //set all points in line renderer
        for (int i = 0; i < projectionPointCount; i++)
        {
            //set the position of the projection line ith vertex  
            _assignedPos = (i * origin + (projectionPointCount - i - 1) * destination) / (projectionPointCount - 1);
            
            //change the y height based on the projection curve
            float evaluatedHeight = projectionCurve.Evaluate((float)(projectionPointCount - i - 1) / (projectionPointCount - 1));
            _assignedPos.y = origin.y * evaluatedHeight + destination.y * (1 - evaluatedHeight);
            
            //set the position of the vertex to line renderer
            rayLineRenderer.SetPosition(i, _assignedPos);
        }
    }

    /// <summary> Show the projection, hide JMR Laser </summary>
    void StartTeleportRay()
    {
        // If no references to lasers is present, cache them
        if (_laserPointer == null)
        {
            CacheLaserReferences();
        }
        LaserVisibility(false);

        _jmrRay = JMRPointerManager.Instance.GetCurrentRay();

        _projectionDistance = safeStartDistance;

        // get destination position and instantiate projection destination effect
        _destinationPosition = _jmrRay.GetPoint(_projectionDistance);
        _destinationPosition.y = yOffset;
        _destinationProjection = Instantiate(projectionPositionPrefab, _destinationPosition, Quaternion.identity);

        _startedProjection = true;
    }

    /// <summary> Show JMR Laser and hide projection </summary>
    void StopTeleportRay()
    {
        LaserVisibility(true);

        Destroy(_destinationProjection);
        _startedProjection = false;
        
        // Create a null ray to hide projection
        CreateRay(Vector3.zero, Vector3.zero);
    }

    /// <summary> move player to destinationProjection position and stop ray </summary>
    void Teleport()
    {
        player.transform.position = _destinationProjection.transform.position - Vector3.up * yOffset;

        StopTeleportRay();
    }

    /// <summary> Change the projection destination by distance multiplier based on touch value </summary>
    /// <param name="eventData"> event data for touch </param>
    /// <param name="touchData"> touch position </param>
    public void OnTouchUpdated(TouchEventData eventData, Vector2 touchData)
    {
        _projectionDistance = Mathf.Clamp(_projectionDistance - (touchData.y - 0.5f) * distanceModifyMultiplier, minProjection, maxProjection);
    }
    
    #region not implemented methods
    
    public void OnTouchStart(TouchEventData eventData, Vector2 touchData)
    {   }

    public void OnTouchStop(TouchEventData eventData, Vector2 touchData)
    {    }
    
    #endregion
}
