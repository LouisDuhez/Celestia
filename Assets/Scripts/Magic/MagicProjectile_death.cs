using UnityEngine;
using UnityEngine.VFX;

public class MagicProjectile_death : MonoBehaviour
{
    [Header("Collision Layers")]
    [SerializeField] private LayerMask destructibleWallLayer;
    [SerializeField] private LayerMask movableObjectLayer;
    [SerializeField] private LayerMask animatedWallLayer;

    [Header("Effets & Feedback")]
    [SerializeField] private float shakeDuration = 0.2f;
    [SerializeField] private float shakeMagnitude = 0.3f;
    
    [Header("VFX Settings")]
    [SerializeField] private string forceDeathPropertyName = "ForceDeath"; // Le nom exact de ton booléen
    [SerializeField] private float timeBeforeDestroy = 2.0f; 

    private bool isDead = false;
    private VisualEffect vfx;

    void Awake()
    {
        vfx = GetComponentInChildren<VisualEffect>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDead) return;

        bool hasHitSomething = false;

        // --- Détection des layers ---
        if ((destructibleWallLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            Destroy(other.gameObject);
            hasHitSomething = true;
        }
        else if ((movableObjectLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            // Fallback si le composant exact n'est pas connu
            MonoBehaviour movable = other.GetComponent<MonoBehaviour>(); // Placeholder
            if (other.CompareTag("MovableObject"))
            {
                // SendMessage est plus sûr si on n'a pas accès à la classe MovableObject
                other.SendMessage("ActivateMovement", SendMessageOptions.DontRequireReceiver);
            }
            hasHitSomething = true;
        }
        else if ((animatedWallLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            // SendMessage est plus sûr si on n'a pas accès à la classe AnimatedWall
            other.SendMessage("BreakWall", SendMessageOptions.DontRequireReceiver);
            hasHitSomething = true;
        }

        if (hasHitSomething)
        {
            ExecuteDeath();
        }
    }

    private void ExecuteDeath()
    {
        isDead = true;

        // Camera Shake effect (only if CameraShake exists)
        // If CameraShake class is not in your project, comment these lines out
        // or replace with your actual camera shake logic.
        /*
        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(shakeDuration, shakeMagnitude);
        */

        // On stoppe le mouvement physique
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Désactive le collider
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (vfx != null)
        {
            // L'ASTUCE EST ICI : 
            // On passe le booléen à "true", ce qui tue la particule dans le VFX Graph
            // et déclenche automatiquement ton "Trigger Event On Die" !
            vfx.SetBool(forceDeathPropertyName, true);
        }

        // On détruit l'objet GameObject après un délai pour laisser l'explosion se voir
        Destroy(gameObject, timeBeforeDestroy);
    }
}