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

    private int  _currentIndex;
    private bool _isBlending;

    private PlayerControls _controls;

    private void Awake()
    {
        _controls = new PlayerControls();
    }

    private void OnEnable()
    {
        _controls.Player.Enable();
        _controls.Player.RotateLeft.performed  += OnRotateLeft;
        _controls.Player.RotateRight.performed += OnRotateRight;
    }

    private void OnDisable()
    {
        _controls.Player.RotateLeft.performed  -= OnRotateLeft;
        _controls.Player.RotateRight.performed -= OnRotateRight;
        _controls.Player.Disable();
    }

    private void OnDestroy()
    {
        _controls.Dispose();
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

    private void TryRotate(int direction)
    {
        if (vCameras == null || vCameras.Count == 0) return;
        if (_lockDuringBlend && _isBlending) return;

        _currentIndex = (_currentIndex + direction + vCameras.Count) % vCameras.Count;
        ApplyPriorities();

        if (_lockDuringBlend)
            StartCoroutine(BlendCooldown());
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