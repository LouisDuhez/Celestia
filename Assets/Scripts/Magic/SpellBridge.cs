using UnityEngine;
using System.Collections;
using UnityEngine.Playables;
using UnityEngine.InputSystem;

public class SpellBridge : MonoBehaviour
{
    [Header("Alembic Prefab")]
    [SerializeField] private GameObject alembicPrefab;

    [Header("Spawn Point")]
    [SerializeField] private Transform spawnPoint;

    [Header("Plan invisible pour marcher dessus")]
    [SerializeField] private GameObject invisiblePlanePrefab;
    [SerializeField] private Vector3 planeRotationEuler = new Vector3(30f, 0f, 0f); 

    [Header("Animation Personnage")]
    [SerializeField] private Animator animator;
    [SerializeField] private string castTriggerName = "CastSpell";

    [Header("Timing & durée")]
    [SerializeField] private float spawnDelay = 0.1f;
    [SerializeField] private float lifeTime = 5f; 

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
        _controls.Player.RKey.performed += ctx => OnRKey();
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

    private void OnRKey()
    {
        if (!_isCasting)
        {
            StartCoroutine(SpawnAlembic());
        }
    }

    private IEnumerator SpawnAlembic()
    {
        _isCasting = true;

        if (animator != null && !string.IsNullOrEmpty(castTriggerName))
        {
            animator.SetTrigger(castTriggerName);
        }

        yield return new WaitForSeconds(spawnDelay);

        if (alembicPrefab == null || spawnPoint == null)
        {
            Debug.LogWarning("[SpellBridge] AlembicPrefab ou SpawnPoint non assigné !");
            _isCasting = false;
            yield break;
        }

        GameObject alembic = Instantiate(alembicPrefab, spawnPoint.position, spawnPoint.rotation);

        Rigidbody rb = alembic.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = alembic.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;

        PlayableDirector director = alembic.GetComponent<PlayableDirector>();
        if (director != null && director.playableAsset != null)
        {
            director.Play();
        }
        else
        {
            Debug.LogWarning("[SpellBridge] Pas de PlayableDirector trouvé sur le prefab Alembic ou asset manquant !");
        }

        if (invisiblePlanePrefab != null)
        {
            Vector3 planePos = spawnPoint.position;
            planePos.y -= 0.05f; 

            Quaternion planeRotation = alembic.transform.rotation;
            planeRotation *= Quaternion.Euler(-20f, 0f, 0f);

            GameObject plane = Instantiate(invisiblePlanePrefab, planePos, planeRotation);
            Destroy(plane, lifeTime);
        }

        Destroy(alembic, lifeTime);
        
        // Safety: wait for lifetime then reset casting
        yield return new WaitForSeconds(lifeTime);
        _isCasting = false;
    }
}
