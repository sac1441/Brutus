using UnityEngine;

public class Egg : MonoBehaviour, ICollectable
{
    public void Collect()
    {
        Debug.Log("collected egg");
        Destroy(gameObject);
    }
}
