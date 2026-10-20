using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Renderer))]
public class YellowSpeedTile : MonoBehaviour
{
    [Min(0f)]
    [SerializeField] private float speedBoost = 4f;
    [Min(0f)]
    [SerializeField] private float cooldown = 0.75f;
    [SerializeField] private Color readyColor = new Color(1f, 0.78f, 0.04f);
    [SerializeField] private Color usedColor = new Color(0.35f, 0.27f, 0.03f);

    private Renderer tileRenderer;
    private MaterialPropertyBlock properties;
    private float nextReadyTime;

    private void Awake()
    {
        tileRenderer = GetComponent<Renderer>();
        properties = new MaterialPropertyBlock();
        GetComponent<BoxCollider>().isTrigger = true;
        SetColor(readyColor);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time < nextReadyTime ||
            !other.TryGetComponent(out BouncingBallController ball) ||
            !ball.isActiveAndEnabled)
        {
            return;
        }

        nextReadyTime = Time.time + cooldown;
        ball.AddSpeedBoost(speedBoost);
        StartCoroutine(ShowCooldown());
    }

    private IEnumerator ShowCooldown()
    {
        SetColor(usedColor);
        yield return new WaitForSeconds(cooldown);
        SetColor(readyColor);
    }

    private void SetColor(Color color)
    {
        tileRenderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        properties.SetColor("_EmissionColor", color * 0.35f);
        tileRenderer.SetPropertyBlock(properties);
    }
}
