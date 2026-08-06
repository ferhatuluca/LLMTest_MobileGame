# Unity C# Naming Conventions

All generated C# code must follow these rules:

- Classes, structs, enums, methods, properties, and events use `PascalCase`.
- Interfaces use `IPascalCase`, such as `IDamageable`.
- Private fields use `_camelCase`.
- Private static fields use `s_camelCase`.
- Constants use `k_PascalCase`.
- Local variables and parameters use `camelCase`.
- Boolean names should read like conditions, such as `_isAlive`, `HasTarget`, or `CanAttack()`.
- Method names should begin with a verb, such as `ApplyDamage`, `FindTarget`, or `ResetState`.
- Enum names should be singular, such as `WeaponType`.
- Enum values use `PascalCase`.
- Namespaces use `PascalCase`, such as `StudioName.GameName.Combat`.
- MonoBehaviour and ScriptableObject filenames must match their class names.
- Avoid unclear names such as `data`, `temp`, `manager`, `helper`, or `doStuff`.
- Avoid Hungarian notation and type prefixes such as `strName`, `fSpeed`, or `m_health`.
- Prefer clear, descriptive names over abbreviations.