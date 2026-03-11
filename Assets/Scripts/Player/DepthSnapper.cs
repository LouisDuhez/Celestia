using UnityEngine;

/// <summary>
/// FEZ-like depth snapping.
///
/// CORRECTED THEORY
/// ----------------
/// The previous version cast along camForward (depth axis) looking for geometry
/// at the same screen-space XY — that only works if the platform is exactly
/// at the same world height, which is never the case with staircase layouts.
///
/// The correct approach mirrors exactly what a camera "sees":
///
///   1. Cast DOWNWARD (Vector3.down) from the player's feet position.
///   2. Use a BoxCast whose half-extents are:
///        x = charRadius  (screen-space width — real world X or Z depending on cam)
///        y = smallValue  (we only want hits directly below)
///        z = maxDepth/2  (very deep along camForward so we catch platforms at
///                         any depth that are visually aligned)
///   3. Rotate the box so its Z axis aligns with camForward.
///
///   This creates a "slab" that is wide in depth (catches anything behind/in front)
///   but thin in height, cast downward — exactly what the camera projects.
///
/// SNAP MATH
/// ---------
///   Only the component of position along camForward is changed:
///     depthDelta = dot(hitPoint, camForward) - dot(playerPos, camForward)
///     newPos     = playerPos + depthDelta * camForward
///
/// WHEN TO SNAP
/// ------------
///   - Airborne (!isGrounded)
///   - Falling OR just left ground (velocity.y &lt;= _maxUpSpeed, loose gate)
///   - Cooldown elapsed
///   - Not hanging on a ledge
///   - Hit normal points upward (walkable surface)
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerMovementController))]
public class DepthSnapper : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("References")]
    [Tooltip("Camera manager used to read the current virtual camera's forward axis. " +
             "Avoids reading Camera.main which is interpolated during Cinemachine blends.")]
    [SerializeField] private camera_Manager _cameraManager;

    [Header("Detection")]
    [Tooltip("Layers considered as walkable ground for depth snapping")]
    [SerializeField] private LayerMask _groundLayers;

    [Tooltip("How far below the player's feet the downward cast travels")]
    [SerializeField] private float _castDownDistance = 8f;

    [Tooltip("Half-depth of the detection slab along the camera forward axis")]
    [SerializeField] private float _depthSearchHalfExtent = 30f;

    [Header("Snap Gate")]
    [Tooltip("Allow snap even on the upward arc of a jump (set to a positive value). " +
             "Increase if snapping misses when jumping onto higher platforms.")]
    [SerializeField] private float _maxUpSpeed = 2f;

    [Tooltip("Seconds after a snap before another snap can occur")]
    [SerializeField] private float _snapCooldown = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool _drawGizmos = true;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private CharacterController      _cc;
    private PlayerMovementController _movement;
    private float                    _snapCooldownTimer;

    // Gizmo data
    private Vector3 _gizmoOrigin;
    private Vector3 _gizmoHalfExtents;
    private Quaternion _gizmoOrientation;
    private bool    _gizmoHit;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _cc       = GetComponent<CharacterController>();
        _movement = GetComponent<PlayerMovementController>();
    }

    private void Update()
    {
        if (_snapCooldownTimer > 0f)
            _snapCooldownTimer -= Time.deltaTime;

        if (_movement.IsHanging)     return;
        if (_cc.isGrounded)          return;
        if (_snapCooldownTimer > 0f) return;
        // Loose gate: allow snap on the way up too (for jumping onto higher platforms)
        if (_cc.velocity.y > _maxUpSpeed) return;

        TrySnap();
    }

    // -------------------------------------------------------------------------
    // Core snap logic
    // -------------------------------------------------------------------------

    private void TrySnap()
    {
        // Read the forward axis from the active virtual camera so we get the
        // target orientation, not an interpolated blend value from Camera.main.
        Vector3 camForward;
        if (_cameraManager != null && _cameraManager.ActiveCamera != null)
            camForward = _cameraManager.ActiveCamera.transform.forward;
        else
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            camForward = cam.transform.forward;
        }

        Quaternion boxRot = Quaternion.LookRotation(camForward, Vector3.up);

        float      r    = _cc.radius;
        Vector3    half = new Vector3(r, 0.05f, _depthSearchHalfExtent);

        Vector3 origin = transform.position
                       + Vector3.up * (_cc.radius + 0.05f);

        _gizmoOrigin      = origin;
        _gizmoHalfExtents = half;
        _gizmoOrientation = boxRot;
        _gizmoHit         = false;

        if (!Physics.BoxCast(origin, half, Vector3.down,
                             out RaycastHit hit, boxRot,
                             _castDownDistance, _groundLayers,
                             QueryTriggerInteraction.Ignore))
            return;

        if (Vector3.Dot(hit.normal, Vector3.up) < 0.5f) return;

        _gizmoHit = true;

        SnapToDepth(hit.point, camForward);
    }

    // -------------------------------------------------------------------------
    // Snap execution
    // -------------------------------------------------------------------------

    private void SnapToDepth(Vector3 hitPoint, Vector3 camForward)
    {
        float depthPlayer   = Vector3.Dot(transform.position, camForward);
        float depthPlatform = Vector3.Dot(hitPoint,           camForward);
        float depthDelta    = depthPlatform - depthPlayer;

        if (Mathf.Abs(depthDelta) < 0.01f) return;

        Vector3 newPosition = transform.position + depthDelta * camForward;

        _cc.enabled        = false;
        transform.position = newPosition;
        _cc.enabled        = true;

        _snapCooldownTimer = _snapCooldown;
    }

    // -------------------------------------------------------------------------
    // Debug gizmos
    // -------------------------------------------------------------------------

    private void OnDrawGizmos()
    {
        if (!_drawGizmos || !Application.isPlaying) return;

        Gizmos.color = _gizmoHit
            ? new Color(0f, 1f, 0f, 0.35f)
            : new Color(1f, 0.3f, 0f, 0.35f);

        Gizmos.matrix = Matrix4x4.TRS(
            _gizmoOrigin + Vector3.down * (_castDownDistance * 0.5f),
            _gizmoOrientation,
            new Vector3(_gizmoHalfExtents.x * 2f,
                        _castDownDistance,
                        _gizmoHalfExtents.z * 2f)
        );
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
