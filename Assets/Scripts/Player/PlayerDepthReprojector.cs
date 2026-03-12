using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Reprojects the player's depth onto the new camera plane after a 90-degree
/// rotation using the FEZ Screen-Space paradigm.
///
/// PIPELINE
/// --------
///  1. SaveScreenPosition() — called BEFORE the camera switches.
///     Records the player's position in the OLD camera's local space:
///       _savedRight = Dot(playerPos, oldCamRight)   ? lateral screen position
///       _savedUp    = Dot(playerPos, oldCamUp)       ? vertical screen position
///     These two scalars are the player's 2D screen coordinates and are
///     independent of which world axis the camera faces.
///
///  2. OnCameraRotated() — called AFTER the camera switches.
///     Reconstructs the ray using the NEW camera's right/up/forward:
///       rayOrigin = newCamRight * _savedRight
///                 + newCamUp   * _savedUp
///                 - newCamForward * _castDistance
///     This guarantees the ray passes through the exact same screen pixel
///     for ALL 4 camera orientations (EAST, WEST, NORTH, SOUTH).
///
///  3. SphereCastAll ? pick only the collider whose Y bounds contain the
///     player's current Y (prevents snapping to a platform on another floor).
///
///  4. Near-edge snap: hit.point depth + inward margin.
///
///  5. Clamp against the collider's far face.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerMovementController))]
public class PlayerDepthReprojector : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("References")]
    [SerializeField] private camera_Manager _cameraManager;

    [Header("Detection")]
    [Tooltip("Layers considered as collidable platforms.")]
    [SerializeField] private LayerMask _platformLayers;

    [Tooltip("Distance behind the scene the ray starts from. Must exceed total scene depth.")]
    [SerializeField] private float _castDistance = 80f;

    [Tooltip("How far inward from the near surface the player is placed on the depth axis. " +
             "Tune between 0.1 and 0.5.")]
    [SerializeField] private float _snapInwardMargin = 0.25f;

    [Tooltip("Vertical tolerance when matching the player's Y to a platform's bounds.")]
    [SerializeField] private float _yMatchTolerance = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool _drawGizmos = true;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private CharacterController      _cc;
    private PlayerMovementController _movement;

    // Player's screen-space coordinates in the OLD camera's local axes.
    // Saved before the camera switches so they are always in the correct space.
    private float _savedRight; // Dot(playerWorldPos, oldCamRight)
    private float _savedUp;    // Dot(playerWorldPos, oldCamUp)

    // -------------------------------------------------------------------------
    // Gizmo state
    // -------------------------------------------------------------------------

    private bool    _gizmoActive;
    private Ray     _gizmoRay;
    private float   _gizmoRayLength;
    private float   _gizmoCastRadius;
    private bool    _gizmoDidHit;
    private Vector3 _gizmoHitSurface;
    private Vector3 _gizmoSnapAnchor;
    private Vector3 _gizmoFinalPos;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _cc       = GetComponent<CharacterController>();
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

    // -------------------------------------------------------------------------
    // Public API — called by camera_Manager BEFORE switching active camera
    // -------------------------------------------------------------------------

    /// <summary>
    /// Projects the player's world position onto the OLD camera's right and up
    /// axes. These two scalars are the true 2D screen coordinates of the player
    /// and are preserved across the camera rotation.
    /// Must be called before ApplyPriorities() in camera_Manager.
    /// </summary>
    public void SaveScreenPosition()
    {
        if (_cameraManager == null) return;

        CinemachineCamera oldVCam = _cameraManager.ActiveCamera;
        if (oldVCam == null) return;

        Vector3 playerPos = transform.position;

        // Project the player's world position onto the current camera's local
        // right and up axes. These are the horizontal and vertical screen
        // coordinates in world units (orthographic projection).
        _savedRight = Vector3.Dot(playerPos, oldVCam.transform.right);
        _savedUp    = Vector3.Dot(playerPos, oldVCam.transform.up);
    }

    // -------------------------------------------------------------------------
    // Camera rotation event
    // -------------------------------------------------------------------------

    private void OnCameraRotated(Vector3 oldCameraRight, Vector3 oldCameraUp,
                                  Vector3 newCameraForward, Vector3 newCameraRight)
    {
        if (_movement.IsHanging) return;
        ApplyDepthSnap(newCameraForward, newCameraRight);
    }

    // -------------------------------------------------------------------------
    // Core snap logic
    // -------------------------------------------------------------------------

    private void ApplyDepthSnap(Vector3 newCameraForward, Vector3 newCameraRight)
    {
        // ?? Step 2: Rebuild the ray in the NEW camera's coordinate space ??????
        //
        // The player's saved screen coords (_savedRight, _savedUp) were measured
        // in the OLD camera's right/up space. We now re-express them using the
        // NEW camera's right/up axes. Because these are orthogonal world axes
        // (not blend-interpolated), this is exact for all 4 camera orientations.
        //
        // newCamUp = cross(newCamForward, newCamRight) reconstructed to be safe.
        // We do NOT use Camera.main.transform.up here (mid-blend = wrong).
        //
        Vector3 newCameraUp = Vector3.Cross(newCameraForward, newCameraRight);

        // Reconstruct the world-space screen position from the two saved scalars
        // expressed in the new camera's right/up frame.
        Vector3 screenWorldPos = newCameraRight * _savedRight
                               + newCameraUp    * _savedUp;

        // Ray origin is placed behind the scene by _castDistance along -forward.
        Vector3 rayOrigin = screenWorldPos - newCameraForward * _castDistance;
        Ray     screenRay = new Ray(rayOrigin, newCameraForward);

        _gizmoRay        = screenRay;
        _gizmoRayLength  = _castDistance * 2f;
        _gizmoCastRadius = _cc.radius;
        _gizmoActive     = true;
        _gizmoDidHit     = false;

        // ?? Step 3: SphereCastAll — pick platform at the player's Y ??????????
        RaycastHit[] hits = Physics.SphereCastAll(
            screenRay,
            _cc.radius,
            _castDistance * 2f,
            _platformLayers,
            QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0) return;

        float      playerY    = transform.position.y;
        RaycastHit bestHit    = default;
        float      bestDepth  = float.MaxValue;
        bool       foundMatch = false;

        foreach (RaycastHit h in hits)
        {
            Bounds hb   = h.collider.bounds;
            float  yMin = hb.min.y - _yMatchTolerance;
            float  yMax = hb.max.y + _yMatchTolerance;

            if (playerY < yMin || playerY > yMax) continue;

            // Prefer platform closest to the camera (smallest forward depth).
            float depth = Vector3.Dot(h.point, newCameraForward);
            if (!foundMatch || depth < bestDepth)
            {
                bestHit    = h;
                bestDepth  = depth;
                foundMatch = true;
            }
        }

        if (!foundMatch) return;

        _gizmoDidHit     = true;
        _gizmoHitSurface = bestHit.point;

        // ?? Step 4: Near-edge snap + inward margin ????????????????????????????
        float nearFaceDepth = Vector3.Dot(bestHit.point, newCameraForward);
        float snapDepth     = nearFaceDepth + _snapInwardMargin;

        // Clamp against far face — Max of both bounds corners handles ±X / ±Z.
        Bounds b            = bestHit.collider.bounds;
        float  farFaceDepth = Mathf.Max(
            Vector3.Dot(b.min, newCameraForward),
            Vector3.Dot(b.max, newCameraForward)
        );
        snapDepth = Mathf.Min(snapDepth, farFaceDepth);

        float playerCurrentDepth = Vector3.Dot(transform.position, newCameraForward);
        float depthDelta         = snapDepth - playerCurrentDepth;

        _gizmoSnapAnchor = transform.position + depthDelta * newCameraForward;

        if (Mathf.Abs(depthDelta) < 0.01f) return;

        _gizmoFinalPos = _gizmoSnapAnchor;

        // ?? Step 5: Teleport ??????????????????????????????????????????????????
        _cc.enabled        = false;
        transform.position = _gizmoSnapAnchor;
        _cc.enabled        = true;

        _movement.ForceGroundedState();
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_drawGizmos || !Application.isPlaying || !_gizmoActive) return;

        // Ray through the scene (blue)
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.9f);
        Gizmos.DrawRay(_gizmoRay.origin, _gizmoRay.direction * _gizmoRayLength);

        // SphereCast width (transparent blue)
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.2f);
        Gizmos.DrawWireSphere(_gizmoRay.origin, _gizmoCastRadius);
        Gizmos.DrawWireSphere(
            _gizmoRay.origin + _gizmoRay.direction * _gizmoRayLength,
            _gizmoCastRadius);

        if (!_gizmoDidHit) return;

        // Near face hit point (yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_gizmoHitSurface, 0.12f);
        Gizmos.DrawLine(_gizmoRay.origin, _gizmoHitSurface);

        // Snap anchor = near face + margin (orange)
        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawWireSphere(_gizmoSnapAnchor, 0.18f);
        Gizmos.DrawLine(_gizmoHitSurface, _gizmoSnapAnchor);

        // Final player position (green)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_gizmoFinalPos, 0.28f);
        Gizmos.DrawLine(_gizmoSnapAnchor, _gizmoFinalPos);
    }
#endif
}