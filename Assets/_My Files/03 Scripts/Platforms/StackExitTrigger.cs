using UnityEngine;
using TarodevController;

public class StackExitTrigger : MonoBehaviour
{
    [SerializeField] private CameraFollow.Mode triggerMode = CameraFollow.Mode.Vertical;

    private bool _activated = false;

    // Called by LevelSequencer at runtime to set the correct mode
    public void SetTriggerMode(CameraFollow.Mode mode)
    {
        triggerMode = mode;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_activated) return;
        if (!other.CompareTag("Player")) return;

        float targetX = transform.parent.position.x;

        CameraFollow cam = Camera.main.GetComponent<CameraFollow>();
        if (cam != null)
            cam.SetMode(triggerMode, targetX);

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
            player.SetFloorY(transform.position.y, triggerMode);

        _activated = true;
    }
}