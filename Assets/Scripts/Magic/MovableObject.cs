using System.Collections;
using UnityEngine;

public class MovableObject : MonoBehaviour
{
    [Header("Configuration du Mouvement")]
    [Tooltip("Déplacement à ajouter (ex: X=0, Y=5, Z=0 pour monter de 5m)")]
    [SerializeField] private Vector3 movementOffset = new Vector3(0, 0, 0);

    [Tooltip("Rotation à ajouter en degrés (ex: Y=90 pour tourner de 90°)")]
    [SerializeField] private Vector3 rotationOffset = new Vector3(0, 90, 0);

    [Header("Animation")]
    [SerializeField] private float duration = 1.0f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine currentCoroutine;
    private bool hasMoved = false;

    /// <summary>
    /// Cette fonction est appelée par le projectile
    /// </summary>
    public void ActivateMovement()
    {
        // if (hasMoved) return; 

        Vector3 targetPos = transform.position + movementOffset;

        Quaternion targetRot = transform.rotation * Quaternion.Euler(rotationOffset);

        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(SmoothMoveRoutine(targetPos, targetRot));
        
        hasMoved = true;
    }

    private IEnumerator SmoothMoveRoutine(Vector3 targetPos, Quaternion targetRot)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float timeElapsed = 0f;

        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;
            float t = timeElapsed / duration;
            float curveValue = animationCurve.Evaluate(t);

            transform.position = Vector3.Lerp(startPos, targetPos, curveValue);
            transform.rotation = Quaternion.Lerp(startRot, targetRot, curveValue);

            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;
    }
}