using System;
using Sandbox;

public struct WeaponOffsetValue
{
    public Vector3 Value { get; set; }

    public static implicit operator Vector3(WeaponOffsetValue offset) => offset.Value;
    public static implicit operator WeaponOffsetValue(Vector3 vec) => new WeaponOffsetValue { Value = vec };

    public override string ToString() => Value.ToString();
}
