using UnityEngine;

// One instance owns one explosion; damage is attempted only once, never once per form collider.
public sealed class CombatHazard : MonoBehaviour
{
    [SerializeField] private bool isMine;
    [Min(0f)] [SerializeField] private float armSeconds = 1.5f;
    [Min(1f)] [SerializeField] private float lifetime = 12f;
    [Min(.1f)] [SerializeField] private float flightSeconds = 2f;
    [Min(.1f)] [SerializeField] private float radiusInDiameters = 1.5f;
    [SerializeField] private Transform visual;
    private EnemyWaveController run;
    private Vector3 launchPoint;
    private float createdAt;
    private float blastUntil;
    private bool exploded;
    private GameObject marker;
    public string WarningLabel => exploded ? "! BLAST" : !isMine ? "! MORTAR — MOVE" :
        Time.time - createdAt < armSeconds ? "! MINE ARMING" : "! ARMED MINE";
    public float Radius => radiusInDiameters * (run != null ? run.Placement.Diameter : .8f);
    public bool Contains(Vector3 point, float clearance) =>
        PlayerMovementMath.Planar(point - transform.position).magnitude < Radius + clearance;

    public void ConfigurePrefab(bool mine, Transform model)
    {
        isMine = mine;
        radiusInDiameters = mine ? 1.5f : 2f;
        visual = model;
    }

    public void Initialize(EnemyWaveController owner, Vector3 origin)
    {
        run = owner;
        createdAt = Time.time;
        launchPoint = origin;
        marker = run.CreateMarker(isMine ? "Mine arming" : "Mortar landing zone", transform.position,
            Radius, isMine ? new Color(1f, .6f, .05f) : new Color(1f, .15f, .4f));
        if (!isMine && visual != null) visual.position = origin;
    }

    private void Update()
    {
        if (run == null || !run.IsCombatActive) return;
        if (exploded)
        {
            if (Time.time >= blastUntil) Destroy(gameObject);
            return;
        }
        float age = Time.time - createdAt;
        if (!isMine)
        {
            float fraction = Mathf.Clamp01(age / flightSeconds);
            if (visual != null)
                visual.position = Vector3.Lerp(launchPoint, transform.position, fraction) +
                    Vector3.up * Mathf.Sin(fraction * Mathf.PI) * run.Placement.Diameter * 6f;
            if (age >= flightSeconds) Explode();
        }
        else
        {
            if (marker != null) marker.transform.localScale = Vector3.one *
                (age < armSeconds ? .8f + .2f * Mathf.PingPong(age * 3f, 1f) : 1f);
            if (age >= lifetime) { Destroy(gameObject); return; }
            if (age >= armSeconds && InBlast()) Explode();
        }
    }

    private bool InBlast() => Contains(run.Player.ActivePosition, run.Placement.Diameter * .5f);
    private void Explode()
    {
        if (exploded) return;
        exploded = true;
        if (InBlast()) run.Health.TryDamageFrom(transform.position);
        blastUntil = Time.time + .25f;
        if (visual != null) visual.gameObject.SetActive(false);
        if (marker != null)
        {
            marker.transform.localScale = Vector3.one;
            LineRenderer ring = marker.GetComponent<LineRenderer>();
            if (ring != null) { ring.startColor = ring.endColor = Color.white; ring.widthMultiplier *= 3f; }
        }
    }
    private void OnDestroy() { if (marker != null) Destroy(marker); }
}
