using System;
using UnityEngine;

public enum PresentedEnemyKind { Spawner, MineLayer, Mortar, Minion, NetShooter, Runner }

[Serializable]
public sealed class EnemyPresentationProfile
{
    public PresentedEnemyKind kind;
    public string displayName, symbol;
    public Color color;
    public static PresentedEnemyKind Kind(EnemyBumper enemy) => enemy.GetComponent<SpawnerEnemy>() != null ? PresentedEnemyKind.Spawner :
        enemy.GetComponent<MineLayerEnemy>() != null ? PresentedEnemyKind.MineLayer :
        enemy.GetComponent<MortarEnemy>() != null ? PresentedEnemyKind.Mortar :
        enemy.GetComponent<NetShooterEnemy>() != null ? PresentedEnemyKind.NetShooter :
        enemy.GetComponent<RunnerEnemy>() != null ? PresentedEnemyKind.Runner : PresentedEnemyKind.Minion;
    public static EnemyPresentationProfile[] Defaults() => new[] {
        new EnemyPresentationProfile { kind = PresentedEnemyKind.Spawner, displayName = "Spawner", symbol = "S", color = new Color(.3f, 1f, .5f) },
        new EnemyPresentationProfile { kind = PresentedEnemyKind.MineLayer, displayName = "Mine layer", symbol = "L", color = new Color(1f, .6f, .18f) },
        new EnemyPresentationProfile { kind = PresentedEnemyKind.Mortar, displayName = "Mortar", symbol = "M", color = new Color(.8f, .5f, 1f) },
        new EnemyPresentationProfile { kind = PresentedEnemyKind.Minion, displayName = "Basic minion", symbol = "•", color = new Color(1f, .88f, .2f) },
        new EnemyPresentationProfile { kind = PresentedEnemyKind.NetShooter, displayName = "Net shooter", symbol = "N", color = Color.cyan },
        new EnemyPresentationProfile { kind = PresentedEnemyKind.Runner, displayName = "Runner", symbol = "R", color = new Color(1f, .3f, .45f) }
    };
}
