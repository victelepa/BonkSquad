using UnityEngine;

[RequireComponent(typeof(EnemyBumper))]
public sealed class RunnerEnemy : MonoBehaviour
{
    [Min(0f)] [SerializeField] private float baseSpeedInDiameters = 1.25f;
    [Min(0f)] [SerializeField] private float speedPerLostSegment = 1f;
    private EnemyBumper enemy;
    private int observedHitPoints = -1;
    public float Speed => enemy != null ? enemy.MoveSpeed : 0f;
    private void Awake() => enemy = GetComponent<EnemyBumper>();
    private void OnEnable()
    {
        if (enemy == null) enemy = GetComponent<EnemyBumper>();
        enemy.HealthChanged -= RefreshSpeed;
        enemy.HealthChanged += RefreshSpeed;
        RefreshSpeed(enemy);
    }
    private void RefreshSpeed(EnemyBumper target)
    {
        float d = target.Encounter != null ? target.Encounter.Placement.Diameter : 1f;
        target.SetMoveSpeed((baseSpeedInDiameters + (target.MaximumHitPoints - target.HitPoints) * speedPerLostSegment) * d);
        observedHitPoints = target.HitPoints;
    }
    internal void RefreshForEncounter()
    {
        if (enemy == null) enemy = GetComponent<EnemyBumper>();
        RefreshSpeed(enemy);
    }
    private void LateUpdate()
    {
        // Also observe hit points directly so prefab activation order or a missed
        // event can never leave a damaged runner at its base speed.
        if (enemy != null && !enemy.IsDefeated && enemy.HitPoints != observedHitPoints)
            RefreshSpeed(enemy);
    }
    private void OnDisable() { if (enemy != null) enemy.HealthChanged -= RefreshSpeed; }
}
