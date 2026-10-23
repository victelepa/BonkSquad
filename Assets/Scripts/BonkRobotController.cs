using UnityEngine;
using UnityEngine.InputSystem;

public class BonkRobotController : MonoBehaviour
{
    [Header("Speed System (shared by both forms and HUD)")]
    [SerializeField] private PlayerSpeedSettings speedSettings = new PlayerSpeedSettings();
    public PlayerSpeedSettings SpeedSettings => speedSettings;
    public Vector3 CurrentPlanarVelocity => IsBallForm
        ? (ballController != null ? ballController.PlanarVelocity : Vector3.zero)
        : (standingController != null ? standingController.CurrentPlanarVelocity : Vector3.zero);
    public float CurrentSpeed => CurrentPlanarVelocity.magnitude;

    [Header("Forms")]
    [SerializeField] private GameObject standingForm;
    [SerializeField] private GameObject ballForm;
    [SerializeField] private BouncingBallController ballController;
    [SerializeField] private HadesPlayerController standingController;
    [Tooltip("Start the scene in ball form instead of normal form.")]
    [SerializeField] private bool startAsBall;

    [Header("Input")]
    [Tooltip("Input action used to switch between normal and ball forms.")]
    [SerializeField] private InputActionReference switchFormAction;

    [Header("Camera Follow")]
    [SerializeField] private Transform cameraTransform;
    [Tooltip("Camera offset from the currently active form.")]
    [SerializeField] private Vector3 cameraOffset =
        new Vector3(0f, 6f, -8f);

    [Tooltip("Camera follow smoothing time. Lower values respond faster.")]
    [Range(0.01f, 1f)]
    [SerializeField] private float cameraSmoothTime = 0.2f;

    public bool IsBallForm { get; private set; }

    private Transform cameraTarget;
    private Vector3 cameraVelocity;
    private void OnValidate()
    {
        if (speedSettings == null) speedSettings = new PlayerSpeedSettings();
        speedSettings.Validate();
    }

    private void Awake()
    {
        OnValidate();
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (ballController == null && ballForm != null)
        {
            ballController = ballForm.GetComponent<BouncingBallController>();
        }

        if (standingController == null && standingForm != null)
        {
            standingController = standingForm.GetComponent<HadesPlayerController>();
        }

        if (standingForm == null || ballForm == null ||
            ballController == null || standingController == null)
        {
            Debug.LogError("BonkRobotController requires both forms and their movement controllers.", this);
            enabled = false;
            return;
        }

        ballController.Configure(this);
        standingController.Configure(this);
        IsBallForm = startAsBall;

        standingForm.SetActive(!IsBallForm);
        ballForm.SetActive(IsBallForm);
        
        if (IsBallForm) ballController.EnterBallMode(Vector3.zero);
        else standingController.EnterStandingMode(Vector3.zero);

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
        if (!isActiveAndEnabled) return;
        if (!IsBallForm)
        {
            Vector3 inheritedVelocity = standingController != null
                ? standingController.CurrentPlanarVelocity
                : Vector3.zero;

            ballForm.transform.SetPositionAndRotation(
                standingForm.transform.position,
                standingForm.transform.rotation);

            standingForm.SetActive(false);
            ballForm.SetActive(true);
            IsBallForm = true;
            cameraTarget = ballForm.transform;

            if (ballController != null)
            {
                ballController.EnterBallMode(inheritedVelocity);
            }
        }
        else
        {
            Vector3 ballVelocity = ballController != null
                ? ballController.PlanarVelocity
                : Vector3.zero;

            Vector3 standingPosition = ballForm.transform.position;
            Quaternion standingRotation = ballVelocity.sqrMagnitude > 0f
                ? Quaternion.LookRotation(ballVelocity.normalized)
                : ballForm.transform.rotation;

            if (ballController != null)
            {
                ballController.ExitBallMode();
            }

            ballForm.SetActive(false);
            standingForm.SetActive(true);
            IsBallForm = false;
            if (standingController != null)
            {
                standingController.Teleport(standingPosition, standingRotation);
                standingController.EnterStandingMode(ballVelocity);
            }
            cameraTarget = standingForm.transform;
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
