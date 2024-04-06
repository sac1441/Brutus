using System.Collections;
using UnityEngine;

public class HighRiserPlayer : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float jumpForce = 10f;
    public float gravityMultiplier = 2f;
    public float jumpGravityMultiplier = 4f;
    public float wallCollisionCheckDistance = 0.1f;

    private bool isGrounded;
    private Rigidbody2D rb;
    private float movementDirection = 1f; // Initial movement direction

    public Transform cameraTransform;

    public float cameraMoveSpeed = 2f;
    private float jumpHeightThreshold = 5;

    private float originalCameraY;

    public float cameraMoveDistance = .5f; // Reduced distance to move the camera vertically
    private float cameraFollowSpeed = 5f;

    // Blinking variables
    public float blinkDuration = 1f; // Duration for which the player will blink
    private bool isBlinking = false;
    public float blinkInterval = 0.1f; // Interval between each blink

    private Vector3 initialPlayerPosition;
    private Quaternion initialRotation;
    private bool facingLeft = true; // Player's facing direction (initially left)

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        originalCameraY = cameraTransform.position.y;
        initialPlayerPosition = transform.position;
        initialRotation = transform.rotation;
    }

    void Update()
    {
        // Get the SpriteRenderer component
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();

        // Flip the sprite based on the movement direction
        spriteRenderer.flipX = !facingLeft; // Flip the sprite if not facing left

        // Move continuously left and right
        if (!isBlinking) // Only move if not blinking
        {
            Vector3 movement = new Vector3(movementDirection, 0f, 0f);
            transform.Translate(movement * moveSpeed * Time.deltaTime);
            if (isGrounded)
                transform.Translate(movement * moveSpeed * Time.deltaTime);
        }

        // Jump when the space button is pressed and the player is grounded
        if (Input.GetButtonDown("Jump") && isGrounded && !isBlinking)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode2D.Impulse);
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

        // Update camera position to follow the player vertically
        Vector3 targetCameraPosition = new Vector3(cameraTransform.position.x, transform.position.y + 2, cameraTransform.position.z);
        cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetCameraPosition, cameraFollowSpeed * Time.deltaTime);
    }

    void MoveCamera()
    {
        // Define the target position for the camera
        Vector3 targetPosition = Camera.main.transform.position + new Vector3(0f, 0.4f, 0f);

        // Use smoothstep for easing the interpolation
        float lerpSpeed = 0.05f; // Adjust this value to control the speed of the camera movement
        float smoothStep = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lerpSpeed * Time.deltaTime));

        // Use Vector3.Lerp to smoothly interpolate between the current and target positions
        Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, targetPosition, smoothStep);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if the player has collided with the ground/platform
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            rb.gravityScale = gravityMultiplier; // Reset gravity scale on landing

            if (collision.gameObject.transform.position.y < transform.position.y)
            {
                // Perform actions when the player lands on the top floor
                Debug.Log("Player has landed on the top floor!");
                // You can add additional actions here, such as moving the camera or triggering events.
                // Move the camera vertically by a fixed distance quickly
                // Move the camera vertically by a fixed distance smoothly
                Vector3 targetPosition = new Vector3(cameraTransform.position.x, cameraTransform.position.y + 0.2f, cameraTransform.position.z);
                StartCoroutine(MoveCameraSmoothly(cameraTransform.position, targetPosition));
            }
        }

        // Check if the player has collided with a wall to change direction
        if (collision.gameObject.CompareTag("Wall"))
        {
            // Use the collision normal to determine which side of the wall was hit
            Vector2 normal = collision.contacts[0].normal;

            if (Mathf.Abs(normal.x) > 0.5f) // Check if collision is more horizontal than vertical
            {
                // Flip the movement direction based on the wall hit
                movementDirection *= -1;
                facingLeft = !facingLeft; // Update facing direction
            }
        }

        // Check if the player has collided with an obstacle
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            
            // Respawn the player and start blinking
            StartCoroutine(RespawnAndBlink(true));
        }
    }

    IEnumerator MoveCameraSmoothly(Vector3 startPosition, Vector3 targetPosition)
    {
        float elapsedTime = 0f;
        float totalTime = .1f; // Calculate total time based on speed and distance

        while (elapsedTime < totalTime)
        {
            cameraTransform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / totalTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        cameraTransform.position = targetPosition; // Ensure the camera reaches the target position exactly
    }

    IEnumerator RespawnAndBlink(bool direction)
    {

        // Respawn the player
        transform.position = initialPlayerPosition;
        

        // Start blinking coroutine
        isBlinking = true;
        float endTime = Time.time + blinkDuration;
        while (Time.time < endTime)
        {
            
            // Toggle player visibility
            GetComponent<SpriteRenderer>().enabled = !GetComponent<SpriteRenderer>().enabled;
            // Wait for blink interval
            yield return new WaitForSeconds(blinkInterval);
        }

        // Ensure player is visible when blinking ends
        GetComponent<SpriteRenderer>().enabled = true;

        // Reset blink flag
        isBlinking = false;
    }
}
