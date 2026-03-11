using TMPro;
using UnityEngine;

public class PlayerWarningController : MonoBehaviour
{
    [SerializeField] private CanvasGroup m_Container;
    [SerializeField] private TextMeshProUGUI m_Text;

    private void Awake()
    {
        m_Container.alpha = 0f;
    }

    private void Start()
    {
        UISystem.Instance.PlayerWarningChanged += OnPlayerWarningChanged;
    }

    private void OnDestroy()
    {
        if (UISystem.Instance != null)
        {
            UISystem.Instance.PlayerWarningChanged -= OnPlayerWarningChanged;
        }
    }

    private void OnPlayerWarningChanged(UISystem.Tip tip)
    {
        m_Text.text = tip.ToString();
        m_Container.alpha = tip.IsVisible ? 1f : 0f;
    }
}
