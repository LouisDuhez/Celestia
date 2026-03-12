using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles the player's 2D-like horizontal movement and jumping.
/// Animator contract (must match StarterAssetsThirdPerson.controller):
///   - Speed        (float) : 0=idle, >0=walk/run  ? drives Idle/Walk/Run blend tree
///   - MotionSpeed  (float) : always 1f             ? blend tree multiplier
///   - Grounded     (bool)  : true when on ground   ? triggers InAir?JumpLand transition
///   - Jump         (bool)  : true while airborne   ? triggers Blend?JumpStart transition
///   - FreeFall     (bool)  : true when falling (no jump)
///   - LedgeHanging (bool)  : managed by LedgeLocator (not here)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovementController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector — Movement
    // -------------------------------------------------------------------------

    [Header("Movement")]
    [SerializeField] private float _moveSpeed       = 2.0f;
    [SerializeField] private float _sprintSpeed     = 5.335f;
    [SerializeField] private float _speedChangeRate = 10.0f;

    // -------------------------------------------------------------------------
    // Inspector — Jump & Gravity
    // -------------------------------------------------------------------------

    [Header("Jump & Gravity")]
    [SerializeField] private float _jumpHeight  = 1.2f;
    [SerializeField] private float _gravity     = -15.0f;

    [Tooltip("Minimum time between landing and being able to jump again")]
    [SerializeField] private float _jumpTimeout = 0.50f;

    [Tooltip("Delay before FreeFall triggers (avoids false positive when walking down a step)")]
    [SerializeField] private float _fallTimeout = 0.15f;

    // -------------------------------------------------------------------------
    // Inspector — Grounded
    // -------------------------------------------------------------------------

    [Header("Grounded Detection")]
    [SerializeField] private float     _groundedOffset = -0.14f;
    [SerializeField] private float     _groundedRadius = 0.28f;
    [SerializeField] private LayerMask _groundLayers;

    // -------------------------------------------------------------------------
    // Inspector — Audio (Animation Events)
    // -------------------------------------------------------------------------

    [Header("Audio")]
    [SerializeField] private AudioClip   _landingAudioClip;
    [SerializeField] private AudioClip[] _footstepAudioClips;
    [Range(0f, 1f)]
    [SerializeField] private float _footstepAudioVolume = 0.5f;

    // -------------------------------------------------------------------------
    // Inspector — Animation
    // -------------------------------------------------------------------------

    [Header("Animation")]
    [SerializeField] private Animator _animator;

    // -------------------------------------------------------------------------
    // Inspector — References
    // -------------------------------------------------------------------------

    [Header("References")]
    [Tooltip("Camera manager used to read the active virtual camera's right axis. " +
             "Prevents using Camera.main which is interpolated during Cinemachine blends.")]
    [SerializeField] private camera_Manager _cameraManager;

    // -------------------------------------------------------------------------
    // Animator parameter hashes (cached — no per-frame string lookup)
    // -------------------------------------------------------------------------

    private static readonly int _hashSpeed       = Animator.StringToHash("Speed");
    private static readonly int _hashMotionSpeed = Animator.StringToHash("MotionSpeed");
    private static readonly int _hashGrounded    = Animator.StringToHash("Grounded");
    private static readonly int _hashJump        = Animator.StringToHash("Jump");
    private static readonly int _hashFreeFall    = Animator.StringToHash("FreeFall");

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private CharacterController _characterController;
    private PlayerControls      _controls;

    private float _moveInput;
    private bool  _sprintInput;
    private bool  _jumpRequested;

    // Last non-zero horizontal input direction (-1 or +1).
    // Persists when input is released so LedgeDetector can probe in the
    // direction the player was moving even during passive free-fall.
    private float _lastMoveDirection = 1f;

    private float _currentSpeed;
    private float _animationBlend;
    private float _verticalVelocity;

    private bool  _isGrounded;
    private float _jumpTimeoutDelta;
    private float _fallTimeoutDelta;

    private bool _hasAnimator;

    private const float _terminalVelocity = 53.0f;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates <see cref="_controls"/> if it has not been created yet.
    /// Called defensively in OnEnable/OnDisable because Unity can invoke
    /// OnEnable before Awake when an object is activated at scene load.
    /// </summary>
    private void EnsureControls()
    {
        if (_controls != null) return;

        _controls = new PlayerControls();
        _controls.Player.Move.performed   += OnMovePerformed;
        _controls.Player.Move.canceled    += OnMoveCanceled;
        _controls.Player.Jump.performed   += OnJumpPerformed;
        _controls.Player.Sprint.performed += ctx => _sprintInput = true;
        _controls.Player.Sprint.canceled  += ctx => _sprintInput = false;
    }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        _hasAnimator = _animator != null;

        EnsureControls();
    }

    private void Start()
    {
        _jumpTimeoutDelta = _jumpTimeout;
        _fallTimeoutDelta = _fallTimeout;

        // Initialise Animator to grounded state so we don't start in JumpStart
        if (_hasAnimator)
        {
            _animator.SetBool(_hashGrounded, true);
            _animator.SetBool(_hashJump,     false);
            _animator.SetBool(_hashFreeFall, false);
        }
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

        _controls.Player.Move.performed   -= OnMovePerformed;
        _controls.Player.Move.canceled    -= OnMoveCanceled;
        _controls.Player.Jump.performed   -= OnJumpPerformed;
        _controls.Dispose();
        _controls = null;
    }

    private void Update()
    {
        // While hanging/climbing, freeze all movement and animator logic.
        // LedgeLocator owns the Animator during that time.
        if (IsHanging) return;

        GroundedCheck();
        JumpAndGravity();
        Move();
    }

    // -------------------------------------------------------------------------
    // Input callbacks
    // -------------------------------------------------------------------------

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        _moveInput = ctx.ReadValue<Vector2>().x;
        if (!Mathf.Approximately(_moveInput, 0f))
            _lastMoveDirection = Mathf.Sign(_moveInput);
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
        => _moveInput = 0f;

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        // Block jump input while hanging so Space triggers climb, not a new jump
        if (IsHanging) return;

        if (_isGrounded && _jumpTimeoutDelta <= 0f)
            _jumpRequested = true;
    }

    // -------------------------------------------------------------------------
    // Grounded check — Physics sphere (more reliable than CharacterController.isGrounded)
    // -------------------------------------------------------------------------

    private void GroundedCheck()
    {
        Vector3 spherePos = new Vector3(
            transform.position.x,
            transform.position.y - _groundedOffset,
            transform.position.z
        );

        _isGrounded = Physics.CheckSphere(
            spherePos,
            _groundedRadius,
            _groundLayers,
            QueryTriggerInteraction.Ignore
        );

        // Grounded drives the InAir?JumpLand transition in the Animator.
        // It must be set BEFORE Jump/FreeFall are cleared so the Animator
        // sees "Grounded=true AND Jump=true" simultaneously, which is the
        // exact condition that fires the InAir?JumpLand transition.
        if (_hasAnimator)
            _animator.SetBool(_hashGrounded, _isGrounded);
    }

    // -------------------------------------------------------------------------
    // Jump & Gravity
    // -------------------------------------------------------------------------

    private void JumpAndGravity()
    {
        if (_isGrounded)
        {
            // Reset fall timer
            _fallTimeoutDelta = _fallTimeout;

            // Clear airborne flags AFTER Grounded is already true (set in GroundedCheck).
            // The Animator transition InAir?JumpLand fires on Grounded=true,
            // then JumpLand?Blend fires on its own exit time — we must NOT
            // clear Jump=false before the transition has been evaluated.
            // Setting them false here (same frame as Grounded=true) is correct:
            // Unity evaluates all SetBool calls at end of frame.
            if (_hasAnimator)
            {
                _animator.SetBool(_hashJump,     false);
                _animator.SetBool(_hashFreeFall, false);
            }

            // Clamp downward velocity
            if (_verticalVelocity < 0f)
                _verticalVelocity = -2f;

            // Jump
            if (_jumpRequested)
            {
                _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
                _jumpRequested    = false;

                if (_hasAnimator)
                    _animator.SetBool(_hashJump, true);
            }

            // Jump re-trigger cooldown
            if (_jumpTimeoutDelta >= 0f)
                _jumpTimeoutDelta -= Time.deltaTime;
        }
        else
        {
            // Reset jump cooldown while airborne
            _jumpTimeoutDelta = _jumpTimeout;
            _jumpRequested    = false;

            // FreeFall: only after the fall timeout (avoids triggering on small steps)
            if (_fallTimeoutDelta >= 0f)
            {
                _fallTimeoutDelta -= Time.deltaTime;
            }
            else
            {
                // FreeFall = free-fall with no jump (walked off a ledge)
                if (_hasAnimator)
                    _animator.SetBool(_hashFreeFall, true);
            }
        }

        // Apply gravity (capped at terminal velocity)
        if (_verticalVelocity < _terminalVelocity)
            _verticalVelocity += _gravity * Time.deltaTime;
    }

    // -------------------------------------------------------------------------
    // Movement — always along camera's right axis (FEZ-like)
    // -------------------------------------------------------------------------

    private void Move()
    {
        float targetSpeed = _sprintInput ? _sprintSpeed : _moveSpeed;
        if (Mathf.Abs(_moveInput) < 0.01f) targetSpeed = 0f;

        // Smooth speed transitions (acceleration / deceleration)
        float currentHorizontalSpeed = new Vector3(
            _characterController.velocity.x, 0f, _characterController.velocity.z
        ).magnitude;

        const float speedOffset = 0.1f;
        if (currentHorizontalSpeed < targetSpeed - speedOffset ||
            currentHorizontalSpeed > targetSpeed + speedOffset)
        {
            _currentSpeed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed, Time.deltaTime * _speedChangeRate);
            _currentSpeed = Mathf.Round(_currentSpeed * 1000f) / 1000f;
        }
        else
        {
            _currentSpeed = targetSpeed;
        }

        // Smooth blend value for the Animator blend tree
        _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * _speedChangeRate);
        if (_animationBlend < 0.01f) _animationBlend = 0f;

        // Read right axis from the active virtual camera so the movement
        // direction is always the post-rotation target axis, never the
        // interpolated blend value from Camera.main.
        Vector3 cameraRight;
        if (_cameraManager != null && _cameraManager.ActiveCamera != null)
            cameraRight = _cameraManager.ActiveCamera.transform.right;
        else
            cameraRight = Camera.main != null ? Camera.main.transform.right : Vector3.right;

        cameraRight.y = 0f;
        cameraRight.Normalize();

        _characterController.Move(
            (cameraRight * (_moveInput * _currentSpeed) + Vector3.up * _verticalVelocity) * Time.deltaTime
        );

        // Rotate player to face movement direction
        if (Mathf.Abs(_moveInput) > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(cameraRight * _moveInput),
                20f * Time.deltaTime
            );
        }

        // Drive Animator blend tree
        if (_hasAnimator)
        {
            _animator.SetFloat(_hashSpeed,       _animationBlend);
            _animator.SetFloat(_hashMotionSpeed, 1f);
        }
    }

    // -------------------------------------------------------------------------
    // Animation Events — receivers for clips on PlayerArmature
    // -------------------------------------------------------------------------

    private void OnFootstep(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight <= 0.5f) return;
        if (_footstepAudioClips == null || _footstepAudioClips.Length == 0) return;

        AudioSource.PlayClipAtPoint(
            _footstepAudioClips[Random.Range(0, _footstepAudioClips.Length)],
            transform.TransformPoint(_characterController.center),
            _footstepAudioVolume
        );
    }

    private void OnLand(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight <= 0.5f) return;
        if (_landingAudioClip == null) return;

        AudioSource.PlayClipAtPoint(
            _landingAudioClip,
            transform.TransformPoint(_characterController.center),
            _footstepAudioVolume
        );
    }

    // -------------------------------------------------------------------------
    // Public API — called by LedgeLocator
    // -------------------------------------------------------------------------

    /// <summary>
    /// When true, PlayerMovementController freezes all Animator state updates
    /// and gravity so ledge hanging/climbing animations are not interrupted.
    /// Set by LedgeLocator on grab and cleared on FinishClimb.
    /// </summary>
    public bool IsHanging { get; set; }

    /// <summary>
    /// Current horizontal input value (-1 to +1).
    /// Read by LedgeLocator to pass the movement sign to LedgeDetector.
    /// </summary>
    public float MoveInput => _moveInput;

    /// <summary>
    /// The last non-zero horizontal input direction (-1 or +1).
    /// Persists after input is released so LedgeDetector can still probe
    /// in the correct direction during passive free-fall.
    /// </summary>
    public float LastMoveDirection => _lastMoveDirection;

    /// <summary>
    /// True when the physics CheckSphere confirms the player is on the ground.
    /// More reliable than CharacterController.isGrounded which stays true for
    /// 1-2 extra frames after leaving a surface.
    /// </summary>
    public bool IsGrounded => _isGrounded;

    /// <summary>
    /// Resets the vertical velocity and all in-air Animator states so the
    /// controller behaves as if the player just landed cleanly.
    /// </summary>
    public void ForceGroundedState()
    {
        _verticalVelocity = -2f;
        _fallTimeoutDelta = _fallTimeout;
        _jumpTimeoutDelta = _jumpTimeout;
        _jumpRequested    = false;
        IsHanging         = false;

        if (_hasAnimator)
        {
            _animator.SetBool(_hashJump,     false);
            _animator.SetBool(_hashFreeFall, false);
            _animator.SetBool(_hashGrounded, true);
        }
    }

    // -------------------------------------------------------------------------
    // Debug gizmos
    // -------------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isGrounded
            ? new Color(0f, 1f, 0f, 0.35f)
            : new Color(1f, 0f, 0f, 0.35f);

        Gizmos.DrawSphere(
            new Vector3(transform.position.x, transform.position.y - _groundedOffset, transform.position.z),
            _groundedRadius
        );
    }
}
