using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{


    public void LoadScene_Collectibles() {

        SceneManager.LoadScene("CollectiblesScene");
    }

    public void LoadScene_MainScene()
    {
        SceneManager.LoadScene("Main Scene");
    }

}
