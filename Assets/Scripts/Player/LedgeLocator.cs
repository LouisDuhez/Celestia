using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Detects climbable ledges in front of the player while airborne,
/// snaps the player to a hanging position, then climbs on Space press.
///
/// Requires PlayerMovementController on the same GameObject.
/// Ledge GameObjects must have the Ledge component and belong to the ledge layer.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerMovementController))]
public class LedgeLocator : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Detection")]
    [Tooltip("How far in front of the player the raycast reaches")]
    [SerializeField] private float _reachDistance = 1.5f;

    [Tooltip("Layer(s) that contain climbable ledge colliders")]
    [SerializeField] private LayerMask _ledgeLayer;

    [Header("Climb")]
    [Tooltip("How far forward the player moves when climbing over the ledge")]
    [SerializeField] private float _forwardClimbOffset = 0.5f;

    [Tooltip("Duration of the climb movement (seconds)")]
    [SerializeField] private float _climbDuration = 0.6f;

    // -------------------------------------------------------------------------
    // Animator hashes
    // -------------------------------------------------------------------------

    private static readonly int _hashLedgeHanging  = Animator.StringToHash("LedgeHanging");
    private static readonly int _hashLedgeClimbing = Animator.StringToHash("LedgeClimbing");
    private static readonly int _hashJump          = Animator.StringToHash("Jump");
    private static readonly int _hashFreeFall      = Animator.StringToHash("FreeFall");
    private static readonly int _hashGrounded      = Animator.StringToHash("Grounded");

    // -------------------------------------------------------------------------
    // Private references
    // -------------------------------------------------------------------------

    private CharacterController      _characterController;
    private PlayerMovementController _movement;
    private Animator                 _animator;
    private PlayerControls           _controls;

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    private bool       _isGrabbing;
    private bool       _isClimbing;
    private GameObject _currentLedge;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates <see cref="_controls"/> if not yet initialised.
    /// Guards OnEnable/OnDisable against Unity calling them before Awake.
    /// </summary>
    private void EnsureControls()
    {
        if (_controls != null) return;

        _controls = new PlayerControls();
        _controls.Player.Jump.performed += OnJumpPerformed;
    }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _movement            = GetComponent<PlayerMovementController>();
        _animator            = GetComponentInChildren<Animator>();

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

        if (!_isGrabbing)
            DetectLedge();
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

    private void DetectLedge()
    {
        // Only grab ledges while airborne
        if (_characterController.isGrounded) return;

        Vector3 rayOrigin = transform.position + Vector3.up * (_characterController.height * 0.9f);

        Debug.DrawRay(rayOrigin, transform.forward * _reachDistance, Color.red);

        if (!Physics.Raycast(rayOrigin, transform.forward, out RaycastHit hit, _reachDistance, _ledgeLayer))
            return;

        Ledge ledgeScript = hit.collider.GetComponent<Ledge>();
        if (ledgeScript == null) return;

        // Ignore underside of platforms
        if (hit.normal.y < -0.1f) return;

        // Only grab when not rising fast (avoids catching on the way up)
        if (_characterController.velocity.y < 1.0f)
            GrabLedge(hit, ledgeScript);
    }

    // -------------------------------------------------------------------------
    // Grab
    // -------------------------------------------------------------------------

    private void GrabLedge(RaycastHit hit, Ledge ledgeScript)
    {
        _isGrabbing   = true;
        _currentLedge = hit.collider.gameObject;

        // Tell the movement controller to freeze gravity + Animator updates
        _movement.IsHanging = true;

        // Face the wall
        transform.rotation = Quaternion.LookRotation(-hit.normal);

        // Snap player position: against the wall at ledge height
        Vector3 snapPos = hit.point + hit.normal * ledgeScript.forwardOffset;
        snapPos.y = hit.collider.bounds.max.y - ledgeScript.verticalOffset;

        _characterController.enabled = false;
        transform.position           = snapPos;
        _characterController.enabled = true;

        // Force Animator into hanging state, clearing all in-air states
        if (_animator)
        {
            _animator.SetBool(_hashJump,          false);
            _animator.SetBool(_hashFreeFall,      false);
            _animator.SetBool(_hashGrounded,      false);
            _animator.SetBool(_hashLedgeHanging,  true);
        }
    }

    // -------------------------------------------------------------------------
    // Climb
    // -------------------------------------------------------------------------

    private IEnumerator ClimbRoutine()
    {
        _isClimbing = true;

        if (_animator)
        {
            _animator.SetBool(_hashLedgeHanging,  false);
            _animator.SetBool(_hashLedgeClimbing, true);
        }

        Vector3 startPos = transform.position;
        float   topY     = _currentLedge.GetComponent<Collider>().bounds.max.y;

        Vector3 endPos = startPos + transform.forward * _forwardClimbOffset;
        endPos.y = topY;

        _characterController.enabled = false;

        float elapsed = 0f;
        while (elapsed < _climbDuration)
        {
            elapsed           += Time.deltaTime;
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
        _isGrabbing   = false;
        _isClimbing   = false;
        _currentLedge = null;

        _characterController.enabled = true;

        // Restore movement controller to a clean grounded state
        // (also clears IsHanging)
        _movement.ForceGroundedState();

        if (_animator)
        {
            _animator.SetBool(_hashLedgeHanging,  false);
            _animator.SetBool(_hashLedgeClimbing, false);
        }
    }

    // -------------------------------------------------------------------------
    // Debug gizmos
    // -------------------------------------------------------------------------

    private void OnDrawGizmos()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc == null) return;

        Gizmos.color = Color.red;
        Vector3 origin = transform.position + Vector3.up * (cc.height * 0.9f);
        Gizmos.DrawLine(origin, origin + transform.forward * _reachDistance);
    }
}