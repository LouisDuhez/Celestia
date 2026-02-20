using UnityEngine;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine.InputSystem; // Obligatoire pour le nouveau système

public class camera_Manager : MonoBehaviour
{
    [Header("Liste des caméras (dans l'ordre de rotation)")]
    public List<CinemachineCamera> vCameras;

    private int currentIndex = 0;
    private int priorityActive = 20;
    private int priorityInactive = 10;

    // Cette fonction sera appelée par l'Input Action "RotateLeft"
    void OnRotateLeft()
    {
        RotateCamera(-1);
    }

    void OnRotateRight()
    {
        RotateCamera(1);
    }

    private void RotateCamera(int direction)
    {
        currentIndex += direction;

        if (currentIndex < 0) currentIndex = vCameras.Count - 1;
        else if (currentIndex >= vCameras.Count) currentIndex = 0;

        UpdateCameraPriorities();
    }

    private void UpdateCameraPriorities()
    {
        for (int i = 0; i < vCameras.Count; i++)
        {
            vCameras[i].Priority = (i == currentIndex) ? priorityActive : priorityInactive;
        }
    }
}