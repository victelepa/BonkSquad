using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerSpeedTrailController : MonoBehaviour
{
    [Header("Speed Source")]
    [SerializeField] private BouncingBallController player;

    [Header("Trail 实例根物体")]
    [SerializeField] private GameObject lowSpeedTrail;
    [SerializeField] private GameObject mediumSpeedTrail;
    [SerializeField] private GameObject highSpeedTrail;

    [Header("Emission")]
    [Min(0f)]
    [SerializeField] private float minimumEmissionSpeed = 0.05f;

    private TrailRenderer[][] trailGroups;
    
    private int activeTier = -1;

    private void Awake()
    {
        if (player == null)
            player = GetComponentInParent<BouncingBallController>();

        trailGroups = new TrailRenderer[][]
        {
            FindTrails(lowSpeedTrail),
            FindTrails(mediumSpeedTrail),
            FindTrails(highSpeedTrail)
        };
    }

    private void OnEnable()
    {
        ResetTrails();
    }

    private void LateUpdate()
    {
        if (player == null || !player.isActiveAndEnabled)
        {
            SwitchTier(-1);
            return;
        }

        float speed = player.PlanarVelocity.magnitude;

        if (speed <= minimumEmissionSpeed)
        {
            SwitchTier(-1);
            return;
        }

        PlayerSpeedSettings settings = player.SpeedSettings;

        int tier = speed >= settings.LargeEnemyKillSpeed ? 2
                 : speed >= settings.SmallEnemyKillSpeed ? 1
                 : 0;

        SwitchTier(tier);
    }

    private void SwitchTier(int nextTier)
    {
        if (activeTier == nextTier)
            return;
        
        if (activeTier >= 0)
            SetGroupEmitting(activeTier, false);

        activeTier = nextTier;

        if (activeTier >= 0)
            SetGroupEmitting(activeTier, true);
    }

    private void SetGroupEmitting(int tier, bool emitting)
    {
        foreach (TrailRenderer trail in trailGroups[tier])
        {
            if (trail != null)
                trail.emitting = emitting;
        }
    }

    private void OnDisable()
    {
        ResetTrails();
    }

    private void ResetTrails()
    {
        activeTier = -1;

        if (trailGroups == null)
            return;

        foreach (TrailRenderer[] group in trailGroups)
        {
            foreach (TrailRenderer trail in group)
            {
                if (trail == null)
                    continue;

                trail.emitting = false;
                trail.Clear();
            }
        }
    }

    private static TrailRenderer[] FindTrails(GameObject root)
    {
        return root != null
            ? root.GetComponentsInChildren<TrailRenderer>(true)
            : new TrailRenderer[0];
    }
}