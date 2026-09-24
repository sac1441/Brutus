using UnityEngine;

/// <summary>
/// Starts this object's looping Animator state at a random point, so many copies
/// (e.g. every egg in the level) don't animate in perfect sync.
/// </summary>
[RequireComponent(typeof(Animator))]
public class RandomAnimStart : MonoBehaviour
{
    private void Start()
    {
        var anim = GetComponent<Animator>();
        if (anim == null || anim.runtimeAnimatorController == null) return;
        var info = anim.GetCurrentAnimatorStateInfo(0);
        anim.Play(info.fullPathHash, 0, Random.value);
    }
}
