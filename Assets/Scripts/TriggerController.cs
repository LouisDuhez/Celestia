using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

public class TriggerController : MonoBehaviour
{
    private static readonly string PLAYER_TAG = "Player";
    private static readonly string INTERACT_ACTION = "Interact";

    private InputAction m_InteractAction;

    public bool CanInteract { get; protected set; } = true;

    private void Start()
    {
        m_InteractAction = InputSystem.actions.FindAction(INTERACT_ACTION);
    }

    private void OnTriggerStay(Collider other)
    {
        Assert.IsNotNull(m_InteractAction, $"Input Action {INTERACT_ACTION} is missing. Please add it and its bindings to the InputSystem_Actions asset.");

        if (other.CompareTag(PLAYER_TAG) && m_InteractAction.IsPressed() && CanInteract)
        {
            Interact();
        }
    }

    protected virtual void Interact() 
    {
    }

    protected void DisableInteraction()
    {
        CanInteract = false;
        UISystem.Instance.HidePlayerTip();
    }
}
