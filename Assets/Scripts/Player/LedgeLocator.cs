using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Detects climbable ledges in front of the player while airborne using
/// <see cref="LedgeDetector"/> (screen-space, works on all 4 cameras),
/// snaps the player to a hanging position, then climbs on Space press.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerMovementController))]
[RequireComponent(typeof(LedgeDetector))]
public class LedgeLocator : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Climb")]
    [Tooltip("How far over the ledge the player moves when climbing")]
    [SerializeField] private float _forwardClimbOffset = 0.5f;

    [Tooltip("Duration of the climb movement in seconds")]
    [SerializeField] private float _climbDuration = 0.6f;

    // -------------------------------------------------------------------------
    // Animator hashes
    // -------------------------------------------------------------------------

    private static readonly int _hashLedgeHanging = Animator.StringToHash("LedgeHanging");
    private static readonly int _hashLedgeClimbing = Animator.StringToHash("LedgeClimbing");
    private static readonly int _hashJump = Animator.StringToHash("Jump");
    private static readonly int _hashFreeFall = Animator.StringToHash("FreeFall");
    private static readonly int _hashGrounded = Animator.StringToHash("Grounded");

    // -------------------------------------------------------------------------
    // Private references
    // -------------------------------------------------------------------------

    private CharacterController _cc;
    private PlayerMovementController _movement;
    private LedgeDetector _detector;
    private Animator _animator;
    private PlayerControls _controls;

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    private bool _isGrabbing;
    private bool _isClimbing;
    private float _currentLedgeTopY;
    private Vector3 _currentWallNormal;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void EnsureControls()
    {
        if (_controls != null) return;
        _controls = new PlayerControls();
        _controls.Player.Jump.performed += OnJumpPerformed;
    }

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _movement = GetComponent<PlayerMovementController>();
        _detector = GetComponent<LedgeDetector>();
        _animator = GetComponentInChildren<Animator>();
        EnsureControls();
    }

    private void OnEnable()
    {
        EnsureControls();
        _controls.Player.Enable();
    }

    private void OnDisable()
    {
        if (_controls == null) return;
        _controls.Player.Disable();
    }

    private void OnDestroy()
    {
        if (_controls == null) return;
        _controls.Player.Jump.performed -= OnJumpPerformed;
        _controls.Dispose();
        _controls = null;
    }

    private void Update()
    {
        if (_isClimbing) return;
        if (_isGrabbing) return;
        TryGrabLedge();
    }

    // -------------------------------------------------------------------------
    // Input — Space triggers climb while hanging
    // -------------------------------------------------------------------------

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (_isGrabbing && !_isClimbing)
            StartCoroutine(ClimbRoutine());
    }

    // -------------------------------------------------------------------------
    // Detection
    // -------------------------------------------------------------------------

    private void TryGrabLedge()
    {
        if (_movement.IsGrounded) return;

        // Use active input if pressed, otherwise fall back on last known direction
        // so the player can grab a ledge during passive free-fall.
        float inputSign = !Mathf.Approximately(_movement.MoveInput, 0f)
            ? _movement.MoveInput
            : _movement.LastMoveDirection;

        // Pass vertical velocity so LedgeDetector can extend the height gate
        // downward by the fall distance of this frame — prevents skipping past
        // a ledge entirely during a fast fall.
        LedgeGrabData? data = _detector.TryDetect(inputSign, _cc.velocity.y);
        if (!data.HasValue) return;

        Debug.Log($"[LedgeLocator] Ledge detected — grabPos={data.Value.GrabPosition}  " +
                  $"targetDepth={data.Value.TargetDepthPosition}  ledgeTopY={data.Value.LedgeTopY:F2}");

        GrabLedge(data.Value);
    }

    // -------------------------------------------------------------------------
    // Grab
    // -------------------------------------------------------------------------

    private void GrabLedge(LedgeGrabData data)
    {
        _isGrabbing = true;
        _currentLedgeTopY = data.LedgeTopY;
        _currentWallNormal = data.WallNormal;

        _movement.IsHanging = true;

        // Orient the player facing away from the wall
        if (data.WallNormal != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(-data.WallNormal);

        // ?? Depth-correct snap ????????????????????????????????????????????????
        // TargetDepthPosition keeps the player's screen XY but replaces the
        // depth component with the wall's bounds centre so the grab works on
        // all 4 camera orientations regardless of the wall's world depth.
        _cc.enabled = false;
        transform.position = data.TargetDepthPosition;
        _cc.enabled = true;

        Debug.Log($"[LedgeLocator] Grab SUCCESS — snapped to {data.TargetDepthPosition}");

        if (_animator != null)
        {
            _animator.SetBool(_hashJump, false);
            _animator.SetBool(_hashFreeFall, false);
            _animator.SetBool(_hashGrounded, false);
            _animator.SetBool(_hashLedgeHanging, true);
        }
    }

    // -------------------------------------------------------------------------
    // Climb
    // -------------------------------------------------------------------------

    private IEnumerator ClimbRoutine()
    {
        _isClimbing = true;

        if (_animator != null)
        {
            _animator.SetBool(_hashLedgeHanging, false);
            _animator.SetBool(_hashLedgeClimbing, true);
        }

        Vector3 startPos = transform.position;
        // -WallNormal gives the "over the ledge" direction regardless of camera
        Vector3 overLedgeDir = new Vector3(-_currentWallNormal.x, 0f, -_currentWallNormal.z).normalized;
        Vector3 endPos = new Vector3(
            startPos.x + overLedgeDir.x * _forwardClimbOffset,
            _currentLedgeTopY,
            startPos.z + overLedgeDir.z * _forwardClimbOffset
        );

        _cc.enabled = false;

        float elapsed = 0f;
        while (elapsed < _climbDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, endPos, elapsed / _climbDuration);
            yield return null;
        }

        transform.position = endPos;
        FinishClimb();
    }

    // -------------------------------------------------------------------------
    // Finish
    // -------------------------------------------------------------------------

    private void FinishClimb()
    {
        _isGrabbing = false;
        _isClimbing = false;
        _cc.enabled = true;
        _movement.ForceGroundedState();

        if (_animator != null)
        {
            _animator.SetBool(_hashLedgeHanging, false);
            _animator.SetBool(_hashLedgeClimbing, false);
        }
    }

    // -------------------------------------------------------------------------
    // Debug gizmos
    // -------------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !_isGrabbing) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, -_currentWallNormal * 0.5f);
    }
#endif
}