using System;
using UnityEngine;

public enum PresentedEnemyKind { Spawner, MineLayer, Mortar, Minion }

[Serializable]
public sealed class EnemyPresentationProfile
{
    public PresentedEnemyKind kind;
    public string displayName, symbol;
    public Color color;
    [TextArea] public string ability;
    [TextArea] public string killTip;
    public static PresentedEnemyKind Kind(EnemyBumper enemy) => enemy.GetComponent<SpawnerEnemy>() != null ? PresentedEnemyKind.Spawner :
        enemy.GetComponent<MineLayerEnemy>() != null ? PresentedEnemyKind.MineLayer :
        enemy.GetComponent<MortarEnemy>() != null ? PresentedEnemyKind.Mortar : PresentedEnemyKind.Minion;
    public static EnemyPresentationProfile[] Defaults() => new[] {
        new EnemyPresentationProfile { kind = PresentedEnemyKind.Spawner, displayName = "Spawner", symbol = "S", color = new Color(.3f, 1f, .5f),
            ability = "Sends more minions into the arena. Green rings warn where they will appear.", killTip = "Destroy the spawner, then clear its surviving minions." },
        new EnemyPresentationProfile { kind = PresentedEnemyKind.MineLayer, displayName = "Mine layer", symbol = "L", color = new Color(1f, .6f, .18f),
            ability = "Roams and leaves proximity mines. Flashing rings mean a mine is arming.", killTip = "Avoid armed rings. Build speed on a clear route, then hit the layer." },
        new EnemyPresentationProfile { kind = PresentedEnemyKind.Mortar, displayName = "Mortar", symbol = "M", color = new Color(.8f, .5f, 1f),
            ability = "Marks your position and launches a shell. The marked landing point stays fixed.", killTip = "Leave the warning circle, then line up a fast return attack." },
        new EnemyPresentationProfile { kind = PresentedEnemyKind.Minion, displayName = "Basic minion", symbol = "•", color = new Color(1f, .88f, .2f),
            ability = "Patrols straight horizontal, vertical, or diagonal routes, reversing at obstacles. Contact hurts robot form; ball form is safe.", killTip = "Bonk it in ball form at attack speed. Minion kills also help build combos." }
    };
}
