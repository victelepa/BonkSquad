using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PowerupType
{
    Speed,
    Health
}

public class PowerupPickup : MonoBehaviour
{
    private PowerupType type;
    private float amount;
    private bool collected;

    public void Configure(PowerupType powerupType, float value)
    {
        type = powerupType;
        amount = value;
        name = type == PowerupType.Speed ? "Speed Powerup" : "Health Powerup";
        ApplyAppearance();
    }

    private void Update()
    {
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        transform.position += Vector3.up *
            (Mathf.Sin(Time.time * 3f + transform.position.x) * 0.002f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
        {
            return;
        }

        BonkRobotController robot = other.GetComponentInParent<BonkRobotController>();
        if (robot == null)
        {
            return;
        }

        collected = true;
        if (type == PowerupType.Speed)
        {
            robot.GrantSpeedBoost(amount);
        }
        else
        {
            robot.Heal(Mathf.RoundToInt(amount));
        }

        Destroy(gameObject);
    }

    private void ApplyAppearance()
    {
        Renderer pickupRenderer = GetComponent<Renderer>();
        if (pickupRenderer == null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }
        Material material = new Material(shader);
        Color color = type == PowerupType.Speed
            ? new Color(0.05f, 0.9f, 1f)
            : new Color(0.1f, 1f, 0.25f);
        material.color = color;
        material.SetColor("_BaseColor", color);
        pickupRenderer.material = material;
    }
}

public class PowerupSpawner : MonoBehaviour
{
    private readonly List<PowerupPickup> activePowerups = new List<PowerupPickup>();
    private BonkRobotController robot;
    private BonkGameTuning tuning;
    private int spawnIndex;

    private static readonly Vector3[] SpawnPoints =
    {
        new Vector3(-16f, 1.1f, -14f),
        new Vector3(15f, 1.1f, -13f),
        new Vector3(-14f, 1.1f, 14f),
        new Vector3(15f, 1.1f, 14f),
        new Vector3(0f, 1.1f, -16f),
        new Vector3(0f, 1.1f, 16f),
    };

    private IEnumerator Start()
    {
        robot = FindAnyObjectByType<BonkRobotController>();
        tuning = robot != null ? robot.Tuning : BonkGameTuning.CreateRuntimeDefaults();

        yield return new WaitForSeconds(2f);
        while (true)
        {
            activePowerups.RemoveAll(powerup => powerup == null);
            if (activePowerups.Count < tuning.maximumActivePowerups)
            {
                SpawnPowerup();
            }

            yield return new WaitForSeconds(Random.Range(
                tuning.powerupMinimumDelay,
                Mathf.Max(tuning.powerupMinimumDelay, tuning.powerupMaximumDelay)));
        }
    }

    private void SpawnPowerup()
    {
        PowerupType type = spawnIndex % 3 == 2
            ? PowerupType.Health
            : PowerupType.Speed;
        Vector3 position = SpawnPoints[spawnIndex % SpawnPoints.Length];
        spawnIndex++;

        GameObject pickupObject = GameObject.CreatePrimitive(
            type == PowerupType.Speed ? PrimitiveType.Capsule : PrimitiveType.Sphere);
        pickupObject.transform.SetParent(transform, false);
        pickupObject.transform.position = position;
        pickupObject.transform.localScale = Vector3.one * 0.75f;

        Collider pickupCollider = pickupObject.GetComponent<Collider>();
        pickupCollider.isTrigger = true;
        PowerupPickup pickup = pickupObject.AddComponent<PowerupPickup>();
        pickup.Configure(type, type == PowerupType.Speed ? tuning.speedPowerupBoost : 1f);
        activePowerups.Add(pickup);
    }
}
