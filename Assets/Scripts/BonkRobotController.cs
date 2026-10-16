using UnityEngine;
using UnityEngine.InputSystem;

public class BonkRobotController : MonoBehaviour
{
    [SerializeField] private GameObject standingForm;
    [SerializeField] private GameObject ballForm;
    [SerializeField] private BouncingBallController ballController;
    [SerializeField] private bool startAsBall;
    
    [SerializeField] private InputActionReference switchFormAction;
    
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Vector3 cameraOffset =
        new Vector3(0f, 6f, -8f);

    [SerializeField] private float cameraSmoothTime = 0.2f;

    public bool IsBallForm { get; private set; }

    private Transform cameraTarget;
    private Vector3 cameraVelocity;

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (ballController == null && ballForm != null)
        {
            ballController = ballForm.GetComponent<BouncingBallController>();
        }

        IsBallForm = startAsBall;

        standingForm.SetActive(!IsBallForm);
        ballForm.SetActive(IsBallForm);
        
        cameraTarget = IsBallForm
            ? ballForm.transform
            : standingForm.transform;
    }

    private void OnEnable()
    {
        if (switchFormAction == null)
        {
            return;
        }

        switchFormAction.action.performed += OnSwitchForm;
        switchFormAction.action.Enable();
    }

    private void OnDisable()
    {
        if (switchFormAction == null)
        {
            return;
        }

        switchFormAction.action.performed -= OnSwitchForm;
        switchFormAction.action.Disable();
    }

    private void OnSwitchForm(
        InputAction.CallbackContext context)
    {
        ToggleForm();
    }

    public void ToggleForm()
    {
        GameObject currentForm =
            IsBallForm ? ballForm : standingForm;

        GameObject nextForm =
            IsBallForm ? standingForm : ballForm;
        
        Vector3 launchDirection = standingForm.transform.forward;
        
        nextForm.transform.position =
            currentForm.transform.position;
        
        currentForm.SetActive(false);
        nextForm.SetActive(true);

        IsBallForm = !IsBallForm;
        
        cameraTarget = nextForm.transform;
        
        if (IsBallForm && ballController != null)
        {
            ballController.Launch(launchDirection);
        }
    }

    private void LateUpdate()
    {
        FollowActiveForm();
    }

    private void FollowActiveForm()
    {
        if (cameraTransform == null ||
            cameraTarget == null)
        {
            return;
        }

        Vector3 desiredPosition =
            cameraTarget.position + cameraOffset;

        cameraTransform.position = Vector3.SmoothDamp(
            cameraTransform.position,
            desiredPosition,
            ref cameraVelocity,
            cameraSmoothTime
        );
        
    }
}
