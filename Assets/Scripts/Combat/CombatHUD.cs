using UnityEngine;

// Uses IMGUI for the prototype controls; leaves the existing speed/dizziness Canvas untouched.
[RequireComponent(typeof(EnemyWaveController))]
public sealed class CombatHUD : MonoBehaviour
{
    private EnemyWaveController run;
    [SerializeField] private bool showMinimap = true;
    private GUIStyle title, text, button, mapLabel, markerLabel, combo;
    private Collider[] arenaGeometry;
    private void Awake() => run = GetComponent<EnemyWaveController>();
    private void OnGUI()
    {
        if (run == null || run.Player == null) return;
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, wordWrap = true };
            text = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            combo = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            mapLabel = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            markerLabel = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.black } };
        }
        Matrix4x4 previous = GUI.matrix;
        Color previousColor = GUI.color;
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
        DrawDamageFlash();
        GUI.Box(new Rect(14, 14, 335, 145), "");
        GUI.Label(new Rect(28, 22, 310, 32), $"Wave {run.WaveIndex + 1}/{run.WaveCount} · {run.CurrentWave.title}", title);
        GUI.color = new Color(.12f, .14f, .17f);
        GUI.DrawTexture(new Rect(28, 65, 260, 22), Texture2D.whiteTexture);
        GUI.color = Time.time - run.Health.LastHitTime < .2f ? Color.white : new Color(.2f, .85f, .5f);
        GUI.DrawTexture(new Rect(28, 65, 260 * run.Health.Current / run.Health.Maximum, 22), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(28, 89, 300, 28), $"Health {run.Health.Current:0}/{run.Health.Maximum:0}", text);
        GUI.Label(new Rect(28, 119, 310, 30), $"Time {Mathf.CeilToInt(run.TimeRemaining)}s     Enemies {run.LivingEnemies}", text);
        PinballScore score = run.Scoring;
        GUI.Box(new Rect(14, 166, 335, 132), "");
        GUI.Label(new Rect(28, 172, 310, 28), $"Score {score.Points}   Impact ×{score.ImpactCharges}", text);
        GUI.color = new Color(.12f, .14f, .17f);
        GUI.DrawTexture(new Rect(28, 207, 285, 10), Texture2D.whiteTexture);
        GUI.color = new Color(.8f, .4f, 1f);
        GUI.DrawTexture(new Rect(28, 207, 285f * score.Progress / score.ChargeThreshold, 10), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(28, 220, 310, 28), $"{score.Progress}/{score.ChargeThreshold} to next charge", text);
        if (Time.time <= score.FeedbackUntil)
            GUI.Label(new Rect(28, 248, 310, 48), score.Feedback, text);
        if (run.IsCombatActive) DrawCombo(score);
        if (showMinimap) DrawMinimap();
        if (!run.IsCombatActive)
        {
            GUI.Box(new Rect(360, 205, 560, 370), "");
            string heading = run.State == EnemyWaveState.Introduction ? run.CurrentWave.title :
                run.State == EnemyWaveState.Victory ? "All waves cleared!" : run.ResultMessage;
            GUI.Label(new Rect(390, 230, 500, 60), heading, title);
            string description = run.State == EnemyWaveState.Introduction ? run.CurrentWave.introduction +
                "\nMinion contact hurts robot only; explosions hurt both. Chain kills within 3s or ricochet for bonuses. 100 points = a double-damage Impact Charge." :
                run.State == EnemyWaveState.Failed ? "Retry this wave with full health and new enemy positions." :
                run.State == EnemyWaveState.Cleared ? "Next wave introduces a new enemy. Your remaining health carries over." :
                "Spawners, mine layers, and mortars defeated.";
            GUI.Label(new Rect(390, 295, 500, 155), description, text);
            if (run.State == EnemyWaveState.Introduction && run.WaveIndex < run.WaveCount - 1 &&
                GUI.Button(new Rect(490, 520, 300, 40), "Try All Enemy Types", button)) run.TryAllEnemyTypes();
            if (run.State == EnemyWaveState.Introduction && GUI.Button(new Rect(490, 465, 300, 44), "Start Wave", button)) run.StartWave();
            else if (run.State == EnemyWaveState.Cleared && GUI.Button(new Rect(490, 465, 300, 44), "Next Wave", button)) run.NextWave();
            else if (run.State == EnemyWaveState.Failed && GUI.Button(new Rect(490, 465, 300, 44), "Retry Wave", button)) run.RetryWave();
            else if (run.State == EnemyWaveState.Victory && GUI.Button(new Rect(490, 465, 300, 44), "Restart Run", button)) run.RestartRun();
        }
        GUI.color = previousColor;
        GUI.matrix = previous;
    }

    private void DrawCombo(PinballScore score)
    {
        if (!score.FeedbackIsReward || string.IsNullOrEmpty(score.Feedback) || Time.time > score.FeedbackUntil) return;
        float age = Mathf.Max(0f, Time.time - score.FeedbackStartedAt);
        float fade = Mathf.Clamp01((score.FeedbackUntil - Time.time) / .35f);
        float pop = 1f + .14f * Mathf.Sin(Mathf.Clamp01(age / .35f) * Mathf.PI);
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), new Vector2(800, 88));
        Rect banner = new Rect(420, 42, 760, 92);
        // A bright rim and dark backing keep the announcement readable over the arena.
        GUI.color = new Color(1f, .8f, .12f, .85f * fade);
        GUI.DrawTexture(banner, Texture2D.whiteTexture);
        GUI.color = new Color(.08f, .045f, .12f, .93f * fade);
        GUI.DrawTexture(new Rect(423, 45, 754, 86), Texture2D.whiteTexture);
        combo.normal.textColor = new Color(.02f, .01f, .03f, fade);
        GUI.color = Color.white;
        GUI.Label(new Rect(438, 51, 728, 80), score.Feedback, combo);
        combo.normal.textColor = Color.Lerp(new Color(1f, .86f, .2f, fade), new Color(1f, 1f, 1f, fade), Mathf.Clamp01(1f - age / .22f));
        GUI.Label(new Rect(436, 49, 728, 80), score.Feedback, combo);
        GUI.color = previousColor;
        GUI.matrix = previousMatrix;
    }

    private void DrawDamageFlash()
    {
        PlayerDamageFeedback feedback = run.Health.GetComponent<PlayerDamageFeedback>();
        float intensity = feedback != null ? feedback.Intensity : 0f;
        if (intensity <= 0f) return;
        Color previous = GUI.color;
        GUI.color = new Color(1f, .08f, .05f, .08f * intensity);
        GUI.DrawTexture(new Rect(0, 0, 1280, 720), Texture2D.whiteTexture);
        GUI.color = new Color(1f, .08f, .05f, .55f * intensity);
        GUI.DrawTexture(new Rect(0, 0, 1280, 24), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, 696, 1280, 24), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, 24, 24, 672), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(1256, 24, 24, 672), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    // North stays at the top; independent of the follow camera and active player form.
    public static Vector2 WorldToMinimap(Vector3 position, Bounds floorBounds, Rect map)
    {
        return new Vector2(map.x + Mathf.InverseLerp(floorBounds.min.x, floorBounds.max.x, position.x) * map.width,
            map.y + (1f - Mathf.InverseLerp(floorBounds.min.z, floorBounds.max.z, position.z)) * map.height);
    }

    private void DrawMinimap()
    {
        if (run.Placement == null || run.Placement.Floor == null) return;
        Rect panel = new Rect(1016, 404, 250, 302);
        Rect map = new Rect(1035, 437, 212, 212);
        Bounds floor = run.Placement.Floor.bounds;
        GUI.color = new Color(.06f, .08f, .11f, .95f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(1030, 410, 226, 24), "ARENA                         N ↑", mapLabel);
        GUI.color = new Color(.16f, .2f, .24f);
        GUI.DrawTexture(map, Texture2D.whiteTexture);
        if (arenaGeometry == null)
            arenaGeometry = run.Placement.Floor.transform.parent.GetComponentsInChildren<Collider>();
        GUI.color = new Color(.4f, .45f, .5f);
        foreach (Collider solid in arenaGeometry)
        {
            if (solid == null || solid == run.Placement.Floor || solid.isTrigger || !solid.enabled) continue;
            Vector2 topLeft = WorldToMinimap(new Vector3(solid.bounds.min.x, 0, solid.bounds.max.z), floor, map);
            Vector2 bottomRight = WorldToMinimap(new Vector3(solid.bounds.max.x, 0, solid.bounds.min.z), floor, map);
            GUI.DrawTexture(Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y), Texture2D.whiteTexture);
        }
        foreach (EnemyBumper enemy in run.LivingEnemyObjects)
        {
            if (enemy == null || enemy.IsDefeated || !enemy.gameObject.activeInHierarchy) continue;
            string symbol;
            Color color;
            if (enemy.TryGetComponent<SpawnerEnemy>(out _)) { symbol = "S"; color = Color.green; }
            else if (enemy.TryGetComponent<MineLayerEnemy>(out _)) { symbol = "L"; color = new Color(1f, .5f, .05f); }
            else if (enemy.TryGetComponent<MortarEnemy>(out _)) { symbol = "M"; color = new Color(.8f, .4f, 1f); }
            else { symbol = ""; color = Color.yellow; }
            DrawMapMarker(WorldToMinimap(enemy.transform.position, floor, map), color, symbol, symbol == "" ? 5f : 12f);
        }
        DrawMapMarker(WorldToMinimap(run.Player.ActivePosition, floor, map), Color.white, "P", 14f);
        GUI.color = Color.white;
        GUI.Label(new Rect(1030, 651, 226, 20), "P Player     • Minion (yellow)", mapLabel);
        GUI.color = Color.green;
        GUI.Label(new Rect(1030, 674, 70, 20), "S Spawn", mapLabel);
        GUI.color = new Color(1f, .5f, .05f);
        GUI.Label(new Rect(1104, 674, 72, 20), "L Mines", mapLabel);
        GUI.color = new Color(.8f, .4f, 1f);
        GUI.Label(new Rect(1180, 674, 76, 20), "M Mortar", mapLabel);
        GUI.color = Color.white;
    }

    private void DrawMapMarker(Vector2 point, Color color, string symbol, float size)
    {
        Rect rect = new Rect(point.x - size / 2f, point.y - size / 2f, size, size);
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(rect.x - 1, rect.y - 1, size + 2, size + 2), Texture2D.whiteTexture);
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (symbol.Length > 0) GUI.Label(rect, symbol, markerLabel);
    }

}
