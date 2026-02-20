using UnityEngine;

public class DoorController : TriggerController
{
    private static readonly string IS_OPEN_PARAMETER = "Knock Knock";

    [SerializeField] private Animator m_Animator;
    [SerializeField] private ItemController m_ItemController;

    protected override void Interact()
    {
        if (TryOpenDoor())
        {
            DisableInteraction();
        }
    }

    private bool TryOpenDoor()
    {
        if (m_ItemController != null)
        {
            if (!InventorySystem.Instance.HasItem(m_ItemController.UniqueID))
            {
                UISystem.Instance.ShowPlayerWarning("Door is <b>locked</b>. You need to find the <b>key</b>.");
                return false;
            }
        }

        m_Animator.SetBool(IS_OPEN_PARAMETER, true);

        return true;
    }
}