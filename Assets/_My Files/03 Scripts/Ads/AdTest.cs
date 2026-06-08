using UnityEngine;

public class AdTest : MonoBehaviour
{
    void Start()
    {
        Invoke(nameof(ShowTestAd), 5f);
    }

    void ShowTestAd()
    {
        FindObjectOfType<AdManager>().ShowInterstitialAd();
    }
}