using UnityEngine;

/// <summary>
/// Marks a GameObject as a respawn point.
/// Place this component on the spawn GameObject positioned in the scene.
/// Act as a checkpoint trigger. When the player enters it, it updates the
/// active RespawnPoint on the PlayerRespawnController.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RespawnPoint : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool _drawGizmo = true;
    [SerializeField] private Color _gizmoColor = new Color(0f, 1f, 0f, 0.3f);

    // Optimisation : éviter de réassigner le checkpoint s'il est déjà actif
    private bool _isActivated = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_isActivated) return;

        // TryGetComponent is faster and safer than CompareTag + GetComponent
        if (other.TryGetComponent(out PlayerRespawnController respawnController))
        {
            respawnController.SetRespawnPoint(this);
            _isActivated = true;
            
            Debug.Log($"[RespawnPoint] Checkpoint validé : {gameObject.name} ! Position: {transform.position}");
            // On peut ajouter des VFX ou SFX de validation ici
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_drawGizmo) return;
        
        Gizmos.color = _gizmoColor;
        Collider col = GetComponent<Collider>();
        
        if (col != null)
        {
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        }
        else
        {
            // Fallback si pas de collider
            Gizmos.DrawSphere(transform.position, 0.5f);
        }
        
        // Affiche la direction dans laquelle le joueur va regarder au respawn
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
    }
#endif
}
