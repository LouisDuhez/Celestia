using UnityEngine;
using UnityEngine.VFX;
using System.Collections;
using UnityEngine.InputSystem;

public class projection_ICE : MonoBehaviour
{
    [Header("Projectile Prefab")]
    [SerializeField] private GameObject projectilePrefab;

    [Header("Spawn Point")]
    [SerializeField] private Transform spawnPoint;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string castTriggerName = "CastSpell";

    [Header("Vitesse et délai")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float vfxDelay = 0.3f; // délai avant apparition du projectile

    private PlayerControls _controls;
    private bool _isCasting;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        EnsureControls();
    }

    private void EnsureControls()
    {
        if (_controls != null) return;
        _controls = new PlayerControls();
        _controls.Player.ICE.performed += ctx => OnICE();
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
        _controls.Dispose();
        _controls = null;
    }

    private void OnICE()
    {
        if (!_isCasting)
        {
            StartCoroutine(ShootProjectileDelayed());
        }
    }

    private IEnumerator ShootProjectileDelayed()
    {
        _isCasting = true;

        if (animator != null && !string.IsNullOrEmpty(castTriggerName))
            animator.SetTrigger(castTriggerName);

        yield return new WaitForSeconds(vfxDelay);

        if (projectilePrefab == null || spawnPoint == null)
        {
            _isCasting = false;
            yield break;
        }

        GameObject projectile = Instantiate(
            projectilePrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Note: Si tu es sur une version plus ancienne de Unity, remplace 'linearVelocity' par 'velocity'
            rb.linearVelocity = spawnPoint.forward * speed;
        }

        VisualEffect vfx = projectile.GetComponent<VisualEffect>();
        if (vfx != null)
            vfx.Play();

        Destroy(projectile, 5f);

        _isCasting = false;
    }
}
