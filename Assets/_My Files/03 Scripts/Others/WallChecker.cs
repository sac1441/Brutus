using UnityEngine;

public class WallChecker : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            foreach (Transform child in collision.transform.parent)
            {
                if (child.GetComponent<BoxCollider2D>() != null && !child.GetComponent<PlatformEffector2D>())
                {
                    //Debug.Log("wall Enabled", child.gameObject);
                    child.GetComponent<BoxCollider2D>().enabled = true;
                }
            }
        }
    }
}
