using UnityEngine;

public class Flower : MonoBehaviour, ICollectable
{
    public void Collect()
    {
        Debug.Log("flower Collected");
        Destroy(gameObject);
    }
}
