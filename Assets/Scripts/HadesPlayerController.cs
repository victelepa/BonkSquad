using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class HadesPlayerController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float turnSpeed = 14f;
    [SerializeField] Vector3 cameraOffset = new Vector3(0f, 10f, -10f);
    [SerializeField] Vector3 cameraRotation = new Vector3(40f, 0f, 0f);

    CharacterController controller;
    Transform cameraTransform;
    float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        Vector2 input = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            input.x = (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed ? 1f : 0f)
                    - (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed ? 1f : 0f);
            input.y = (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed ? 1f : 0f)
                    - (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed ? 1f : 0f);
        }
#else
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif

        Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 move = forward * input.y + right * input.x;
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += Physics.gravity.y * Time.deltaTime;

        controller.Move((move * moveSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);

        if (move.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
    }

    void LateUpdate()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (cameraTransform == null)
            return;

        cameraTransform.SetPositionAndRotation(
            transform.position + cameraOffset,
            Quaternion.Euler(cameraRotation));
    }
}
