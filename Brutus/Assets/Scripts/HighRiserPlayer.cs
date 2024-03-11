using UnityEngine;

public class HighRiserPlayer : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float jumpForce = 10f;
    public float gravityMultiplier = 2f; // Initial gravity multiplier
    public float jumpGravityMultiplier = 4f; // Gravity multiplier during jump


    private bool isGrounded;
    private Rigidbody2D rb;

    public Transform cameraTransform;
    public float cameraMoveSpeed = 2f;
    private float jumpHeightThreshold =5;
    private float originalCameraY;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private SpriteRenderer playerSpriteRenderer;
    private bool isBlinking = false;

    public AudioClip collisionSound; // Assign your collision sound in the Unity Editor
    public AudioClip deathSound; // Assign your death sound in the Unity Editor
    public  AudioSource audioSource;

    public GameObject WinScreen;

    private bool isFacingRight = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        originalCameraY = cameraTransform.position.y;
        initialPosition = this.transform.position;
        initialRotation = this.transform.rotation;


        playerSpriteRenderer = GetComponent<SpriteRenderer>();

        if (audioSource == null)
        {
            Debug.LogError("AudioSource component not found! Please attach an AudioSource component.");
        }

    }

    void Update()
    {
        // Move left and right continuously
        float horizontalInput = Input.GetAxis("Horizontal");
        Vector3 movement = new Vector3(1, 0f, 0f);
        if(isGrounded)
        transform.Translate(movement * moveSpeed * Time.deltaTime);

        // Jump when the space button is pressed and the player is grounded
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, (ForceMode2D)ForceMode.Impulse);
            isGrounded = false;
            // Increase gravity during the jump
            rb.gravityScale = jumpGravityMultiplier;
        }



        // Apply gravity multiplier to simulate faster descent
        rb.velocity += Vector2.down * (rb.gravityScale - 1) * gravityMultiplier * Time.deltaTime;

        if (cameraTransform != null && transform.position.y > jumpHeightThreshold)
            {
                cameraTransform.Translate(Vector3.up * cameraMoveSpeed * Time.deltaTime);
            }
            else if (cameraTransform != null)
            {
                // Reset the camera to its default state if the player descends
                float currentCameraY = Mathf.Lerp(cameraTransform.position.y, originalCameraY, Time.deltaTime);
                cameraTransform.position = new Vector3(cameraTransform.position.x, currentCameraY, cameraTransform.position.z);
            }
        }

        
    

    void OnCollisionEnter2D(UnityEngine.Collision2D collision)
    {

        // Check if the player collides with another GameObject tagged as "Obstacle"
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            // Play collision sound
            if (audioSource != null && collisionSound != null)
            {
                audioSource.PlayOneShot(collisionSound);
            }
            // Delay the reset of the player's position
            Invoke("ResetPlayerPosition", 1f);

            // Start the blink effect
            
        }

        if (collision.gameObject.CompareTag("Rum"))
        {

            WinScreen.SetActive(true);
        }


        // Check if the player has collided with the ground/platform
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            rb.gravityScale = gravityMultiplier; // Reset gravity scale on landing
        }

        // Check if the player has collided with a wall to change direction
        if (collision.gameObject.CompareTag("Wall") && transform.position.x < 0)
        {
            Debug.Log("Ammachi");
            // Change direction only when hitting the wall on the left side (x < 0)
            moveSpeed = Mathf.Abs(moveSpeed); // Make moveSpeed positive
            Flip();
        }
        else if (collision.gameObject.CompareTag("Wall") && transform.position.x > 0)
        {
            // Change direction only when hitting the wall on the right side (x > 0)
            moveSpeed = -Mathf.Abs(moveSpeed); // Make moveSpeed negativeFlip();
            Flip();
        }
    }

    void ResetPlayerPosition()
    {
        // Set the player's position to a specific reset position
        // You can customize this position based on your requirements
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        this.GetComponent<SpriteRenderer>().flipX = false;
        isBlinking = true;

        StartCoroutine(BlinkEffect(1f, 0.08f));



        // Set a flag to prevent multiple blink effects during the delay
        
        moveSpeed = -.75f;
        // Reset the blink flag
        isBlinking = false;
    }

    System.Collections.IEnumerator BlinkEffect(float duration, float blinkInterval)
    {
        // Toggle the visibility of the player's sprite in a loop for the given duration
        float timer = 0f;
        
        while (timer < duration)
        {
            playerSpriteRenderer.enabled = !playerSpriteRenderer.enabled;
            yield return new WaitForSeconds(blinkInterval);
            timer += blinkInterval;
        }

        // Ensure the player's sprite is visible at the end of the blink effect
        playerSpriteRenderer.enabled = true;
        moveSpeed = -1.42f;
    }

    void Flip()
    {
        if (this.GetComponent<SpriteRenderer>().flipX == true)
        {
            this.GetComponent<SpriteRenderer>().flipX = false;
        }
        else { this.GetComponent<SpriteRenderer>().flipX = true; }
    }
}
