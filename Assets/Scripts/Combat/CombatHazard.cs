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
    private EnemyBumper shooter;
    private float blastStarted;
    [Range(0f,1f)] [SerializeField] private float blastVolume = .2f;
    private AudioClip blastClip;
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
        radiusInDiameters = mine ? 1.5f : 2.75f;
        visual = model;
    }

    public void Initialize(EnemyWaveController owner, Vector3 origin, EnemyBumper shellOwner = null)
    {
        run = owner; shooter = shellOwner;
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
            if (marker != null)
            {
                float fraction = Mathf.Clamp01((Time.time - blastStarted) / .45f);
                marker.transform.localScale = Vector3.one * Mathf.Lerp(.45f, 1f, fraction);
                var line = marker.GetComponent<LineRenderer>();
                Color color = isMine ? new Color(1f, .45f, .05f, 1f-fraction) : new Color(1f, .2f, .5f, 1f-fraction);
                line.startColor = line.endColor = color;
            }
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
        PlayBlastSound();
        if (InBlast()) run.Health.TryDamageFrom(transform.position, isMine ? "Mine blast" : "Mortar blast");
        if (!isMine && shooter != null && !shooter.IsDefeated &&
            Contains(shooter.transform.position, shooter.GetComponent<CapsuleCollider>().radius)) shooter.TakeOwnMortarHit();
        blastStarted = Time.time;
        blastUntil = Time.time + .45f;
        if (visual != null) visual.gameObject.SetActive(false);
        if (marker != null)
        {
            marker.transform.localScale = Vector3.one;
            LineRenderer ring = marker.GetComponent<LineRenderer>();
            if (ring != null) { ring.startColor = ring.endColor = Color.white; ring.widthMultiplier *= 3f; }
        }
    }
    private void PlayBlastSound()
    {
        if (blastVolume <= 0) return;
        var source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = .6f;
        source.minDistance = 4; source.maxDistance = 35;
        var samples = new float[6615];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i/44100f, fade = 1f-(float)i/samples.Length;
            samples[i] = (Mathf.Sin(t*Mathf.PI*2*(isMine ? 110 : 75)) + .25f*Mathf.Sin(i*12.9898f))*.6f*fade*fade;
        }
        blastClip = AudioClip.Create("Blast feedback", samples.Length, 1, 44100, false); blastClip.SetData(samples,0);
        source.PlayOneShot(blastClip,blastVolume);
    }
    private void OnDestroy() { if (marker != null) Destroy(marker); if (blastClip != null) Destroy(blastClip); }
}
