using UnityEngine;

[DefaultExecutionOrder(-200)]
public sealed class RunStateHost : MonoBehaviour
{
    public static RunStateHost Instance { get; private set; }

    public RunState State { get; } = new RunState();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
