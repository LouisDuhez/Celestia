using UnityEngine;

/// <summary>
/// Subscribes to Chataigne OSC string events and controls the camera manager.
/// </summary>
public class MicrobitManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The core camera manager responsible for handling blends and depth reprojection.")]
    [SerializeField] private camera_Manager _cameraManager;

    // -------------------------------------------------------------------------
    // Event Receiver for Chataigne
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by Chataigne via Unity Event when an OSC string message is received.
    /// Expected format: "ID=1,Pos=Nord,Sens=Droite"
    /// </summary>
    /// <param name="data">The raw comma-separated string from Micro:bit.</param>
    public void OnReceiveMicrobitData(string data)
    {
        if (string.IsNullOrEmpty(data)) return;
        
        string[] parts = data.Split(',');

        if (parts.Length >= 3)
        {
            string posValue = parts[1].Split('=')[1].Trim();

            // Map direction strings to your camera_Manager array indices.
            // Adjust these numbers if your camera_Manager list is ordered differently.
            // Typical clockwise order: 0 = North, 1 = East, 2 = South, 3 = West.
            int targetIndex = -1;

            switch (posValue)
            {
                case "Nord":  targetIndex = 0; break;
                case "Est":   targetIndex = 1; break;
                case "Sud":   targetIndex = 2; break;
                case "Ouest": targetIndex = 3; break;
            }

            if (targetIndex != -1 && _cameraManager != null)
            {
                // Call the new method on camera_Manager to rotate cleanly
                // taking care of blends, cooldowns, and depth snapping.
                _cameraManager.ForceSetCamera(targetIndex);
            }
        }
    }
}
