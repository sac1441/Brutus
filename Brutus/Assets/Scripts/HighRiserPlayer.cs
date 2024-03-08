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

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        originalCameraY = cameraTransform.position.y;
    }

    void Update()
    {
        // Get the SpriteRenderer component
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();

        // Flip the sprite based on the movement direction
        spriteRenderer.flipX = movementDirection < 0;

        // Move continuously left and right
        Vector3 movement = new Vector3(movementDirection, 0f, 0f);
        transform.Translate(movement * moveSpeed * Time.deltaTime);

        if (isGrounded)
            transform.Translate(movement * moveSpeed * Time.deltaTime);

        // Jump when the space button is pressed and the player is grounded
        if (Input.GetButtonDown("Jump") && isGrounded)
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
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if the player has collided with the ground/platform
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            rb.gravityScale = gravityMultiplier; // Reset gravity scale on landing
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
            }
        }
    }
}
