using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

public class camera_Manager : MonoBehaviour
{
    [Header("Virtual Cameras (clockwise order)")]
    public List<CinemachineCamera> vCameras = new List<CinemachineCamera>();

    [Header("Priorities")]
    [SerializeField] private int _priorityActive   = 20;
    [SerializeField] private int _priorityInactive = 10;

    [Header("Blend Lock")]
    [SerializeField] private bool  _lockDuringBlend = true;
    [SerializeField] private float _blendDuration   = 0.5f;

    [Header("References")]
    [Tooltip("Used to snapshot the player's screen position before the camera switches.")]
    [SerializeField] private PlayerDepthReprojector _depthReprojector;

    private int  _currentIndex;
    private bool _isBlending;

    private PlayerControls _controls;

    /// <summary>
    /// Creates <see cref="_controls"/> if not yet initialised.
    /// Guards OnEnable/OnDisable against Unity calling them before Awake.
    /// </summary>
    private void EnsureControls()
    {
        if (_controls != null) return;

        _controls = new PlayerControls();
        _controls.Player.RotateLeft.performed  += OnRotateLeft;
        _controls.Player.RotateRight.performed += OnRotateRight;
    }

    private void Awake()
    {
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
        _controls.Player.RotateLeft.performed  -= OnRotateLeft;
        _controls.Player.RotateRight.performed -= OnRotateRight;
        _controls.Player.Disable();
    }

    private void OnDestroy()
    {
        if (_controls == null) return;
        _controls.Player.RotateLeft.performed  -= OnRotateLeft;
        _controls.Player.RotateRight.performed -= OnRotateRight;
        _controls.Dispose();
        _controls = null;
    }

    private void Start()
    {
        if (vCameras == null || vCameras.Count == 0)
        {
            Debug.LogWarning("[camera_Manager] No virtual cameras assigned.");
            return;
        }
        _currentIndex = 0;
        ApplyPriorities();
    }

    private void OnRotateLeft(InputAction.CallbackContext ctx)  => TryRotate(-1);
    private void OnRotateRight(InputAction.CallbackContext ctx) => TryRotate(+1);

    // -------------------------------------------------------------------------
    // Events
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fired immediately when a 90-degree rotation is committed, before the
    /// Cinemachine blend starts.
    ///
    /// Parameters:
    ///   oldCameraRight   — normalised right axis of the camera BEFORE rotation.
    ///   oldCameraUp      — normalised up axis of the camera BEFORE rotation.
    ///   newCameraForward — normalised forward axis of the camera AFTER rotation.
    ///   newCameraRight   — normalised right axis of the camera AFTER rotation.
    ///
    /// Subscribers (e.g. <see cref="PlayerDepthReprojector"/>) use these axes
    /// to reproject the player's depth onto the new view plane.
    /// </summary>
    public event Action<Vector3, Vector3, Vector3, Vector3> OnCameraRotated;

    /// <summary>
    /// Force a specific camera by index. Used by external controllers (e.g. Microbit).
    /// </summary>
    public void ForceSetCamera(int targetIndex)
    {
        if (vCameras == null || vCameras.Count == 0) return;
        if (_lockDuringBlend && _isBlending) return;

        targetIndex = Mathf.Clamp(targetIndex, 0, vCameras.Count - 1);
        if (_currentIndex == targetIndex) return;

        CinemachineCamera oldVCam      = vCameras[_currentIndex];
        Vector3           oldCameraRight = oldVCam != null ? oldVCam.transform.right   : Vector3.right;
        Vector3           oldCameraUp    = oldVCam != null ? oldVCam.transform.up      : Vector3.up;

        // ?? STEP 1: snapshot player screen pos BEFORE the active camera changes ??
        _depthReprojector?.SaveScreenPosition();

        _currentIndex = targetIndex;
        ApplyPriorities();

        CinemachineCamera newVCam          = vCameras[_currentIndex];
        Vector3           newCameraForward = newVCam != null ? newVCam.transform.forward : Vector3.forward;
        Vector3           newCameraRight   = newVCam != null ? newVCam.transform.right   : Vector3.right;

        OnCameraRotated?.Invoke(oldCameraRight, oldCameraUp, newCameraForward, newCameraRight);

        if (_lockDuringBlend)
            StartCoroutine(BlendCooldown());
    }

    private void TryRotate(int direction)
    {
        if (vCameras == null || vCameras.Count == 0) return;
        if (_lockDuringBlend && _isBlending) return;

        int targetIndex = (_currentIndex + direction + vCameras.Count) % vCameras.Count;
        ForceSetCamera(targetIndex);
    }

    private void ApplyPriorities()
    {
        for (int i = 0; i < vCameras.Count; i++)
        {
            if (vCameras[i] == null) continue;
            vCameras[i].Priority = (i == _currentIndex) ? _priorityActive : _priorityInactive;
        }
    }

    private IEnumerator BlendCooldown()
    {
        _isBlending = true;
        yield return new WaitForSeconds(_blendDuration);
        _isBlending = false;
    }

    public int CurrentIndex => _currentIndex;

    public CinemachineCamera ActiveCamera =>
        (vCameras != null && _currentIndex < vCameras.Count) ? vCameras[_currentIndex] : null;
}