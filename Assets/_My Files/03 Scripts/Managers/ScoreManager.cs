using TarodevController;
using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public PlayerController PlayerController;
    public TextMeshProUGUI scoreText;
    private int score = 0;

    private void AddScore()
    {
        score++;
        scoreText.text = score.ToString();
    }

    private void Start()
    {
        PlayerController.Jumped += AddScore;
    }

    private void OnDisable()
    {
        PlayerController.Jumped -= AddScore;
    }
}