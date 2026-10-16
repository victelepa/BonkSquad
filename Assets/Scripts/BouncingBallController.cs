using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BouncingBallController : MonoBehaviour
{
    [SerializeField] private float launchImpulse = 20f;
    [SerializeField] private float maxSpeed = 12f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Launch(Vector3 direction)
    {
        Vector3 launchDirection =
            Vector3.ProjectOnPlane(direction, Vector3.up);

        if (launchDirection.sqrMagnitude < 0.001f)
        {
            launchDirection = Vector3.forward;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.WakeUp();

        rb.AddForce(
            launchDirection.normalized * launchImpulse,
            ForceMode.Impulse
        );
    }

    private void FixedUpdate()
    {
        LimitSpeed();
    }

    private void LimitSpeed()
    {
        Vector3 velocity = rb.linearVelocity;

        Vector3 horizontalVelocity = new Vector3(
            velocity.x,
            0f,
            velocity.z
        );

        if (horizontalVelocity.sqrMagnitude > maxSpeed * maxSpeed)
        {
            horizontalVelocity =
                horizontalVelocity.normalized * maxSpeed;

            rb.linearVelocity = new Vector3(
                horizontalVelocity.x,
                velocity.y,
                horizontalVelocity.z
            );
        }
    }
}
