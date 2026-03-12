using UnityEngine;

/// <summary>
/// Immutable data returned by <see cref="LedgeDetector.TryDetect"/> when a
/// valid climbable ledge is found.
///
/// All positions are in world space.
/// </summary>
public readonly struct LedgeGrabData
{
    /// <summary>
    /// The world position where the player's hands/body should snap to.
    /// XZ = against the wall face (offset by char radius from the hit point).
    /// Y  = ledge top surface minus a small hand offset.
    /// </summary>
    public readonly Vector3 GrabPosition;

    /// <summary>
    /// Same as <see cref="GrabPosition"/> but with the depth component
    /// (along camera forward) replaced by the wall collider's centre depth.
    ///
    /// Use this to depth-snap the player onto the wall's plane, exactly
    /// like <see cref="PlayerDepthReprojector"/> does on camera rotation.
    /// Pass this to <c>PlayerMovementController</c> or directly set
    /// <c>transform.position</c> after disabling the CharacterController.
    /// </summary>
    public readonly Vector3 TargetDepthPosition;

    /// <summary>
    /// The outward face normal of the wall collider.
    /// Use this to orient the player facing away from the wall:
    ///   <c>Quaternion.LookRotation(-WallNormal)</c>
    /// </summary>
    public readonly Vector3 WallNormal;

    /// <summary>
    /// The exact world Y of the ledge top surface, found by the downward
    /// top-edge raycast. Use this to compute the climb-over target position.
    /// </summary>
    public readonly float LedgeTopY;

    public LedgeGrabData(Vector3 grabPosition, Vector3 targetDepthPosition,
                          Vector3 wallNormal, float ledgeTopY)
    {
        GrabPosition        = grabPosition;
        TargetDepthPosition = targetDepthPosition;
        WallNormal          = wallNormal;
        LedgeTopY           = ledgeTopY;
    }
}
