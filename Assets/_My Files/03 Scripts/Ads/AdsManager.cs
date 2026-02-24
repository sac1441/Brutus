using UnityEngine;

public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance;
    public int adsShowCount = 3;
    private int playerDeathCount = 0;

    #region Ads Manager Instance
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    public void CheckForAdsTime()
    {
        playerDeathCount++;
        if (playerDeathCount == adsShowCount)
        {
            FindAnyObjectByType<InterstitialAdExample>().ShowAd();
            Debug.Log("<color=red>Showing interstitial Ads</color>");
            playerDeathCount = 0;
        }
    }
}
