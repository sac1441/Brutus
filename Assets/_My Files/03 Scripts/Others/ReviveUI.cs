using TarodevController;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ReviveUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private GameObject revivePanel;
    [SerializeField] private EggShopUI eggShopUI;

    [Header("Egg Revive")]
    [SerializeField] private int eggReviveCost = 30;
    [SerializeField] private Button eggButton;
    [SerializeField] private Text eggCostText;

    [Header("Close")]
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (revivePanel != null) revivePanel.SetActive(false);

        eggButton.onClick.AddListener(OnEggReviveClicked);
        closeButton.onClick.AddListener(OnCloseClicked);
    }

    private void OnEnable()
    {
        if (playerController != null) playerController.Died += Show;
    }

    private void OnDisable()
    {
        if (playerController != null) playerController.Died -= Show;
    }

    private void Show()
    {
        if (eggCostText != null) eggCostText.text = eggReviveCost.ToString();
        eggButton.interactable = true; // always clickable now - either revives or opens the shop

        if (revivePanel != null) revivePanel.SetActive(true);
    }

    private void Hide()
    {
        if (revivePanel != null) revivePanel.SetActive(false);
    }

    // ---------- Egg revive ----------

    private void OnEggReviveClicked()
    {
        Debug.Log($"[ReviveUI] Egg button clicked. EggCount={playerController.EggCount}, eggReviveCost={eggReviveCost}, eggShopUI assigned={eggShopUI != null}");

        if (playerController.EggCount < eggReviveCost)
        {
            Debug.Log("[ReviveUI] Not enough eggs - calling eggShopUI.Open(revivePanel)");
            eggShopUI.Open(revivePanel);
            return;
        }

        Debug.Log("[ReviveUI] Enough eggs - spending and reviving");
        if (!playerController.TrySpendEggs(eggReviveCost)) return;

        Hide();
        playerController.Revive();
    }

    // ---------- Close (restart from beginning) ----------

    private void OnCloseClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(1);
    }

    private void OnDestroy()
    {
        eggButton.onClick.RemoveAllListeners();
        closeButton.onClick.RemoveAllListeners();
    }
}