using UnityEngine;

/// <summary>Issue 验证场景：两把共享钥匙、两扇共享钥匙门。</summary>
public sealed class KeyDoorTestScenario : MonoBehaviour
{
    // Built-in TuningLab room: keep the demonstration in the lower-left,
    // away from the default player spawn at x=8/10.
    public Vector2 keyA = new Vector2(-11f, 1f);
    public Vector2 keyB = new Vector2(-7f, 1f);
    public Vector2 doorA = new Vector2(-9f, 1.5f);
    public Vector2 doorB = new Vector2(-5f, 1.5f);
    LabKey keyLeft;
    LabKey keyRight;
    LabDoor doorLeft;
    LabDoor doorRight;
    TuningLab lab;

    void Start()
    {
        lab = FindAnyObjectByType<TuningLab>();
        if (lab != null) lab.MapReset += ResetMapObjects;
        Build();
    }

    public void Build()
    {
        CreateKey("Key_01", keyA);
        CreateKey("Key_02", keyB);
        CreateDoor("Door_01", doorA, new Color(0.25f, 0.75f, 1f));
        CreateDoor("Door_02", doorB, new Color(0.9f, 0.35f, 0.8f));
    }

    void CreateKey(string name, Vector2 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = position;
        var key = go.AddComponent<LabKey>();
        if (name == "Key_01") keyLeft = key;
        else keyRight = key;
    }

    void CreateDoor(string name, Vector2 position, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = position;
        var door = go.AddComponent<LabDoor>();
        door.SetClosedColor(color);
        if (name == "Door_01") doorLeft = door;
        else doorRight = door;
    }

    void ResetMapObjects()
    {
        if (doorLeft != null) doorLeft.ResetDoor();
        if (doorRight != null) doorRight.ResetDoor();
        if (keyLeft == null) CreateKey("Key_01", keyA);
        if (keyRight == null) CreateKey("Key_02", keyB);
    }

    void OnDestroy()
    {
        if (lab != null) lab.MapReset -= ResetMapObjects;
    }
}
