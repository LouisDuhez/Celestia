using UnityEngine;

/// <summary>
/// Snaps the player's depth when the camera rotates 90° (FEZ-like mechanic).
/// 
/// ARCHITECTURE REWORK:
/// - No more Time.timeScale hacks. The snap happens instantly in one frame.
/// - Removed fragile bounds.min/max logic.
/// - Replaced with a downward Raycast to ensure the player doesn't fall off edges.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerMovementController))]
public class PlayerDepthReprojector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private camera_Manager _cameraManager;

    [Header("Detection")]
    [Tooltip("Layers considered as collidable platforms.")]
    [SerializeField] private LayerMask _platformLayers;

    [Tooltip("Distance of the BoxCast from behind the scene.")]
    [SerializeField] private float _castDistance = 60f;

    [Tooltip("How far below the player we check for ground during Edge Safety.")]
    [SerializeField] private float _groundCheckDistance = 2f;

    [Header("Debug")]
    [SerializeField] private bool _drawGizmos = true;

    private CharacterController _cc;
    private PlayerMovementController _movement;

    // Gizmo state for debugging
    private Vector3 _gizmoHitPoint;
    private Vector3 _gizmoFinalPos;
    private Vector3 _gizmoDownwardRayStart;
    private bool _gizmoDidHit;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _movement = GetComponent<PlayerMovementController>();
    }

    private void OnEnable()
    {
        if (_cameraManager != null)
            _cameraManager.OnCameraRotated += OnCameraRotated;
    }

    private void OnDisable()
    {
        if (_cameraManager != null)
            _cameraManager.OnCameraRotated -= OnCameraRotated;
    }

    /// <summary>
    /// Triggered instantly when the camera finishes its 90-degree rotation.
    /// </summary>
    private void OnCameraRotated(Vector3 oldCameraRight, Vector3 newCameraForward, Vector3 newCameraRight)
    {
        if (_movement.IsHanging) return;

        // Execute the snap immediately in the same frame. No Coroutine needed.
        ApplyDepthSnap(newCameraForward, newCameraRight);
    }

    private void ApplyDepthSnap(Vector3 newCameraForward, Vector3 newCameraRight)
    {
        Vector3 playerPos = transform.position;

        // 1. Isolate the 2D screen anchor (ignoring current depth)
        float currentDepth = Vector3.Dot(playerPos, newCameraForward);
        Vector3 anchorPoint2D = playerPos - currentDepth * newCameraForward;

        // 2. Setup the BoxCast from behind the scene
        Vector3 castOrigin = anchorPoint2D - newCameraForward * _castDistance;
        Vector3 halfExtents = new Vector3(_cc.radius, _cc.height * 0.5f, 0.05f);
        Quaternion castRot = Quaternion.LookRotation(newCameraForward, Vector3.up);

        _gizmoDidHit = false;

        if (Physics.BoxCast(
                castOrigin, halfExtents, newCameraForward,
                out RaycastHit depthHit, castRot,
                _castDistance * 2f, _platformLayers,
                QueryTriggerInteraction.Ignore))
        {
            // 3. Calculate the raw snapped position (flush against the wall)
            float hitDepth = Vector3.Dot(depthHit.point, newCameraForward);
            float snapDepth = hitDepth - _cc.radius;
            float anchorDepth = Vector3.Dot(anchorPoint2D, newCameraForward);

            Vector3 resolvedPosition = anchorPoint2D + (snapDepth - anchorDepth) * newCameraForward;

            // 4. Edge Safety Check (Prevent floating in the void)
            resolvedPosition = EnforceEdgeSafety(resolvedPosition, depthHit.point, newCameraRight);

            // 5. Teleport
            _cc.enabled = false;
            transform.position = resolvedPosition;
            _cc.enabled = true;

            // Debug Data
            _gizmoHitPoint = depthHit.point;
            _gizmoFinalPos = resolvedPosition;
            _gizmoDidHit = true;
        }
    }

    /// <summary>
    /// Casts a ray downwards. If the player is over the void, slides them laterally
    /// towards the center of the platform until ground is found.
    /// </summary>
    private Vector3 EnforceEdgeSafety(Vector3 targetPos, Vector3 hitPoint, Vector3 newCameraRight)
    {
        // Vector pointing from our target position towards the exact point we hit on the wall
        Vector3 lateralSlideVector = Vector3.Project((hitPoint - targetPos), newCameraRight);

        float distanceToCenter = lateralSlideVector.magnitude;
        if (distanceToCenter < 0.05f) return targetPos; // Already centered

        Vector3 slideDirection = lateralSlideVector.normalized;
        int maxSteps = 5;
        float stepSize = distanceToCenter / maxSteps;

        // We slide the player step-by-step towards the solid center of the platform
        for (int i = 0; i <= maxSteps; i++)
        {
            Vector3 currentTestPos = targetPos + slideDirection * (i * stepSize);

            // Cast from the center of the player, slightly above the feet
            Vector3 rayOrigin = currentTestPos + (Vector3.up * 0.1f);

            if (i == 0) _gizmoDownwardRayStart = rayOrigin; // For debug

            if (Physics.Raycast(rayOrigin, Vector3.down, _groundCheckDistance, _platformLayers, QueryTriggerInteraction.Ignore))
            {
                // Ground found! Safe to snap here.
                return currentTestPos;
            }
        }

        // Fallback: If no ground is found, snap directly to the lateral center of the hit point
        return targetPos + lateralSlideVector;
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos || !Application.isPlaying || !_gizmoDidHit) return;

        // The exact hit point on the platform face
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_gizmoHitPoint, 0.1f);

        // The final resolved position
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_gizmoFinalPos, 0.25f);

        // Draw the downward safety ray
        Gizmos.color = Color.red;
        Gizmos.DrawRay(_gizmoDownwardRayStart, Vector3.down * _groundCheckDistance);
    }
}