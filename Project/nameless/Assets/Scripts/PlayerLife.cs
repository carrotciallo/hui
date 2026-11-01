using System;
using UnityEngine;

[RequireComponent(typeof(PlayerMotor))]
public sealed class PlayerLife : MonoBehaviour
{
    [SerializeField] int slot;
    [SerializeField] PlayerLife teammate;
    [SerializeField] float respawnDelay = 0.5f;

    public int Slot => slot;
    public PlayerLife Teammate => teammate;
    public bool IsAlive { get; private set; } = true;

    public event Action<DeathCause> Died;
    public event Action<Vector2> Respawned;

    PlayerMotor motor;
    Rigidbody2D body;
    Collider2D[] colliders;
    Renderer[] renderers;

    float respawnTimer;
    bool waitingRespawn;

    void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        body = GetComponent<Rigidbody2D>();
        colliders = GetComponentsInChildren<Collider2D>(true);
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    public void Configure(int playerSlot, PlayerLife teammatePlayer, float delay = 0.5f)
    {
        slot = playerSlot;
        teammate = teammatePlayer;
        respawnDelay = Mathf.Max(0f, delay);
    }

    void Update()
    {
        if (!IsAlive)
        {
            if (!waitingRespawn) return;

            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f) ReviveAt(RespawnPosition);
            return;
        }

        var world = World.Instance;
        if (world != null && transform.position.y < world.FallFloor) Kill(DeathCause.Fall);
    }

    public void Kill(DeathCause cause)
    {
        if (!IsAlive) return;

        IsAlive = false;
        waitingRespawn = true;
        respawnTimer = respawnDelay;
        SetSolid(false);
        Died?.Invoke(cause);
    }

    /// 复活并放到指定位置；对活人调用等同传送。
    public void ReviveAt(Vector2 position)
    {
        bool wasDead = !IsAlive;

        IsAlive = true;
        waitingRespawn = false;
        respawnTimer = 0f;
        SetSolid(true);
        motor.Teleport(position);

        if (wasDead) Respawned?.Invoke(position);
    }

    public void Teleport(Vector2 position)
    {
        if (IsAlive) motor.Teleport(position);
        else ReviveAt(position);
    }

    public Vector2 RespawnPosition
    {
        get
        {
            var state = RunStateHost.Instance?.State;
            if (state != null && state.TryGetRespawn(slot, out var recorded)) return recorded;

            var world = World.Instance;
            if (world != null) return world.SpawnFor(transform.position, slot);

            return motor != null ? motor.SpawnPosition : (Vector2)transform.position;
        }
    }

    void SetSolid(bool solid)
    {
        if (body != null) body.simulated = solid;
        if (colliders != null)
            foreach (var collider in colliders) collider.enabled = solid;
        if (renderers != null)
            foreach (var renderer in renderers) renderer.enabled = solid;
        if (motor != null) motor.enabled = solid;
    }
}
