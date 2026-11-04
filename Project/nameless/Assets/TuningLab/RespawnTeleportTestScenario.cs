using UnityEngine;

/// <summary>传送点/重生点人工验证场景的运行时物件布置。</summary>
public sealed class RespawnTeleportTestScenario : MonoBehaviour
{
    public Vector2 teleportA = new Vector2(-8f, 1.5f);
    public Vector2 teleportB = new Vector2(8f, 1.5f);
    public Vector2 respawn = new Vector2(0f, 1.5f);
    public Vector2 keyA = new Vector2(-4f, 1f);
    public Vector2 keyB = new Vector2(4f, 1f);
    public Vector2 doorA = new Vector2(-2f, 1.5f);
    public Vector2 doorB = new Vector2(2f, 1.5f);

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
        // 所有传送点互为终点，不需要在场景里手工连 destination。
        CreateTeleport("Teleport_A", teleportA);
        CreateTeleport("Teleport_B", teleportB);
        CreateRespawn();
        CreateKeysAndDoors();
    }

    void CreateRespawn()
    {
        var go = new GameObject("RespawnPoint");
        go.transform.SetParent(transform, false);
        go.transform.position = respawn;
        go.AddComponent<LabRespawnPoint>();
    }

    LabTeleportPoint CreateTeleport(string name, Vector2 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = position;
        return go.AddComponent<LabTeleportPoint>();
    }

    void CreateKeysAndDoors()
    {
        keyLeft = CreateKey("Key_Left", keyA);
        keyRight = CreateKey("Key_Right", keyB);
        doorLeft = CreateDoor("Door_Left", doorA, new Color(0.25f, 0.75f, 1f));
        doorRight = CreateDoor("Door_Right", doorB, new Color(0.9f, 0.35f, 0.8f));
    }

    LabKey CreateKey(string name, Vector2 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = position;
        return go.AddComponent<LabKey>();
    }

    LabDoor CreateDoor(string name, Vector2 position, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = position;
        var door = go.AddComponent<LabDoor>();
        door.SetClosedColor(color);
        return door;
    }

    void ResetMapObjects()
    {
        if (doorLeft != null) doorLeft.ResetDoor();
        if (doorRight != null) doorRight.ResetDoor();
        if (keyLeft == null) keyLeft = CreateKey("Key_Left", keyA);
        if (keyRight == null) keyRight = CreateKey("Key_Right", keyB);
    }

    void OnDestroy()
    {
        if (lab != null) lab.MapReset -= ResetMapObjects;
    }
}
