using System.Collections.Generic;
using UnityEngine;

// Visual-only copies: prefab scripts, colliders and rigidbodies are never instantiated.
public sealed class EnemyModelPreview : System.IDisposable
{
    private readonly GameObject stage;
    private readonly RenderTexture texture;
    public RenderTexture Texture => texture;
    public EnemyModelPreview(EnemyBumper prefab, int slot)
    {
        stage = new GameObject("Enemy preview meshes"); stage.transform.position = new Vector3(10000 + slot*30, 10000, 10000);
        var paths = new Dictionary<Transform,Transform> { [prefab.transform] = stage.transform };
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            var original = filter.GetComponent<MeshRenderer>(); if (original == null) continue;
            Transform copy = CopyPath(filter.transform, paths); copy.gameObject.layer = 31;
            copy.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            copy.gameObject.AddComponent<MeshRenderer>().sharedMaterials = original.sharedMaterials;
        }
        var renderers = stage.GetComponentsInChildren<Renderer>(); var bounds = new Bounds(stage.transform.position, Vector3.one);
        if (renderers.Length > 0) { bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds); }
        var cameraObject = new GameObject("Preview camera", typeof(Camera)); cameraObject.transform.SetParent(stage.transform, true);
        var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = Mathf.Max(.6f, bounds.size.magnitude*.6f);
        camera.transform.position = bounds.center + new Vector3(3,2,-5).normalized*8; camera.transform.LookAt(bounds.center);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.04f,.07f,.12f);
        camera.cullingMask = 1<<31; camera.nearClipPlane = .1f; camera.farClipPlane = 18;
        texture = new RenderTexture(256,256,16); camera.targetTexture = texture;
        var lightObject = new GameObject("Preview light", typeof(Light)); lightObject.transform.SetParent(cameraObject.transform,false);
        var light = lightObject.GetComponent<Light>(); light.type = LightType.Directional; light.cullingMask = 1<<31; light.intensity = 1.5f;
    }
    private static Transform CopyPath(Transform original, Dictionary<Transform,Transform> paths)
    {
        if (paths.TryGetValue(original, out var copy)) return copy;
        var parent = CopyPath(original.parent, paths); copy = new GameObject(original.name).transform; copy.SetParent(parent,false);
        copy.localPosition = original.localPosition; copy.localRotation = original.localRotation; copy.localScale = original.localScale;
        paths.Add(original,copy); return copy;
    }
    public void Dispose() { if (stage != null) { stage.SetActive(false); Object.Destroy(stage); } if (texture != null) { texture.Release(); Object.Destroy(texture); } }
}
