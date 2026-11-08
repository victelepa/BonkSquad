using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class RotatingPinballBat : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = 35f;
    [Min(0f)] [SerializeField] private float launchSpeed = 12f;
    [Range(0f, .5f)] [SerializeField] private float speedBonus = .15f;
    [Min(0f)] [SerializeField] private float cooldown = .4f;
    [SerializeField] private EnemyWaveController run;
    [Min(.1f)] [SerializeField] private float reservedRadius = 3.4f;
    public bool Reserves(Vector3 point, float clearance) => PlayerMovementMath.Planar(point - transform.position).magnitude < reservedRadius + clearance;
    public void SetReservedRadius(float radius) => reservedRadius = radius;
    private Rigidbody body;
    private float readyAt;
    private Quaternion startingRotation;
    private readonly HashSet<Collider> contacts = new HashSet<Collider>();
    public Vector3 LaunchDirection => transform.forward;
    public void Configure(EnemyWaveController owner) => run = owner;
    private void Awake()
    {
        body = GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
        startingRotation = transform.rotation;
    }
    private void FixedUpdate()
    {
        if (run == null || !run.IsCombatActive) return;
        body.MoveRotation(body.rotation * Quaternion.Euler(0, degreesPerSecond * Time.fixedDeltaTime, 0));
    }
    private void OnCollisionEnter(Collision collision) => TryStrike(collision.collider);
    private bool TryStrike(Collider contact)
    {
        if (!contacts.Add(contact) || Time.time < readyAt || run == null || !run.IsCombatActive) return false;
        var ball = contact.GetComponentInParent<BouncingBallController>();
        if (ball == null || !ball.LaunchFromBat(LaunchDirection, launchSpeed, speedBonus)) return false;
        readyAt = Time.time + cooldown; return true;
    }
    private void OnCollisionExit(Collision collision) => contacts.Remove(collision.collider);
    public void ResetForWave() { contacts.Clear(); readyAt = 0; transform.rotation = startingRotation; if (body != null) body.rotation = startingRotation; }
}
