using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimatedWall : MonoBehaviour
{
    private Animator _animator;
    private Collider _wallCollider;
    private bool _isOpened = false;

    [Header("Réglages Animation")]
    [SerializeField] private string triggerName = "Activate";
    [SerializeField] private float delayBeforeCollisionDisable = 0.5f;

    [Header("Spawn (Optionnel - Crée un nouvel objet)")]
    [Tooltip("Le prefab à faire apparaître (ex: des particules)")]
    [SerializeField] private GameObject objectToSpawn;

    [Tooltip("L'endroit précis où l'objet doit apparaître. Si vide, il apparaîtra au centre du mur.")]
    [SerializeField] private Transform spawnPoint;

    [Header("Activer objet existant (Optionnel - Garde la position de base)")]
    [Tooltip("L'objet déjà présent dans la scène à révéler/activer (ex: un pont caché).")]
    [SerializeField] private GameObject objectToEnable;

    [Tooltip("Cochez pour activer UNIQUEMENT le Collider de cet objet, sans toucher à son SetActive().")]
    [SerializeField] private bool enableColliderOnly = false;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _wallCollider = GetComponent<Collider>();
    }

    public void BreakWall()
    {
        if (_isOpened) return;

        _animator.SetTrigger(triggerName);

        if (_wallCollider != null)
        {
            Invoke(nameof(DisableCollider), delayBeforeCollisionDisable);
        }

        SpawnObject();
        EnableExistingObject(); // Appel de la nouvelle fonction

        _isOpened = true;
    }

    private void SpawnObject()
    {
        if (objectToSpawn != null)
        {
            Vector3 finalPosition = transform.position;
            Quaternion finalRotation = transform.rotation;

            if (spawnPoint != null)
            {
                finalPosition = spawnPoint.position;
                finalRotation = spawnPoint.rotation;
            }

            // Création d'une nouvelle instance
            Instantiate(objectToSpawn, finalPosition, finalRotation);
        }
    }

    private void EnableExistingObject()
    {
        if (objectToEnable != null)
        {
            if (enableColliderOnly)
            {
                // Active uniquement le Collider
                Collider col = objectToEnable.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = true;
                }
                else
                {
                    Debug.LogWarning("Attention : Vous avez coché 'Enable Collider Only', mais l'objet n'a pas de Collider.", objectToEnable);
                }
            }
            else
            {
                // Active l'objet en entier
                objectToEnable.SetActive(true);
            }
        }
    }

    private void DisableCollider()
    {
        if (_wallCollider != null)
        {
            _wallCollider.enabled = false;
        }
    }
}