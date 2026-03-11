using UnityEngine;

[RequireComponent (typeof(TriggerController))]
public class ProximityMessageController : MonoBehaviour
{
    private static readonly string PLAYER_TAG = "Player";
    
    [SerializeField] private string m_MessageToPlayer;

    private TriggerController m_TriggerController;

    private void Awake()
    {
        m_TriggerController = GetComponent<TriggerController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(PLAYER_TAG) && m_TriggerController.CanInteract)
        {
            if (!string.IsNullOrEmpty(m_MessageToPlayer))
            {
                UISystem.Instance.ShowPlayerTip(m_MessageToPlayer);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        UISystem.Instance.HidePlayerTip();
    }
}
