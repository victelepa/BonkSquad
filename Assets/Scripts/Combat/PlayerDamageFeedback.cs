using System.Collections.Generic;
using UnityEngine;

// Property blocks tint both forms without allocating materials or changing shared assets.
public sealed class PlayerDamageFeedback : MonoBehaviour
{
    [Min(.05f)] [SerializeField] private float flashSeconds = .35f;
    [SerializeField] private Color hitColor = new Color(1f, .12f, .08f, 1f);
    private PlayerHealth health;
    private float flashUntil = float.NegativeInfinity;
    private bool applied;
    private readonly List<Surface> surfaces = new List<Surface>();
    public float Intensity => Mathf.Clamp01((flashUntil - Time.unscaledTime) / Mathf.Max(.05f, flashSeconds));
    public bool IsFlashing => Intensity > 0f;

    private sealed class Surface
    {
        public Renderer renderer;
        public int slot, property;
        public Color color;
        public MaterialPropertyBlock original = new MaterialPropertyBlock();
        public MaterialPropertyBlock working = new MaterialPropertyBlock();
    }

    private void Awake()
    {
        int baseColor = Shader.PropertyToID("_BaseColor"), legacyColor = Shader.PropertyToID("_Color");
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                Material material = materials[slot];
                if (material == null) continue;
                int property = material.HasProperty(baseColor) ? baseColor : material.HasProperty(legacyColor) ? legacyColor : -1;
                if (property < 0) continue;
                surfaces.Add(new Surface { renderer = renderer, slot = slot, property = property, color = material.GetColor(property) });
            }
        }
    }
    private void OnEnable()
    {
        health = GetComponent<PlayerHealth>();
        if (health != null) health.Damaged += Flash;
    }
    private void Flash()
    {
        if (!applied)
        {
            foreach (Surface surface in surfaces)
            {
                if (surface.renderer == null) continue;
                surface.renderer.GetPropertyBlock(surface.original, surface.slot);
                Material material = surface.renderer.sharedMaterials[surface.slot];
                surface.color = surface.original.HasProperty(surface.property)
                    ? surface.original.GetColor(surface.property) : material.GetColor(surface.property);
            }
        }
        applied = true;
        flashUntil = Time.unscaledTime + Mathf.Max(.05f, flashSeconds);
        ApplyTint(1f);
    }
    private void LateUpdate()
    {
        if (!applied) return;
        if (!IsFlashing) { Clear(); return; }
        ApplyTint(Intensity);
    }
    private void ApplyTint(float amount)
    {
        foreach (Surface surface in surfaces)
        {
            if (surface.renderer == null) continue;
            surface.renderer.GetPropertyBlock(surface.working, surface.slot);
            surface.working.SetColor(surface.property, Color.Lerp(surface.color, hitColor, amount));
            surface.renderer.SetPropertyBlock(surface.working, surface.slot);
        }
    }
    public void Clear()
    {
        flashUntil = float.NegativeInfinity;
        if (!applied) return;
        foreach (Surface surface in surfaces)
            if (surface.renderer != null) surface.renderer.SetPropertyBlock(surface.original, surface.slot);
        applied = false;
    }
    private void OnDisable()
    {
        if (health != null) health.Damaged -= Flash;
        Clear();
    }
}
