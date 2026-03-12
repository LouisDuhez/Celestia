using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Pure ledge detection component for FEZ-like orthographic gameplay.
/// Does NOT modify physics, transform or animator state — only detects
/// and returns grab data. LedgeLocator (or any consumer) owns the grab logic.
///
/// DETECTION PIPELINE
/// ??????????????????
///  STEP 1  Camera axes — read from active vCam, never Camera.main (mid-blend).
///
///  STEP 2  OverlapBox tunnel — same approach as FezClimbableAlignment.
///          A tall box oriented with the camera covers the full scene depth
///          (180 units by default). Every collider on the ledge layer that
///          overlaps the player's column is a candidate.
///
///  STEP 3  Best-candidate selection — among all hits, pick the one whose
///          centre is laterally closest to the player on the screen axis
///          (X when camera faces ±Z, Z when camera faces ±X).
///          Height gate: skip colliders whose bottom is above the player's
///          max reach height.
///
///  STEP 4  Top-edge raycast — fire a downward ray from above the chosen
///          collider's top surface to find the exact ledge Y.
///
///  STEP 5  Output — build GrabPosition and depth-snap it to the collider
///          centre on the camera forward axis (TargetDepthPosition).
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerMovementController))]
public class LedgeDetector : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("References")]
    [SerializeField] private camera_Manager _cameraManager;

    [Header("Layers")]
    [Tooltip("Layer(s) that contain climbable ledge/wall colliders.")]
    [SerializeField] private LayerMask _ledgeLayers;

    [Header("Detection Tunnel")]
    [Tooltip("Width of the detection tunnel on the lateral screen axis (metres). " +
             "Keep tight (? char radius) so only ledges in front are detected.")]
    [SerializeField] private float _tunnelWidth = 0.4f;

    [Tooltip("Height of the detection tunnel. Should cover the full player height + reach.")]
    [SerializeField] private float _tunnelHeight = 2.5f;

    [Tooltip("Half-depth of the detection tunnel along camForward. Must cover the full scene depth.")]
    [SerializeField] private float _tunnelDepthHalf = 90f;

    [Header("Height Gate")]
    [Tooltip("Minimum ledge top Y relative to player feet (avoids ground-level grabs).")]
    [SerializeField] private float _minLedgeHeight = 0.3f;

    [Tooltip("Maximum ledge top Y relative to player feet (avoids out-of-reach grabs).")]
    [SerializeField] private float _maxLedgeHeight = 2.2f;

    [Header("Top Edge Raycast")]
    [Tooltip("How far above the collider top the downward ray starts.")]
    [SerializeField] private float _topRayStartAbove = 0.3f;

    [Tooltip("Maximum downward search distance for the ledge top surface.")]
    [SerializeField] private float _topRayDistance = 0.8f;

    [Header("Grab Position")]
    [Tooltip("Fine-tune the vertical position of the hang snap.\n" +
             "0 = hands exactly at ledge top.\n" +
             "Negative = hands slightly below the ledge (more natural).\n" +
             "Positive = hands above the ledge.")]
    [SerializeField] private float _hangYOffset = -0.1f;

    [Header("Debug")]
    [SerializeField] private bool _drawGizmos = true;

    // -------------------------------------------------------------------------
    // Gizmo state
    // -------------------------------------------------------------------------

    private bool       _gizmoEnabled;
    private Vector3    _gizmoBoxCenter;
    private Vector3    _gizmoBoxHalf;
    private Quaternion _gizmoBoxRot     = Quaternion.identity;
    private bool       _gizmoWallHit;
    private Bounds     _gizmoWallBounds;
    private bool       _gizmoTopRayHit;
    private Vector3    _gizmoTopRayOrigin;
    private float      _gizmoTopRayDist;
    private Vector3    _gizmoTopRayHitPoint;
    private bool       _gizmoHadResult;
    private Vector3    _gizmoGrabPosition;
    private Vector3    _gizmoTargetDepthPosition;

    // -------------------------------------------------------------------------
    // Private references
    // -------------------------------------------------------------------------

    private CharacterController      _cc;
    private PlayerMovementController _movement;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _cc       = GetComponent<CharacterController>();
        _movement = GetComponent<PlayerMovementController>();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Attempts to detect a climbable ledge in the player's visual forward
    /// direction. Returns a <see cref="LedgeGrabData"/> if a valid ledge is
    /// found, or <c>null</c> otherwise.
    /// </summary>
    /// <param name="inputSign">
    /// +1 = moving right on screen, -1 = moving left.
    /// The value is used only to disambiguate among equidistant candidates.
    /// Pass 0 to use the last known direction (handled by the caller).
    /// </param>
    public LedgeGrabData? TryDetect(float inputSign)
    {
        _gizmoEnabled   = true;
        _gizmoWallHit   = false;
        _gizmoTopRayHit = false;
        _gizmoHadResult = false;

        // ?? Step 1: Camera axes ???????????????????????????????????????????????
        Vector3 camRight;
        Vector3 camForward;

        if (_cameraManager != null && _cameraManager.ActiveCamera != null)
        {
            camRight   = _cameraManager.ActiveCamera.transform.right;
            camForward = _cameraManager.ActiveCamera.transform.forward;
        }
        else
        {
            Camera cam = Camera.main;
            if (cam == null) return null;
            camRight   = cam.transform.right;
            camForward = cam.transform.forward;
        }

        // Flatten to horizontal plane.
        camRight   = new Vector3(camRight.x,   0f, camRight.z).normalized;
        camForward = new Vector3(camForward.x,  0f, camForward.z).normalized;

        // The camera rotation used to orient the OverlapBox.
        Quaternion camRot = Quaternion.LookRotation(camForward, Vector3.up);

        // ?? Step 2: OverlapBox tunnel ?????????????????????????????????????????
        //
        // Identical strategy to FezClimbableAlignment:
        //   • Box oriented with the camera (local Z = camForward)
        //   • Very deep along camForward  ? catches ledges at any world depth
        //   • Narrow laterally            ? only ledges in the player's column
        //   • Centre offset upward        ? covers torso + reach area
        //
        Vector3 boxCenter = transform.position + Vector3.up * (_tunnelHeight * 0.5f);
        Vector3 boxHalf   = new Vector3(_tunnelWidth * 0.5f,
                                        _tunnelHeight * 0.5f,
                                        _tunnelDepthHalf);

        _gizmoBoxCenter = boxCenter;
        _gizmoBoxHalf   = boxHalf;
        _gizmoBoxRot    = camRot;

        Collider[] hits = Physics.OverlapBox(
            boxCenter, boxHalf, camRot,
            _ledgeLayers, QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0) return null;

        // ?? Step 3: Best-candidate selection ??????????????????????????????????
        //
        // "facingZ" mirrors FezClimbableAlignment: when the camera mainly faces
        // along Z, the lateral axis is X — and vice versa.
        //
        bool facingZ = Mathf.Abs(camForward.z) > Mathf.Abs(camForward.x);

        Collider bestCollider  = null;
        float    bestLateral   = float.MaxValue;

        foreach (Collider col in hits)
        {
            if (col.gameObject == gameObject) continue;
            if (col.isTrigger)               continue;

            // Height gate: skip if the collider bottom is above max reach.
            if (col.bounds.min.y > transform.position.y + _maxLedgeHeight) continue;

            // Height gate: skip if the collider top is below min reach.
            if (col.bounds.max.y < transform.position.y + _minLedgeHeight) continue;

            // Pick the candidate that is laterally closest to the player
            // on the screen-horizontal axis (same logic as FezClimbableAlignment).
            float lateral = facingZ
                ? Mathf.Abs(transform.position.x - col.bounds.center.x)
                : Mathf.Abs(transform.position.z - col.bounds.center.z);

            if (lateral < bestLateral)
            {
                bestLateral   = lateral;
                bestCollider  = col;
            }
        }

        if (bestCollider == null) return null;

        _gizmoWallHit    = true;
        _gizmoWallBounds = bestCollider.bounds;

        // ?? Step 4: Top-edge raycast ??????????????????????????????????????????
        //
        // Fire downward from above the collider top to get the exact ledge Y.
        float   wallTopY     = bestCollider.bounds.max.y;
        Vector3 topRayOrigin = new Vector3(
            transform.position.x,
            wallTopY + _topRayStartAbove,
            transform.position.z
        );

        _gizmoTopRayOrigin = topRayOrigin;
        _gizmoTopRayDist   = _topRayDistance;

        if (!Physics.Raycast(topRayOrigin, Vector3.down, out RaycastHit topHit,
                             _topRayDistance, _ledgeLayers, QueryTriggerInteraction.Ignore))
        {
            // Fallback: use bounds.max.y directly if the ray misses.
            // This handles cases where the ray XZ is slightly off-centre.
            // We still proceed — the top surface is the collider's max Y.
        }

        float ledgeTopY = topHit.collider != null ? topHit.point.y : wallTopY;

        _gizmoTopRayHit      = topHit.collider != null;
        _gizmoTopRayHitPoint = topHit.collider != null ? topHit.point : new Vector3(topRayOrigin.x, wallTopY, topRayOrigin.z);

        // ?? Height gate on confirmed Y ????????????????????????????????????????
        float relativeHeight = ledgeTopY - transform.position.y;
        if (relativeHeight < _minLedgeHeight || relativeHeight > _maxLedgeHeight) return null;

        // ?? Step 5: Build output ??????????????????????????????????????????????
        //
        // GrabPosition Y: the CharacterController pivot is at the player's FEET.
        // We want the player's HANDS to be at ledgeTopY, so we offset downward
        // by the full character height.
        //
        //   ledgeTopY
        //       ?  ? hands reach here
        //       ?
        //   [player body — height = _cc.height]
        //       ?
        //   grabPosition.y = ledgeTopY - _cc.height   ? feet / pivot point
        //
        // A small _hangYOffset (Inspector) lets you fine-tune the hand position
        // relative to the ledge top without changing the character height.
        //
        Vector3 grabPosition = new Vector3(
            transform.position.x,
            ledgeTopY - _cc.height + _hangYOffset,
            transform.position.z
        );

        // Depth snap: move grab position onto the collider's depth plane.
        float   colCentreDepth = Vector3.Dot(bestCollider.bounds.center, camForward);
        float   grabDepth      = Vector3.Dot(grabPosition,               camForward);
        float   depthDelta     = colCentreDepth - grabDepth;
        Vector3 targetDepthPos = grabPosition + depthDelta * camForward;

        // Wall normal: direction from collider centre toward the player on the
        // lateral axis — so the player faces the wall correctly.
        Vector3 toPlayer   = transform.position - bestCollider.bounds.center;
        Vector3 wallNormal = facingZ
            ? new Vector3(Mathf.Sign(toPlayer.x), 0f, 0f)
            : new Vector3(0f, 0f, Mathf.Sign(toPlayer.z));

        _gizmoHadResult           = true;
        _gizmoGrabPosition        = grabPosition;
        _gizmoTargetDepthPosition = targetDepthPos;

        return new LedgeGrabData(
            grabPosition:        grabPosition,
            targetDepthPosition: targetDepthPos,
            wallNormal:          wallNormal,
            ledgeTopY:           ledgeTopY
        );
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_drawGizmos || !Application.isPlaying || !_gizmoEnabled) return;
        if (_gizmoBoxRot == default) return;

        // Detection tunnel (green = hit, orange = miss)
        Gizmos.color = _gizmoWallHit
            ? new Color(0f, 1f, 0f, 0.20f)
            : new Color(1f, 0.5f, 0f, 0.10f);

        Gizmos.matrix = Matrix4x4.TRS(_gizmoBoxCenter, _gizmoBoxRot, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, _gizmoBoxHalf * 2f);
        Gizmos.matrix = Matrix4x4.identity;

        if (!_gizmoWallHit) return;

        // Best collider bounds (yellow)
        Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
        Gizmos.DrawWireCube(_gizmoWallBounds.center, _gizmoWallBounds.size);

        // Top-edge ray (green = hit, red = fallback)
        Gizmos.color = _gizmoTopRayHit ? Color.green : Color.red;
        Gizmos.DrawRay(_gizmoTopRayOrigin, Vector3.down * _gizmoTopRayDist);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_gizmoTopRayHitPoint, 0.07f);

        if (!_gizmoHadResult) return;

        // GrabPosition (white)
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(_gizmoGrabPosition, 0.12f);

        // TargetDepthPosition (magenta)
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(_gizmoTargetDepthPosition, 0.15f);
        Gizmos.DrawLine(_gizmoGrabPosition, _gizmoTargetDepthPosition);
    }
#endif
}
