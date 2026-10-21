using UnityEngine;

internal static class PhaseOneArenaBootstrap
{
    private static readonly string[] ExistingBoostObjects = { "Pillar A", "Pillar B" };
    private static readonly string[] ExistingWalls =
    {
        "North Wall", "South Wall", "East Wall", "West Wall", "Center Block"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BuildPrototypeArenaFeatures()
    {
        foreach (string objectName in ExistingBoostObjects)
        {
            GameObject boostObject = GameObject.Find(objectName);
            if (boostObject != null && boostObject.GetComponent<RedBouncePanel>() == null)
            {
                boostObject.AddComponent<RedBouncePanel>();
            }
        }

        foreach (string objectName in ExistingWalls)
        {
            GameObject wall = GameObject.Find(objectName);
            if (wall != null && wall.GetComponent<RedBouncePanel>() == null)
            {
                ConfigureNeutralBounce(wall);
            }
        }

        GameObject root = new GameObject("Phase 1 Arena Features");
        root.AddComponent<PowerupSpawner>();

        CreateBoostPoint(root.transform, "North Boost Point",
            new Vector3(0f, 1.25f, 23.25f), new Vector3(6f, 2.5f, 0.4f));
        CreateBoostPoint(root.transform, "South Boost Point",
            new Vector3(0f, 1.25f, -23.25f), new Vector3(6f, 2.5f, 0.4f));
        CreateBoostPoint(root.transform, "East Boost Point",
            new Vector3(23.25f, 1.25f, 0f), new Vector3(0.4f, 2.5f, 6f));
        CreateBoostPoint(root.transform, "West Boost Point",
            new Vector3(-23.25f, 1.25f, 0f), new Vector3(0.4f, 2.5f, 6f));

        CreateRamp(root.transform, "Launch Ramp A",
            new Vector3(-12f, 0.65f, 10f), new Vector3(5f, 0.75f, 7f), 10f);
        CreateRamp(root.transform, "Launch Ramp B",
            new Vector3(12f, 0.65f, -10f), new Vector3(5f, 0.75f, 7f), -10f);

        CreateObstacle(root.transform, "Angled Barrier A",
            new Vector3(-15f, 1f, 0f), new Vector3(5f, 2f, 0.8f), 35f);
        CreateObstacle(root.transform, "Angled Barrier B",
            new Vector3(15f, 1f, 5f), new Vector3(5f, 2f, 0.8f), -35f);

        CreateSpeedTile(root.transform, "Speed Tile West",
            new Vector3(-18f, 0.08f, -8f), 90f);
        CreateSpeedTile(root.transform, "Speed Tile South West",
            new Vector3(-10f, 0.08f, -17f), 25f);
        CreateSpeedTile(root.transform, "Speed Tile South East",
            new Vector3(10f, 0.08f, -17f), -25f);
        CreateSpeedTile(root.transform, "Speed Tile East",
            new Vector3(18f, 0.08f, 8f), 90f);
        CreateSpeedTile(root.transform, "Speed Tile North East",
            new Vector3(10f, 0.08f, 17f), 25f);
        CreateSpeedTile(root.transform, "Speed Tile North West",
            new Vector3(-10f, 0.08f, 17f), -25f);
    }

    private static void CreateBoostPoint(
        Transform parent, string name, Vector3 position, Vector3 scale)
    {
        GameObject boostPoint = CreateBlock(parent, name, position, scale, 0f);
        boostPoint.AddComponent<RedBouncePanel>();
    }

    private static void CreateRamp(
        Transform parent, string name, Vector3 position, Vector3 scale, float angle)
    {
        GameObject ramp = CreateBlock(parent, name, position, scale, 0f);
        ramp.transform.rotation = Quaternion.Euler(angle, 0f, 0f);
        SetBlockColor(ramp, new Color(0.12f, 0.45f, 0.95f));
    }

    private static void CreateObstacle(
        Transform parent, string name, Vector3 position, Vector3 scale, float yaw)
    {
        GameObject obstacle = CreateBlock(parent, name, position, scale, yaw);
        ConfigureNeutralBounce(obstacle);
    }

    private static void CreateSpeedTile(
        Transform parent, string name, Vector3 position, float yaw)
    {
        GameObject tile = CreateBlock(
            parent, name, position, new Vector3(2.4f, 0.12f, 1.25f), yaw);
        tile.GetComponent<BoxCollider>().isTrigger = true;
        tile.AddComponent<YellowSpeedTile>();
    }

    private static GameObject CreateBlock(
        Transform parent, string name, Vector3 position, Vector3 scale, float yaw)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        block.transform.localScale = scale;
        return block;
    }

    private static void ConfigureNeutralBounce(GameObject obstacle)
    {
        RedBouncePanel bounce = obstacle.AddComponent<RedBouncePanel>();
        bounce.Configure(
            1.5f,
            new Color(0.45f, 0.5f, 0.58f),
            new Color(0.2f, 0.23f, 0.28f));
    }

    private static void SetBlockColor(GameObject block, Color color)
    {
        Renderer blockRenderer = block.GetComponent<Renderer>();
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        blockRenderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        blockRenderer.SetPropertyBlock(properties);
    }
}
