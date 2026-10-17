using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class BonkRobotController : MonoBehaviour
{
    [Header("Phase 1")]
    [SerializeField] private BonkGameTuning tuning;

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
    public int CurrentHealth { get; private set; }
    public int MaxHealth => Tuning.playerHealth;
    public BonkGameTuning Tuning
    {
        get
        {
            if (tuning == null)
            {
                tuning = BonkGameTuning.CreateRuntimeDefaults();
            }

            return tuning;
        }
    }
    public Transform StandingTransform => standingForm != null ? standingForm.transform : null;
    public Transform BallTransform => ballForm != null ? ballForm.transform : null;
    public BouncingBallController BallController => ballController;

    private float nextDamageTime;

    private Transform cameraTarget;
    private Vector3 cameraVelocity;
    private Coroutine standingVelocityTransfer;

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

        if (standingController == null && standingForm != null)
        {
            standingController = standingForm.GetComponent<HadesPlayerController>();
        }

        CurrentHealth = Tuning.playerHealth;
        if (ballController != null)
        {
            ballController.Configure(Tuning);
        }
        if (standingController != null)
        {
            standingController.Configure(this);
        }

        IsBallForm = startAsBall;

        standingForm.SetActive(!IsBallForm);
        ballForm.SetActive(IsBallForm);
        
        cameraTarget = IsBallForm
            ? ballForm.transform
            : standingForm.transform;
    }

    public bool TryTakeDamage(Vector3 sourcePosition)
    {
        if (IsBallForm || Time.time < nextDamageTime || CurrentHealth <= 0)
        {
            return false;
        }

        CurrentHealth--;
        nextDamageTime = Time.time + Tuning.damageInvulnerability;

        Vector3 away = Vector3.ProjectOnPlane(
            standingForm.transform.position - sourcePosition,
            Vector3.up).normalized;
        if (away.sqrMagnitude < 0.01f)
        {
            away = -standingForm.transform.forward;
        }

        if (standingController != null)
        {
            standingController.ApplyKnockback(away * Tuning.damageKnockback);
        }

        if (CurrentHealth <= 0)
        {
            RestartPlayer();
        }

        return true;
    }

    private void RestartPlayer()
    {
        CurrentHealth = Tuning.playerHealth;
        nextDamageTime = Time.time + Tuning.damageInvulnerability;
        if (IsBallForm)
        {
            ToggleForm();
        }
        standingForm.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        standingController.EnterStandingMode(Vector3.zero);
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

            standingForm.transform.position = ballForm.transform.position;
            if (ballVelocity.sqrMagnitude > 0.01f)
            {
                standingForm.transform.rotation =
                    Quaternion.LookRotation(ballVelocity.normalized);
            }

            if (ballController != null)
            {
                ballController.ExitBallMode();
            }

            ballForm.SetActive(false);
            standingForm.SetActive(true);
            IsBallForm = false;
            if (standingController != null)
            {
                standingController.EnterStandingMode(ballVelocity);
                if (standingVelocityTransfer != null)
                {
                    StopCoroutine(standingVelocityTransfer);
                }
                standingVelocityTransfer = StartCoroutine(
                    ReapplyStandingVelocityNextFrame(ballVelocity));
            }
            cameraTarget = standingForm.transform;
        }
    }

    private IEnumerator ReapplyStandingVelocityNextFrame(Vector3 velocity)
    {
        yield return null;

        if (!IsBallForm && standingController != null)
        {
            standingController.EnterStandingMode(velocity);
        }

        standingVelocityTransfer = null;
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
