using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public GameObject UiMenu;

    public void NewGame()
    {
        SceneManager.LoadScene(0);
    }

    public void Pause()
    {
        UiMenu.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Continue()
    {
        UiMenu?.SetActive(false);
        Time.timeScale = 1f;
    }

    public void Quit()
    {
        Application.Quit();
    }
}
