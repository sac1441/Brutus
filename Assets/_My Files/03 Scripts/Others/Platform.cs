using UnityEngine;

public class Platform : MonoBehaviour
{
    public Color groundColor;

    private void Start()
    {
        foreach (Transform child in transform)
        {
            if (child.GetComponent<BoxCollider2D>() != null && !child.GetComponent<PlatformEffector2D>())
            {
                child.GetComponent<BoxCollider2D>().enabled = false;
            }
        }
    }
}
