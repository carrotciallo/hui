using UnityEngine;

/// A 的 L 形态生成的转换区，挂在 A 的子物体上。
public sealed class ConverterZone : MonoBehaviour
{
    FormController owner;

    public void Configure(FormController value) => owner = value;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null) owner.HandleConverterEnter(other);
    }
}
