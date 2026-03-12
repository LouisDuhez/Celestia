using UnityEngine;

/// <summary>
/// Monitors the player's Y position and teleports them back to the active
/// <see cref="RespawnPoint"/> when they fall below <see cref="_deathYThreshold"/>.
///
/// Setup:
///  1. Add this component to the Player GameObject (same as PlayerMovementController).
///  2. Place a GameObject in the scene, add the <see cref="RespawnPoint"/> component to it.
///  3. Assign that GameObject to <see cref="_respawnPoint"/> in the Inspector.
///  4. Tune <see cref="_deathYThreshold"/> to match your level's kill-zone height.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerMovementController))]
public class PlayerRespawnController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Death Zone")]
    [Tooltip("The player is respawned when their Y position drops below this value.")]
    [SerializeField] private float _deathYThreshold = -10f;

    [Header("References")]
    [Tooltip("The RespawnPoint the player will be teleported to. " +
             "Later, swap this reference to the last activated checkpoint.")]
    [SerializeField] private RespawnPoint _respawnPoint;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private CharacterController     _characterController;
    private PlayerMovementController _movementController;

    /// <summary>
    /// Prevents triggering the respawn multiple times in the same fall.
    /// </summary>
    private bool _isRespawning;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _movementController  = GetComponent<PlayerMovementController>();
    }

    private void Update()
    {
        if (_isRespawning) return;

        if (transform.position.y < _deathYThreshold)
            Respawn();
    }

    // -------------------------------------------------------------------------
    // Respawn logic
    // -------------------------------------------------------------------------

    /// <summary>
    /// Teleports the player to the active <see cref="RespawnPoint"/> and resets
    /// all movement state so they land cleanly without residual velocity.
    /// </summary>
    public void Respawn()
    {
        if (_respawnPoint == null)
        {
            Debug.LogWarning("[PlayerRespawnController] No RespawnPoint assigned. Cannot respawn.", this);
            return;
        }

        _isRespawning = true;

        // Disable the CharacterController before moving the transform.
        // Unity's CC will fight against manual position writes if left enabled.
        _characterController.enabled = false;

        transform.position = _respawnPoint.transform.position;
        transform.rotation = _respawnPoint.transform.rotation;

        _characterController.enabled = true;

        // Reset velocity and animator flags so the player lands cleanly.
        _movementController.ForceGroundedState();

        _isRespawning = false;
    }

#if UNITY_EDITOR
    // -------------------------------------------------------------------------
    // Debug gizmos
    // -------------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        // Draw a red horizontal plane at the death threshold so the height is
        // visible directly in the Scene view without running the game.
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Vector3 center = new Vector3(transform.position.x, _deathYThreshold, transform.position.z);
        Gizmos.DrawCube(center, new Vector3(10f, 0.05f, 10f));

        Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
        Gizmos.DrawWireCube(center, new Vector3(10f, 0.05f, 10f));
    }
#endif
}
