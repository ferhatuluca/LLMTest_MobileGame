# Monsters vs Zombies - Game Design

## Game Overview

Monsters vs Zombies is a single-player mobile game with many units fighting at the same time.

The user controls the Player character with an on-screen joystick. Allies fight alongside the Player, while Enemies fight against the Player and Allies.

The current focus is the character and interaction systems.

## Unit Types

There are three unit types:

- **Player:** Controlled by the user. The Player can attack Enemies.
- **Ally:** Controlled by the game. Allies can attack Enemies.
- **Enemy:** Controlled by the game. Enemies can attack the Player and Allies.

Allies and the Player cannot attack each other. Enemies cannot attack other Enemies.

Enemies and Allies can be melee or ranged units.

## Health, Damage, and Death

Every unit has health and can take damage from a valid opposing unit.

When a unit attacks successfully, it reduces the target's health based on the attack's damage. When a unit's health reaches zero, the unit dies and can no longer move, chase, or attack.

Some units have an additional effect when they attack or die. Stunner can stun its target, and Divisible creates smaller units when it dies.

## Enemy and Ally Behaviour

Enemies and Allies have a chase range and an attack range.

### Enemy Behaviour

- If the Player or an Ally enters an Enemy's chase range, the Enemy starts chasing that target.
- If the target enters the Enemy's attack range, the Enemy attacks the target.
- If the target leaves the attack range but remains inside the chase range, the Enemy chases the target again.
- If the target leaves the chase range, the Enemy stops chasing that target.

### Ally Behaviour

- If an Enemy enters an Ally's chase range, the Ally starts chasing that Enemy.
- If the target enters the Ally's attack range, the Ally attacks the target.
- If the target leaves the attack range but remains inside the chase range, the Ally chases the target again.
- If the target leaves the chase range, the Ally stops chasing that target.

Melee units must get close to their target before attacking. Ranged units can attack from a greater distance.

## Player Behaviour

The Player is moved by the user with an on-screen joystick.

The Player has an attack range but has no chase capability, chase component, or chase-range data. The Player never chases a target automatically because movement is controlled by the user. When an Enemy is inside the Player's attack range, the Player can attack it with the current weapon.

## Unit Kinds

### Kinds Shared by Enemies and Allies

#### Classic Melee

- Has no weapon.
- Attacks with its hands.
- Punches its target to deal damage.

#### Classic Range

- Holds a simple ranged weapon.
- Shoots simple bullets at its target.

#### Dragon

- Attacks from range.
- Throws fireballs from its mouth.

### Enemy Kinds

#### Stunner

- Is larger than Classic Melee.
- Holds a large hammer in its right hand.
- Attacks its target with the hammer.
- Stuns the target on the first hit.
- After the first hit, every third hit stuns the target again.

While stunned, a unit cannot move, chase, or attack. It can act again after the stun ends.

#### Divisible

- Is larger and thicker than Classic Melee.
- When it dies, it divides into three smaller units called MiniDivisible.

#### MiniDivisible

- Is an Enemy unit created when a Divisible dies.
- Has the same shape as Divisible but is smaller.
- Each MiniDivisible behaves as a separate Enemy unit.

### Monster Kinds

#### DoubleHead

- Is larger than Classic Melee.
- Has two heads.
- Attacks with its wrist.

More Enemy, Ally, and Monster kinds can be added later.

## Player Weapons

The Player initially has three weapons:

### Pistol

- A simple pistol.
- Shoots simple bullets.

### GrenadeGun

- Shoots grenades as projectiles.

### SpaceGun

- Shoots a laser.

## Weapon Switching During Testing

Weapon switching for the user on mobile is not part of the current system. It will be added later.

For testing in the Unity Editor:

- `Q` changes to the previous Player weapon.
- `E` changes to the next Player weapon.
- The weapons cycle through Pistol, GrenadeGun, and SpaceGun.

These keys are only for testing and are not part of the mobile controls.
