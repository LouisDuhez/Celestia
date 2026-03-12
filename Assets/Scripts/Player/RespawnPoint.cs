using UnityEngine;

/// <summary>
/// Marks a GameObject as a respawn point.
/// Place this component on the spawn GameObject positioned in the scene.
/// Later, checkpoints can simply activate/deactivate instances and
/// <see cref="PlayerRespawnController"/> will always use the last active one.
/// </summary>
public class RespawnPoint : MonoBehaviour
{
    // Intentionally minimal: position is read directly from transform.
    // Add checkpoint-specific data here when the checkpoint system is implemented.
}
