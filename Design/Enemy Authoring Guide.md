# Enemy Authoring Guide

This implementation separates gameplay rules from arena composition so designers can tune encounters without editing C#.

## Placed enemies

Select an enemy in the scene and edit its `Enemy Bumper` component.

- **Enemy Type:** Small or Large. This selects the default health and instant-kill threshold.
- **Move Speed:** Patrol movement speed for this individual enemy.
- **Patrol Path:** Optional shared scene path.
- **Patrol Offsets:** A local fallback loop relative to the enemy's starting position. Yellow gizmos show the route.
- **Attack Detection Range:** Radius in which the melee timer runs. A red gizmo shows the radius.
- **Attack Windup:** Required uninterrupted time in range before damage.
- **Attack Damage:** Health removed by a completed attack.
- **Attack Cooldown:** Delay before another melee windup can complete.
- **Maximum Health:** Set to zero for the type default: Small 30, Large 80.
- **Instant Kill Speed:** Set to zero for the global type threshold: Small 9, Large 16.
- **Damage Per Speed:** Converts a below-threshold impact into damage.
- **Defeat Speed Reward:** Set to zero for the global reward.

Moving or duplicating a placed enemy changes its spawn position and encounter quantity.

## Shared patrol routes

1. Create an empty GameObject and add `Enemy Patrol Path`.
2. Create child transforms for each patrol point.
3. Add the transforms to the ordered Points list.
4. Assign the path to one or more enemies.

The route is drawn in yellow in the Scene view. Enemies follow it as a closed loop. Moving a point updates every enemy that uses that route.

## Spawned enemy groups

Use `Enemy Encounter Spawner` when a designer needs quantity controls rather than manually duplicated enemies.

1. Create an empty GameObject and add `Enemy Encounter Spawner`.
2. Add a Spawn Group.
3. Assign an enemy prefab, Small or Large type, quantity, and spawn transforms.
4. Optionally assign a shared patrol path.
5. If quantity exceeds the number of spawn transforms, Spawn Radius offsets the additional enemies.

Placed enemies and spawned groups can coexist. `Spawn On Start` can be disabled when a future wave manager should trigger the group.

## Combat rules

- A ball impact at or above the type's threshold defeats the enemy immediately.
- A slower impact deals `round(impact speed × Damage Per Speed)` damage, with a minimum of one.
- Defeating an enemy grants speed to the attacking ball.
- Large enemies grant a small speed boost after a weak hit to make the next attempt achievable.
- Robot-form players take melee damage only after remaining inside the attack radius for the complete windup.
- Leaving the radius or changing the targeted player resets the windup.

## Recommended defaults

| Parameter | Small | Large |
|---|---:|---:|
| Health | 30 | 80 |
| Instant-kill speed | 9 | 16 |
| Move speed | 2.5–4 | 1–2.5 |
| Attack range | 2.25 | 2.75 |
| Attack windup | 1.1 s | 1.4 s |
| Attack damage | 1 | 1–2 |
| Damage per speed | 3 | 3 |

Treat these as starting values. Encounter difficulty should primarily come from route layout, enemy combinations, and attack timing rather than health inflation.
