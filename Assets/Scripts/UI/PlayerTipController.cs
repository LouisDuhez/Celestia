using TMPro;
using UnityEngine;

public class PlayerTipController : MonoBehaviour
{
    [SerializeField] private CanvasGroup m_Container;
    [SerializeField] private TextMeshProUGUI m_Text;

    private void Awake()
    {
        m_Container.alpha = 0f;
    }

    private void Start()
    {
        UISystem.Instance.PlayerTipChanged += OnPlayerTipChanged;
    }

    private void OnDestroy()
    {
        if (UISystem.Instance != null)
        {
            UISystem.Instance.PlayerTipChanged -= OnPlayerTipChanged;
        }
    }

    private void OnPlayerTipChanged(UISystem.Tip tip)
    {
        m_Text.text = tip.ToString();
        m_Container.alpha = tip.IsVisible ? 1f : 0f;
    }
}
