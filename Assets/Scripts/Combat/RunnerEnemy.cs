using UnityEngine;

[RequireComponent(typeof(EnemyBumper))]
public sealed class RunnerEnemy : MonoBehaviour
{
    [Min(0f)] [SerializeField] private float baseSpeedInDiameters = .8f;
    [Min(0f)] [SerializeField] private float speedPerLostSegment = .4f;
    private EnemyBumper enemy;
    public float Speed => enemy != null ? enemy.MoveSpeed : 0f;
    private void Awake() => enemy = GetComponent<EnemyBumper>();
    private void OnEnable() { enemy.HealthChanged += RefreshSpeed; RefreshSpeed(enemy); }
    private void RefreshSpeed(EnemyBumper target)
    {
        float d = target.Encounter != null ? target.Encounter.Placement.Diameter : 1f;
        target.SetMoveSpeed((baseSpeedInDiameters + (target.MaximumHitPoints - target.HitPoints) * speedPerLostSegment) * d);
    }
    private void OnDisable() { if (enemy != null) enemy.HealthChanged -= RefreshSpeed; }
}
