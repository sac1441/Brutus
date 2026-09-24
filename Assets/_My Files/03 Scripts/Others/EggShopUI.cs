using UnityEngine;
using UnityEngine.UI;

public class EggShopUI : MonoBehaviour
{
    [SerializeField] private GameObject eggShopPanel;
    [SerializeField] private Button closeButton;

    private GameObject _callerPanel;

    private void Awake()
    {
        if (eggShopPanel == null) Debug.LogError("EggShopUI: eggShopPanel is not assigned!", this);
        if (closeButton == null) Debug.LogError("EggShopUI: closeButton is not assigned!", this);

        if (eggShopPanel != null) eggShopPanel.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    public void Open(GameObject callerPanel)
    {
        Debug.Log($"[EggShopUI] Open() called. eggShopPanel assigned={eggShopPanel != null}, callerPanel={(callerPanel != null ? callerPanel.name : "null")}");

        if (eggShopPanel == null)
        {
            Debug.LogError("[EggShopUI] eggShopPanel is not assigned!", this);
            return;
        }

        _callerPanel = callerPanel;

        if (_callerPanel != null) _callerPanel.SetActive(false);
        eggShopPanel.SetActive(true);

        Debug.Log($"[EggShopUI] eggShopPanel.activeSelf after SetActive(true) = {eggShopPanel.activeSelf}");
    }

    private void Close()
    {
        if (eggShopPanel != null) eggShopPanel.SetActive(false);
        if (_callerPanel != null) _callerPanel.SetActive(true);
    }

    private void OnDestroy()
    {
        closeButton.onClick.RemoveAllListeners();
    }
}