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

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
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
        }
        else if (collision.gameObject.CompareTag("Wall") && transform.position.x > 0)
        {
            // Change direction only when hitting the wall on the right side (x > 0)
            moveSpeed = -Mathf.Abs(moveSpeed); // Make moveSpeed negative
        }
    }
}
