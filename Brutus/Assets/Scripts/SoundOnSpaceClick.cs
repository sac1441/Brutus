using UnityEngine;

public class SoundOnSpaceClick : MonoBehaviour
{
    public AudioClip soundClip; // Assign your audio clip in the Unity Editor
    private AudioSource audioSource;

    void Start()
    {
        // Get the AudioSource component attached to the same GameObject
        audioSource = GetComponent<AudioSource>();

        // Check if an AudioSource component is attached
        if (audioSource == null)
        {
            Debug.LogError("AudioSource component not found! Please attach an AudioSource component.");
        }
    }

    void Update()
    {
        // Check if the space button is pressed
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // Check if the AudioSource and AudioClip are assigned
            if (audioSource != null && soundClip != null)
            {
                // Play the assigned audio clip
                audioSource.PlayOneShot(soundClip);
            }
            else
            {
                Debug.LogError("AudioSource or AudioClip not assigned! Please assign them in the Unity Editor.");
            }
        }
    }
}
