using System;
using UnityEngine;

public class UISystem : MonoBehaviour
{
    #region Singleton
    public static UISystem Instance { get; protected set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            throw new System.Exception("An instance of UIManager singleton already exists.");
        }
        else
        {
            Instance = this;
        }
    }
    #endregion

    public class Tip
    {
        public string Message;
        public bool IsVisible;

        public override string ToString()
        {
            return Message;
        }
    }

    public Action<Tip> PlayerTipChanged;
    public Action<Tip> PlayerWarningChanged;

    public Tip PlayerTip { get; private set; }
    public Tip PlayerWarning { get; private set; }

    private float m_WarningTimestamp;
    [SerializeField] private float m_WarningDuration;

    private void Update()
    {
        if (Time.time > m_WarningTimestamp + m_WarningDuration)
        {
            HidePlayerWarning();
        }
    }

    public void ShowPlayerTip(string message)
    {
        PlayerTip = new Tip() { Message = message, IsVisible = true};
        PlayerTipChanged?.Invoke(PlayerTip);
    }

    public void HidePlayerTip()
    {
        ResetPlayerTip();
        PlayerTipChanged?.Invoke(PlayerTip);
    }

    private void ResetPlayerTip()
    {
        PlayerTip = new Tip() { Message = string.Empty, IsVisible = false };
    }

    public void ShowPlayerWarning(string message)
    {
        PlayerWarning = new Tip() { Message = message, IsVisible = true };
        PlayerWarningChanged?.Invoke(PlayerWarning);

        m_WarningTimestamp = Time.time;
    }

    public void HidePlayerWarning()
    {
        ResetPlayerWarning();
        PlayerWarningChanged?.Invoke(PlayerWarning);
    }

    private void ResetPlayerWarning()
    {
        PlayerWarning = new Tip() { Message = string.Empty, IsVisible = false };
        m_WarningTimestamp = float.MaxValue;
    }
}