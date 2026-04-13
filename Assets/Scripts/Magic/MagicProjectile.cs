using UnityEngine;
using UnityEngine.VFX; // Requis pour le VisualEffect

public class MagicProjectile : MonoBehaviour
{
    [Header("Collision Layers")]
    [SerializeField] private LayerMask destructibleWallLayer;
    [SerializeField] private LayerMask movableObjectLayer;
    [SerializeField] private LayerMask animatedWallLayer;

    [Header("Effets & Feedback")]
    [SerializeField] private float shakeDuration = 0.2f;
    [SerializeField] private float shakeMagnitude = 0.3f;

    [Header("VFX On Die Settings")]
    [Tooltip("Coche cette case si ce projectile doit déclencher un event VFX en mourant")]
    [SerializeField] private bool useOnDieEvent = false; 
    [SerializeField] private string onDieEventName = "on die"; 
    [SerializeField] private float vfxDieDelay = 2.0f;

    private bool isDead = false;

    private void OnTriggerEnter(Collider other)
    {
        // Sécurité si l'objet est déjà en train de mourir (utile uniquement si l'event est activé)
        if (isDead && useOnDieEvent) return; 

        bool hasHitSomething = false;

        if ((destructibleWallLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            Destroy(other.gameObject);
            hasHitSomething = true;
        }
        else if ((movableObjectLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            if (other.CompareTag("MovableObject"))
            {
                other.SendMessage("ActivateMovement", SendMessageOptions.DontRequireReceiver);
            }
            hasHitSomething = true;
        }
        else if ((animatedWallLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            other.SendMessage("BreakWall", SendMessageOptions.DontRequireReceiver);
            hasHitSomething = true;
        }

        if (hasHitSomething)
        {
            /*
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(shakeDuration, shakeMagnitude);
            }
            */

            // --- LE CHOIX SE FAIT ICI ---
            if (useOnDieEvent)
            {
                // NOUVELLE LOGIQUE : On arrête la balle et on lance l'event
                isDead = true;

                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }

                Collider col = GetComponent<Collider>();
                if (col != null) col.enabled = false;

                VisualEffect vfx = GetComponentInChildren<VisualEffect>();
                if (vfx != null)
                {
                    vfx.SendEvent(onDieEventName);
                }

                Destroy(gameObject, vfxDieDelay);
            }
            else
            {
                // ANCIENNE LOGIQUE : Destruction instantanée
                Destroy(gameObject);
            }
        }
    }
}