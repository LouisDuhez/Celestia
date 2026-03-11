using UnityEngine;

/// <summary>
/// Preserves the player's screen-space position (X lateral, Y world-up)
/// when the camera rotates 90°. Depth is NOT changed — the player stays
/// at their current world position, only re-expressed on the new axis.
///
/// The <see cref="DepthSnapper"/> component handles depth correction
/// automatically every frame while the player is airborne.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerMovementController))]
public class PlayerDepthReprojector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private camera_Manager _cameraManager;

    [Header("Debug")]
    [SerializeField] private bool _drawGizmos = true;

    // -------------------------------------------------------------------------
    // Private
    // -------------------------------------------------------------------------

    private CharacterController      _cc;
    private PlayerMovementController _movement;

    private Vector3 _gizmoBefore;
    private Vector3 _gizmoAfter;
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

        // The player does not move — their world position is unchanged.
        // The CharacterController is briefly toggled so Unity does not
        // fight the position assignment with internal depenetration.
        _gizmoBefore = transform.position;
        _gizmoAfter  = transform.position;
        _gizmoActive = true;

        _cc.enabled        = false;
        transform.position = transform.position;
        _cc.enabled        = true;
    }

    // -------------------------------------------------------------------------
    // Debug gizmos
    // -------------------------------------------------------------------------

    private void OnDrawGizmos()
    {
        if (!_drawGizmos || !Application.isPlaying || !_gizmoActive) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_gizmoAfter, 0.25f);
    }
}
