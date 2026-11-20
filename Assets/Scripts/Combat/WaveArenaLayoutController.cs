using System;
using UnityEngine;

public sealed class WaveArenaLayoutController : MonoBehaviour
{
    [Serializable]
    public struct BlockPose
    {
        public Vector2 positionInDiameters;
        public Vector2 sizeInDiameters;
        public float yaw;
        public BlockPose(float x, float z, float width, float depth, float rotation = 0f)
        {
            positionInDiameters = new Vector2(x, z);
            sizeInDiameters = new Vector2(width, depth);
            yaw = rotation;
        }
    }

    [Serializable]
    public sealed class ArenaLayout
    {
        public string name;
        public BlockPose[] blocks = Array.Empty<BlockPose>();
    }

    [SerializeField] private EnemyWaveController run;
    [SerializeField] private Transform[] modularBlocks = Array.Empty<Transform>();
    [SerializeField] private ArenaLayout[] layouts = Array.Empty<ArenaLayout>();
    [Min(.5f)] [SerializeField] private float blockHeightInDiameters = 1.5f;
    public int LayoutCount => layouts?.Length ?? 0;
    public string CurrentLayoutName { get; private set; } = "Open arena";

    public void Configure(EnemyWaveController owner, Transform[] blocks, ArenaLayout[] definitions)
    {
        run = owner;
        modularBlocks = blocks;
        layouts = definitions;
    }

    public void ApplyLayout(int waveIndex)
    {
        if (run == null || run.Placement == null || layouts == null || layouts.Length == 0) return;
        ArenaLayout layout = layouts[Mathf.Abs(waveIndex) % layouts.Length];
        CurrentLayoutName = layout.name;
        float d = run.Placement.Diameter;
        Bounds floor = run.Placement.Floor.bounds;
        for (int i = 0; i < modularBlocks.Length; i++)
        {
            Transform block = modularBlocks[i];
            bool active = block != null && i < layout.blocks.Length;
            if (block != null) block.gameObject.SetActive(active);
            if (!active) continue;
            BlockPose pose = layout.blocks[i];
            block.position = new Vector3(floor.center.x + pose.positionInDiameters.x * d,
                floor.max.y + blockHeightInDiameters * d * .5f,
                floor.center.z + pose.positionInDiameters.y * d);
            block.rotation = Quaternion.Euler(0f, pose.yaw, 0f);
            block.localScale = new Vector3(pose.sizeInDiameters.x * d,
                blockHeightInDiameters * d, pose.sizeInDiameters.y * d);
        }
        Physics.SyncTransforms();
        run.NotifyObstacleMoved();
    }
}
