# Unity C# Coding Preferences

All generated and updated C# code must follow these preferences:

- Use properties with get/set for fields that are used by other classes, do not define get/set methods.
Example: 
```C#
public int CurrentHealth { private set; get; }
```
- Use [field: SerializeField] public properties for fields in ScriptableObject classes.
Example: 
```C#
[field: SerializeField] public int Health { private set; get;}
```
- Only use [SerializeField] private for collections that will be queried in ScriptableObject class.
Example:
```C#
[SerializeField] private PlayerData[] _playerData;
public PlayerData GetPlayerData(PlayerType type)
{
    return ...;
}
```
- Use methods with lambda expressions only when it returns one field or property.
Example:
```C#
public int GetPlayerHealth => _player.Health;
```