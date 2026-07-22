using TarodevController;
using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public PlayerController PlayerController;
    public TextMeshProUGUI scoreText;
    private int score = 1;

    private void AddScore()
    {
        score++;
        scoreText.text = score.ToString();

        // TEMPORARY DIAGNOSTIC -- remove once the delayed-score issue is found.
        Debug.Log($"[ScoreManager] AddScore fired -> score={score}, player Y={PlayerController.transform.position.y:F2}");
    }

    private void Start()
    {
        Debug.Log($"[ScoreManager] Start() -- subscribing to Jumped. PlayerController null? {PlayerController == null}, scoreText null? {scoreText == null}");
        PlayerController.Jumped += AddScore;
    }

    private void OnDisable()
    {
        PlayerController.Jumped -= AddScore;
    }
}
