using System.Collections.Generic;
using UnityEngine;

public class InventorySystem : MonoBehaviour
{
    #region Singleton
    public static InventorySystem Instance { get; protected set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            throw new System.Exception("An instance of InventoryBehaviour singleton already exists.");
        }
        else
        {
            Instance = this;
        }
    }
    #endregion

    private List<int> m_Items = new List<int>();

    public void StoreItem(int key)
    {
        if (!m_Items.Contains(key))
        {
            m_Items.Add(key);
        }
    }

    public void ConsumeItem(int key)
    {
        if (m_Items.Contains(key))
        {
            m_Items.Remove(key);
        }
    }

    public bool HasItem(int key)
    {
        return m_Items.Contains(key);
    }
}