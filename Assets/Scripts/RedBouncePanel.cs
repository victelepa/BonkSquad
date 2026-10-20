using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class RedBouncePanel : MonoBehaviour
{
    [Min(0f)]
    [SerializeField] private float speedBoost = 7f;
    [Min(0f)]
    [SerializeField] private float cooldown = 0.35f;
    [SerializeField] private Color readyColor = new Color(1f, 0.05f, 0.02f);
    [SerializeField] private Color cooldownColor = new Color(0.35f, 0.03f, 0.02f);

    private float nextReadyTime;
    private Renderer[] panelRenderers;
    private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

    private void Awake()
    {
        panelRenderers = GetComponentsInChildren<Renderer>();
        SetColor(readyColor);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (Time.time < nextReadyTime || collision.rigidbody == null)
        {
            return;
        }

        if (!collision.rigidbody.TryGetComponent(out BouncingBallController ball) ||
            !ball.isActiveAndEnabled)
        {
            return;
        }

        Vector3 surfaceNormal = collision.contactCount > 0
            ? collision.GetContact(0).normal
            : -ball.PlanarVelocity.normalized;
        nextReadyTime = Time.time + cooldown;
        StartCoroutine(BoostAfterPhysics(ball, surfaceNormal));
        StartCoroutine(ShowCooldown());
    }

    private IEnumerator BoostAfterPhysics(
        BouncingBallController ball,
        Vector3 surfaceNormal)
    {
        yield return new WaitForFixedUpdate();
        if (ball != null && ball.isActiveAndEnabled)
        {
            ball.ApplyBoostedRicochet(surfaceNormal, speedBoost);
        }
    }

    private IEnumerator ShowCooldown()
    {
        SetColor(cooldownColor);
        yield return new WaitForSeconds(cooldown);
        SetColor(readyColor);
    }

    private void SetColor(Color color)
    {
        foreach (Renderer panelRenderer in panelRenderers)
        {
            panelRenderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            panelRenderer.SetPropertyBlock(properties);
        }
    }
}
