using System.Collections;
using UnityEngine;
using StarterAssets; // INDISPENSABLE pour parler au système d'input du Starter Assets

[RequireComponent(typeof(CharacterController))]
public class LedgeLocator : MonoBehaviour
{
    [Header("Paramètres de Détection")]
    [SerializeField] protected float reachDistance = 1.5f;
    [SerializeField] protected LayerMask ledgeLayer;
    [SerializeField] protected float maxLedgeHeightDifference = 1.0f;

    [Header("Paramètres de Grimpe")]
    [SerializeField] protected float forwardClimbOffset = 0.5f;
    [SerializeField] protected float climbSpeed = 1f;

    // Références
    private CharacterController charController;
    private Animator anim;
    private MonoBehaviour movementScript;
    private StarterAssetsInputs _input; // Référence aux inputs du Starter Assets

    private bool isGrabbing = false;
    private bool isClimbing = false;
    private GameObject currentLedge;

    private void Start()
    {
        charController = GetComponent<CharacterController>();
        anim = GetComponentInChildren<Animator>();

        // On récupère le script qui gère les touches (Clavier/Manette)
        _input = GetComponent<StarterAssetsInputs>();

        // On cherche le script de mouvement (ThirdPersonController)
        movementScript = GetComponent("ThirdPersonController") as MonoBehaviour;
    }

    private void Update()
    {
        if (isClimbing) return;

        if (!isGrabbing)
        {
            DetectLedge();
        }
        else
        {
            HandleInput();
        }
    }

    private void DetectLedge()
    {
        if (charController.isGrounded) return;

        Vector3 rayOrigin = transform.position + Vector3.up * (charController.height);
        RaycastHit hit;

        // Debug visuel
        Debug.DrawRay(rayOrigin, transform.forward * reachDistance, Color.red);

        if (Physics.Raycast(rayOrigin, transform.forward, out hit, reachDistance, ledgeLayer))
        {
            Ledge ledgeScript = hit.collider.GetComponent<Ledge>();
            if (ledgeScript == null) return;
            if (hit.normal.y < -0.1f) return;

            // On vérifie qu'on ne monte pas trop vite (saut montant)
            if (charController.velocity.y < 1.0f)
            {
                GrabLedge(hit, ledgeScript);
            }
        }
    }

    private void GrabLedge(RaycastHit hit, Ledge ledgeScript)
    {
        isGrabbing = true;
        currentLedge = hit.collider.gameObject;

        // Désactiver le mouvement standard
        if (movementScript) movementScript.enabled = false;

        // Arrêter toute inertie ou mouvement résiduel dans les inputs
        _input.move = Vector2.zero;
        _input.jump = false;
        _input.sprint = false;

        if (anim) anim.SetBool("LedgeHanging", true);

        // Snap position
        Vector3 targetPos = hit.point + (hit.normal * ledgeScript.forwardOffset);
        targetPos.y = hit.collider.bounds.max.y - ledgeScript.verticalOffset;

        transform.rotation = Quaternion.LookRotation(-hit.normal);

        charController.enabled = false;
        transform.position = targetPos;
        charController.enabled = true;
    }

    private void HandleInput()
    {
        // CORRECTION ICI : On utilise le système d'input du Starter Asset
        // .y correspond à "Vertical" (Avancer/Reculer ou Haut/Bas)
        float v = _input.move.y;

        if (v > 0.1f) StartCoroutine(ClimbRoutine());
        else if (v < -0.1f) DropLedge();
    }

    private IEnumerator ClimbRoutine()
    {
        isClimbing = true;
        if (anim) anim.SetBool("LedgeHanging", false);
        if (anim) anim.SetBool("LedgeClimbing", true);

        Vector3 startPos = transform.position;
        float finalY = currentLedge.GetComponent<Collider>().bounds.max.y;

        Vector3 endPos = startPos + (transform.forward * forwardClimbOffset);
        endPos.y = finalY;

        charController.enabled = false;

        float timer = 0;
        while (timer < climbSpeed)
        {
            timer += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, endPos, timer / climbSpeed);
            yield return null;
        }

        transform.position = endPos;
        FinishClimb();
    }

    private void DropLedge()
    {
        FinishClimb();
    }

    private void FinishClimb()
    {
        isGrabbing = false;
        isClimbing = false;
        currentLedge = null;

        // 1. On coupe les inputs (pour ne pas sauter dès qu'on arrive)
        if (_input != null)
        {
            _input.jump = false;
            _input.move = Vector2.zero;
        }

        // 2. On réactive le CharacterController
        if (charController) charController.enabled = true;

        // 3. On réactive le script de mouvement ET on le reset
        if (movementScript)
        {
            movementScript.enabled = true;
            var tpc = movementScript as ThirdPersonController;
            if (tpc != null)
            {
                tpc.ForceGroundedState();
            }
        }

        // 4. On coupe les anims de grimpe (avec sécurité anti-crash)
        try
        {
            if (anim)
            {
                anim.SetBool("LedgeHanging", false);
                anim.SetBool("LedgeClimbing", false);
            }
        }
        catch { }
    }

    private void OnDrawGizmos()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            Gizmos.color = Color.red;
            Vector3 rayOrigin = transform.position + Vector3.up * (cc.height * 0.9f);
            Gizmos.DrawLine(rayOrigin, rayOrigin + transform.forward * reachDistance);
        }
    }
}