using System;
using UnityEngine;

public enum PresentedEnemyKind { Spawner, MineLayer, Mortar, Minion, NetShooter, Runner }

[Serializable]
public sealed class EnemyPresentationProfile
{
    public PresentedEnemyKind kind;
    public string displayName, symbol;
    public Color color;
    [TextArea] public string ability, killTip;
    public static PresentedEnemyKind Kind(EnemyBumper enemy) => enemy.GetComponent<SpawnerEnemy>() != null ? PresentedEnemyKind.Spawner :
        enemy.GetComponent<MineLayerEnemy>() != null ? PresentedEnemyKind.MineLayer :
        enemy.GetComponent<MortarEnemy>() != null ? PresentedEnemyKind.Mortar :
        enemy.GetComponent<NetShooterEnemy>() != null ? PresentedEnemyKind.NetShooter :
        enemy.GetComponent<RunnerEnemy>() != null ? PresentedEnemyKind.Runner : PresentedEnemyKind.Minion;
    public static EnemyPresentationProfile[] Defaults() => new[] {
        new EnemyPresentationProfile { kind = PresentedEnemyKind.Spawner, ability = "Telegraphs and sends minions into the arena.", killTip = "Destroy its surviving minions too.", displayName = "Spawner", symbol = "S", color = new Color(.3f, 1f, .5f) },
        new EnemyPresentationProfile { kind = PresentedEnemyKind.MineLayer, ability = "Leaves proximity mines. Orange rings show danger.", killTip = "Avoid armed mines.", displayName = "Mine layer", symbol = "L", color = new Color(1f, .6f, .18f) },
        new EnemyPresentationProfile { kind = PresentedEnemyKind.Mortar, ability = "Marks a fixed landing point, then fires a large blast.", killTip = "Bait its own shells back onto it.", displayName = "Mortar", symbol = "M", color = new Color(.8f, .5f, 1f) },
        new EnemyPresentationProfile { kind = PresentedEnemyKind.Minion, ability = "Predictable straight patrols. Contact hurts robot form.", killTip = "Aim along its patrol; close passes count.", displayName = "Basic minion", symbol = "•", color = new Color(1f, .88f, .2f) },
        new EnemyPresentationProfile { kind = PresentedEnemyKind.NetShooter, displayName = "Net shooter", symbol = "N", color = Color.cyan,
            ability = "Warns, then fires a straight net. Obstacles block it.", killTip = "Dodge the net to avoid a brief snare." },
        new EnemyPresentationProfile { kind = PresentedEnemyKind.Runner, displayName = "Runner", symbol = "R", color = new Color(1f,.3f,.45f),
            ability = "Follows a readable route; each surviving hit makes it faster.", killTip = "Plan an intercept along its route." }
    };
}
