using System.Collections;
using UnityEngine;

/// <summary>
/// Snaps the player's depth when the camera rotates 90° (FEZ-like mechanic).
///
/// FREEZE-THEN-SNAP STRATEGY
/// -------------------------
/// On rotation the game is paused (Time.timeScale = 0) for _freezeDuration
/// real-world seconds. During the freeze:
///   1. The depth BoxCast fires (physics works at timeScale = 0).
///   2. The snap target is computed.
///   3. A downward safety cast verifies the player lands on the platform
///      and not past its edge — the lateral margin is measured from the
///      hit point to the platform's edge along the new camera right axis.
///   4. The player is teleported to the safe position.
/// After the freeze, timeScale is restored to 1.
///
/// DEPTH SNAP ALGORITHM
/// --------------------
///  BoxCast FROM behind the scene ALONG +newCameraForward.
///  First hit = foreground face of the nearest platform at the player's
///  screen position.
///  snapDepth = dot(hit.point, newForward) - capsuleRadius
///  ? capsule surface is flush against the face, not embedded in it.
///
/// EDGE SAFETY MARGIN
/// ------------------
/// After snapping depth, a downward BoxCast from the new position checks
/// whether the capsule footprint is fully over the platform.
/// The safe lateral clearance = distance from player centre to the platform
/// edge along newCameraRight. If the player would fall off the edge, their
/// lateral position is clamped inward by _edgeMargin.
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

    [Tooltip("How far behind the scene the horizontal BoxCast origin is placed. " +
             "Must be larger than the maximum depth of the scene.")]
    [SerializeField] private float _castDistance = 60f;

    [Tooltip("How far below the player the safety downward cast looks for a platform.")]
    [SerializeField] private float _groundCheckDistance = 3f;

    [Header("Freeze")]
    [Tooltip("Real-world seconds the game is frozen after a rotation " +
             "while the snap is computed and applied.")]
    [SerializeField] private float _freezeDuration = 0.3f;

    [Header("Edge Safety")]
    [Tooltip("Minimum distance (metres) between the player centre and the " +
             "edge of the platform along the new camera right axis. " +
             "The player is clamped inward if they would be closer than this.")]
    [SerializeField] private float _edgeMargin = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool _drawGizmos = true;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private CharacterController      _cc;
    private PlayerMovementController _movement;
    private Coroutine                _freezeCoroutine;

    // Gizmo state
    private Vector3 _gizmoHitPoint;
    private Vector3 _gizmoFinalPos;
    private bool    _gizmoDidHit;
    private bool    _gizmoActive;

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
    // Rotation handler
    // -------------------------------------------------------------------------

    private void OnCameraRotated(Vector3 oldCameraRight, Vector3 newCameraForward, Vector3 newCameraRight)
    {
        if (_movement.IsHanging) return;

        // If a previous freeze is still running, stop it before starting a new one.
        if (_freezeCoroutine != null)
            StopCoroutine(_freezeCoroutine);

        _freezeCoroutine = StartCoroutine(
            FreezeSnapAndResume(newCameraForward, newCameraRight));
    }

    // -------------------------------------------------------------------------
    // Freeze ? Snap ? Resume coroutine
    // -------------------------------------------------------------------------

    private IEnumerator FreezeSnapAndResume(Vector3 newCameraForward, Vector3 newCameraRight)
    {
        // ------------------------------------------------------------------
        // FREEZE — stop everything (gravity, animation, physics simulation).
        // Physics queries (BoxCast) still work at timeScale = 0.
        // ------------------------------------------------------------------
        Time.timeScale = 0f;

        // ------------------------------------------------------------------
        // SNAP — compute and apply new depth.
        // ------------------------------------------------------------------
        ApplyDepthSnap(newCameraForward, newCameraRight);

        // ------------------------------------------------------------------
        // WAIT — hold the freeze for _freezeDuration real-world seconds.
        // WaitForSecondsRealtime is unaffected by timeScale.
        // ------------------------------------------------------------------
        yield return new WaitForSecondsRealtime(_freezeDuration);

        // ------------------------------------------------------------------
        // RESUME — restore normal time.
        // ------------------------------------------------------------------
        Time.timeScale    = 1f;
        _freezeCoroutine = null;
    }

    // -------------------------------------------------------------------------
    // Snap logic (runs while timeScale = 0)
    // -------------------------------------------------------------------------

    private void ApplyDepthSnap(Vector3 newCameraForward, Vector3 newCameraRight)
    {
        Vector3 playerPos = transform.position;

        // ------------------------------------------------------------------
        // Step 1 — 2D anchor: player screen-space position (X lateral, Y up).
        // The depth component along newCameraForward is zeroed.
        // ------------------------------------------------------------------
        float   currentDepth  = Vector3.Dot(playerPos, newCameraForward);
        Vector3 anchorPoint2D = playerPos - currentDepth * newCameraForward;

        // ------------------------------------------------------------------
        // Step 2 — Horizontal BoxCast from behind the scene toward camera.
        // Box half-extents match the capsule so we don't snap into a platform
        // that is too narrow to actually hold the player.
        // ------------------------------------------------------------------
        Vector3    castOrigin    = anchorPoint2D - newCameraForward * _castDistance;
        Vector3    halfExtents   = new Vector3(_cc.radius, _cc.height * 0.5f, 0.05f);
        Quaternion castRot       = Quaternion.LookRotation(newCameraForward, Vector3.up);

        _gizmoActive = true;
        _gizmoDidHit = false;

        Vector3 resolvedPosition;

        if (Physics.BoxCast(
                castOrigin, halfExtents, newCameraForward,
                out RaycastHit depthHit, castRot,
                _castDistance * 2f, _platformLayers,
                QueryTriggerInteraction.Ignore))
        {
            // Step 3 — Depth: pull back by capsule radius so the surface of the
            // capsule (not its centre) sits flush against the platform face.
            float hitDepth    = Vector3.Dot(depthHit.point, newCameraForward);
            float snapDepth   = hitDepth - _cc.radius;
            float anchorDepth = Vector3.Dot(anchorPoint2D,  newCameraForward);

            resolvedPosition = anchorPoint2D + (snapDepth - anchorDepth) * newCameraForward;

            _gizmoHitPoint = depthHit.point;

            // Step 4 — Edge safety: check whether the player footprint is
            // fully over the platform along the new camera right axis.
            resolvedPosition = ClampToEdgeSafety(
                resolvedPosition, depthHit.collider, newCameraRight);

            _gizmoFinalPos = resolvedPosition;
            _gizmoDidHit   = true;
        }
        else
        {
            // No platform found at this screen position — keep current depth.
            // DepthSnapper will correct during the next airborne frame.
            resolvedPosition = playerPos;
            _gizmoFinalPos   = playerPos;
        }

        // ------------------------------------------------------------------
        // Step 5 — Teleport (only depth and possibly lateral position changed).
        // CharacterController is toggled to bypass internal depenetration.
        // ------------------------------------------------------------------
        _cc.enabled        = false;
        transform.position = resolvedPosition;
        _cc.enabled        = true;
    }

    // -------------------------------------------------------------------------
    // Edge safety margin
    // -------------------------------------------------------------------------

    /// <summary>
    /// Ensures the player position is at least <see cref="_edgeMargin"/> metres
    /// away from the edge of <paramref name="platform"/> along
    /// <paramref name="newCameraRight"/>.
    ///
    /// The margin varies per camera view because the "edge" that matters is
    /// always the one perpendicular to the new camera right axis (the axis
    /// the player walks along in the new view).
    /// </summary>
    private Vector3 ClampToEdgeSafety(
        Vector3    position,
        Collider   platform,
        Vector3    newCameraRight)
    {
        Bounds  b           = platform.bounds;
        float   playerRight = Vector3.Dot(position,  newCameraRight);
        float   edgeMin     = Vector3.Dot(b.min,     newCameraRight);
        float   edgeMax     = Vector3.Dot(b.max,     newCameraRight);

        // Clamp the player's lateral position so they stay _edgeMargin inside
        // the platform edges.
        float safeMin    = edgeMin + _edgeMargin;
        float safeMax    = edgeMax - _edgeMargin;

        // Guard: if the platform is narrower than 2×_edgeMargin, snap to centre.
        if (safeMin > safeMax)
        {
            float centre = (edgeMin + edgeMax) * 0.5f;
            safeMin = safeMax = centre;
        }

        float clampedRight = Mathf.Clamp(playerRight, safeMin, safeMax);
        float delta        = clampedRight - playerRight;

        return position + delta * newCameraRight;
    }

    // -------------------------------------------------------------------------
    // Debug gizmos
    // -------------------------------------------------------------------------

    private void OnDrawGizmos()
    {
        if (!_drawGizmos || !Application.isPlaying || !_gizmoActive) return;

        if (_gizmoDidHit)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_gizmoHitPoint, 0.1f);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_gizmoFinalPos, 0.25f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(_gizmoHitPoint, _gizmoFinalPos);
        }
        else
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(_gizmoFinalPos, 0.25f);
        }
    }
}
