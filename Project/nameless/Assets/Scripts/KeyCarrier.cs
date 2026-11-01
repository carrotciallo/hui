using UnityEngine;

/// 挂在玩家身上：同一时刻最多携带 1 把钥匙，钥匙数同步进 RunState。
public sealed class KeyCarrier : MonoBehaviour
{
    [SerializeField] Transform visual;

    public bool HasKey { get; private set; }

    void Awake()
    {
        if (visual == null)
        {
            var found = transform.Find("CarriedKey");
            if (found != null) visual = found;
        }

        SetVisual();
    }

    public void Configure(Transform keyVisual)
    {
        visual = keyVisual;
        SetVisual();
    }

    public bool PickUp()
    {
        if (HasKey) return false;

        HasKey = true;
        SetVisual();
        RunStateHost.Instance?.State.AddKey();
        return true;
    }

    public bool Consume()
    {
        if (!HasKey) return false;

        HasKey = false;
        SetVisual();
        RunStateHost.Instance?.State.TryConsumeKey();
        return true;
    }

    void SetVisual()
    {
        if (visual != null) visual.gameObject.SetActive(HasKey);
    }
}
