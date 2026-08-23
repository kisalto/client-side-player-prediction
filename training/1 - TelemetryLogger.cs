// TelemetryLogger.cs
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem; 

public class TelemetryLogger : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Intervalo entre snapshots completos, em segundos. 0.05 = 20Hz.")]
    [SerializeField]
    private float snapshotInterval = 0.05f;

    [Tooltip("De quanto em quanto tempo a lista de inimigos/bosses e recalculada.")]
    [SerializeField]
    private float entityRefreshInterval = 0.5f;

    [Header("Export")]
    [Tooltip("Pasta dentro de Application.persistentDataPath onde os .json vao ser salvos.")]
    [SerializeField]
    private string exportSubfolder = "telemetry";

    [Tooltip("Tecla que exporta o log gravado ate agora.")]
    [SerializeField]
    private Key exportKey = Key.F9; 

    [Tooltip("Se marcado, limpa o buffer em memoria depois de cada export (sessoes menores, exports mais frequentes).")]
    [SerializeField]
    private bool clearBufferAfterExport = false;

    [Header("Projectile pool tags")]
    [SerializeField]
    private string[] projectilePoolTags = new string[]
    {
        "EnemyProjectile", "BossBullet", "PortalBullet",
        "TopPortalBullet", "HomingRocket", "SineRocket"
    };

    // ---- estado interno ------------------------------------------------------
    private PlayerStateMachine player;
    private ObjectPool cachedPool; // <-- Novo cache seguro para a pool
    private readonly List<TelemetrySnapshot> frames = new List<TelemetrySnapshot>();
    private readonly List<TelemetryEvent> events = new List<TelemetryEvent>();

    private EnemyController[] cachedEnemies = new EnemyController[0];
    private BigBobBoss[] cachedBigBosses = new BigBobBoss[0];
    private LilBobBoss[] cachedLilBosses = new LilBobBoss[0];

    // deteccao de morte por borda (id da instancia -> estava vivo no snapshot anterior?)
    private readonly Dictionary<int, bool> wasAliveById = new Dictionary<int, bool>();

    private float snapshotTimer;
    private float refreshTimer;
    private float sessionStartTime;

    private void Awake()
    {
        sessionStartTime = Time.realtimeSinceStartup;
    }

    private void Start()
    {
        player = FindFirstObjectByType<PlayerStateMachine>();
        if (player == null)
        {
            Debug.LogError("[TelemetryLogger] PlayerStateMachine nao encontrado na cena. Logger desativado.");
            enabled = false;
            return;
        }

        SubscribeToPlayerEvents();
        RefreshEntityCaches();
        Debug.Log("[TelemetryLogger] gravando. Aperte " + exportKey + " para exportar o log.");
    }

    private void OnDestroy()
    {
        UnsubscribeFromPlayerEvents();
    }

    private void Update()
    {
        refreshTimer += Time.deltaTime;
        if (refreshTimer >= entityRefreshInterval)
        {
            refreshTimer = 0f;
            RefreshEntityCaches();
        }

        snapshotTimer += Time.deltaTime;
        if (snapshotTimer >= snapshotInterval)
        {
            snapshotTimer = 0f;
            CaptureSnapshot();
        }

        if (Keyboard.current != null && Keyboard.current[exportKey].wasPressedThisFrame)
        {
            ExportToJson();
        }
    }

    // ---------------------------------------------------------------------
    // Eventos do jogador (ja existem no codigo, so assinamos)
    // ---------------------------------------------------------------------
    private void SubscribeToPlayerEvents()
    {
        player.Health.OnDeath += OnPlayerDeath;
        player.Health.OnHurt += OnPlayerHurt;
        player.Health.OnInvincibilityStart += OnPlayerInvincibilityStart;
        player.Health.OnInvincibilityEnd += OnPlayerInvincibilityEnd;
        player.Combat.OnAttackHit += OnPlayerAttackHit;
        player.Combat.OnParrySuccess += OnPlayerParrySuccess;
        player.Combat.OnAttackDeflected += OnPlayerAttackDeflected;
        player.Combat.OnPogoBounceTrigger += OnPlayerPogoBounce;
        player.Dash.OnDashStart += OnPlayerDashStart;
        player.Dash.OnDashEnd += OnPlayerDashEnd;
        BigBobBoss.OnBossDied += OnBigBossDied; 
    }

    private void UnsubscribeFromPlayerEvents()
    {
        if (player != null)
        {
            player.Health.OnDeath -= OnPlayerDeath;
            player.Health.OnHurt -= OnPlayerHurt;
            player.Health.OnInvincibilityStart -= OnPlayerInvincibilityStart;
            player.Health.OnInvincibilityEnd -= OnPlayerInvincibilityEnd;
            player.Combat.OnAttackHit -= OnPlayerAttackHit;
            player.Combat.OnParrySuccess -= OnPlayerParrySuccess;
            player.Combat.OnAttackDeflected -= OnPlayerAttackDeflected;
            player.Combat.OnPogoBounceTrigger -= OnPlayerPogoBounce;
            player.Dash.OnDashStart -= OnPlayerDashStart;
            player.Dash.OnDashEnd -= OnPlayerDashEnd;
        }
        BigBobBoss.OnBossDied -= OnBigBossDied;
    }

    private void OnPlayerDeath() => LogEvent("player_death");
    private void OnPlayerHurt() => LogEvent("player_hurt");
    private void OnPlayerInvincibilityStart() => LogEvent("player_invincibility_start");
    private void OnPlayerInvincibilityEnd() => LogEvent("player_invincibility_end");
    private void OnPlayerAttackHit() => LogEvent("player_attack_hit");
    private void OnPlayerParrySuccess() => LogEvent("player_parry_success");
    private void OnPlayerAttackDeflected() => LogEvent("player_attack_deflected");
    private void OnPlayerPogoBounce() => LogEvent("player_pogo_bounce");
    private void OnPlayerDashStart() => LogEvent("player_dash_start");
    private void OnPlayerDashEnd() => LogEvent("player_dash_end");
    private void OnBigBossDied() => LogEvent("boss_died_event");

    private void LogEvent(string type, string extra = "")
    {
        events.Add(new TelemetryEvent
        {
            t = Time.realtimeSinceStartup - sessionStartTime,
            type = type,
            extra = extra
        });
    }

    // ---------------------------------------------------------------------
    // Cache de inimigos/bosses (throttled)
    // ---------------------------------------------------------------------
    private void RefreshEntityCaches()
    {
        cachedEnemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        cachedBigBosses = FindObjectsByType<BigBobBoss>(FindObjectsSortMode.None);
        cachedLilBosses = FindObjectsByType<LilBobBoss>(FindObjectsSortMode.None);
        
        // <-- Adicionado: Buca silenciosa pela pool na cena sem acionar o getter maldito do Singleton
        if (cachedPool == null)
        {
            cachedPool = FindFirstObjectByType<ObjectPool>();
        }
    }

    // ---------------------------------------------------------------------
    // Snapshot completo
    // ---------------------------------------------------------------------
    private void CaptureSnapshot()
    {
        var snap = new TelemetrySnapshot
        {
            t = Time.realtimeSinceStartup - sessionStartTime
        };

        // --- jogador -----------------------------------------------------------
        Vector2 pPos = player.transform.position;
        Vector2 pVel = player.Movement.Velocity;
        snap.px = pPos.x;
        snap.py = pPos.y;
        snap.pvx = pVel.x;
        snap.pvy = pVel.y;
        snap.isGrounded = player.Movement.IsGrounded;
        snap.isClimbing = player.Movement.IsClimbing;
        snap.isFacingRight = player.Movement.IsFacingRight;
        snap.isDashing = player.Dash.IsDashing;
        snap.canDash = player.Dash.CanDash;
        snap.isAttacking = player.Combat.IsAttacking;
        snap.isBlocking = player.Combat.IsBlocking;
        snap.isBlockingUp = player.Combat.IsBlockingUp;
        snap.isParrying = player.Combat.IsParrying;
        snap.currentHealth = player.Health.CurrentHealth;
        snap.maxHealth = player.Health.MaxHealth;
        snap.isInvincible = player.Health.IsInvincible;
        snap.isAlive = player.Health.IsAlive;

        // --- inimigos regulares --------------------------------------------------
        var enemyList = new List<EntitySnapshot>(cachedEnemies.Length);
        foreach (EnemyController enemy in cachedEnemies)
        {
            if (enemy == null)
            {
                continue;
            }
            int id = enemy.GetInstanceID();
            bool aliveNow = enemy.IsAlive;
            CheckDeathEdge(id, aliveNow, "enemy_died", enemy.name);
            if (!aliveNow)
            {
                continue; 
            }
            Vector2 pos = enemy.transform.position;
            enemyList.Add(new EntitySnapshot
            {
                x = pos.x,
                y = pos.y,
                health = enemy.GetCurrentHealth,
                maxHealth = enemy.MaxHealth,
                isAttacking = enemy.Combat != null && enemy.Combat.IsAttacking,
                hasDetectedPlayer = enemy.Detection != null && enemy.Detection.HasDetectedPlayer,
                distanceToPlayer = enemy.Detection != null ? enemy.Detection.GetDistanceToPlayer() : -1f,
                isBeingKnockedBack = enemy.isBeingKnockedBack
            });
        }
        snap.enemies = enemyList;

        // --- boss (BigBobBoss ou LilBobBoss, o que estiver ativo na cena) --------
        snap.bossActive = false;
        foreach (BigBobBoss boss in cachedBigBosses)
        {
            if (boss == null) continue;
            
            int id = boss.GetInstanceID();
            bool aliveNow = boss.IsAlive;
            CheckDeathEdge(id, aliveNow, "boss_died_poll", boss.name);
            if (aliveNow && boss.IsBossActive)
            {
                Vector2 pos = boss.transform.position;
                snap.bossActive = true;
                snap.bossX = pos.x;
                snap.bossY = pos.y;
                snap.bossHealth = boss.GetCurrentHealth;
                snap.bossMaxHealth = boss.MaxHealth;
            }
        }
        foreach (LilBobBoss boss in cachedLilBosses)
        {
            if (boss == null) continue;
            
            int id = boss.GetInstanceID();
            bool aliveNow = boss.IsAlive;
            CheckDeathEdge(id, aliveNow, "boss_died_poll", boss.name);
            if (aliveNow && !snap.bossActive)
            {
                Vector2 pos = boss.transform.position;
                snap.bossActive = true;
                snap.bossX = pos.x;
                snap.bossY = pos.y;
                snap.bossHealth = boss.GetCurrentHealth;
                snap.bossMaxHealth = boss.MaxHealth;
            }
        }

        // --- projeteis (de todas as pool tags conhecidas) ------------------------
        var bulletList = new List<BulletSnapshot>();
        
        // <-- Adicionado: Verificação segura usando a pool em cache em vez de ObjectPool.Instance
        if (cachedPool != null)
        {
            foreach (string tag in projectilePoolTags)
            {
                cachedPool.ForEachActive(tag, go =>
                {
                    var rb = go.GetComponent<Rigidbody2D>();
                    var col = go.GetComponent<Collider2D>();
                    Vector2 vel = rb != null ? rb.linearVelocity : Vector2.zero;
                    Vector2 size = col != null ? (Vector2)col.bounds.size : Vector2.one;
                    bulletList.Add(new BulletSnapshot
                    {
                        tag = tag,
                        x = go.transform.position.x,
                        y = go.transform.position.y,
                        vx = vel.x,
                        vy = vel.y,
                        w = size.x,
                        h = size.y
                    });
                });
            }
        }
        snap.bullets = bulletList;

        frames.Add(snap);
    }

    private void CheckDeathEdge(int instanceId, bool aliveNow, string eventType, string entityName)
    {
        bool wasAlive = wasAliveById.TryGetValue(instanceId, out bool prev) ? prev : aliveNow;
        if (wasAlive && !aliveNow)
        {
            LogEvent(eventType, entityName);
        }
        wasAliveById[instanceId] = aliveNow;
    }

    // ---------------------------------------------------------------------
    // Export para JSON
    // ---------------------------------------------------------------------
    private void ExportToJson()
    {
        if (frames.Count == 0)
        {
            Debug.LogWarning("[TelemetryLogger] nenhum snapshot gravado ainda.");
            return;
        }

        var session = new TelemetrySession
        {
            recordedAt = DateTime.UtcNow.ToString("o"),
            snapshotIntervalSeconds = snapshotInterval,
            frameCount = frames.Count,
            eventCount = events.Count,
            frames = frames,
            events = events
        };

        string json = JsonUtility.ToJson(session, true);
        string folder = Path.Combine(Application.persistentDataPath, exportSubfolder);
        Directory.CreateDirectory(folder);
        string fileName = "session_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".json";
        string fullPath = Path.Combine(folder, fileName);
        File.WriteAllText(fullPath, json);

        Debug.Log("[TelemetryLogger] exportado: " + frames.Count + " frames, " + events.Count +
            " eventos -> " + fullPath);

        if (clearBufferAfterExport)
        {
            frames.Clear();
            events.Clear();
            wasAliveById.Clear();
        }
    }
}

[Serializable]
public class TelemetrySession
{
    public string recordedAt;
    public float snapshotIntervalSeconds;
    public int frameCount;
    public int eventCount;
    public List<TelemetrySnapshot> frames;
    public List<TelemetryEvent> events;
}

[Serializable]
public class TelemetrySnapshot
{
    public float t;

    // jogador
    public float px, py, pvx, pvy;
    public bool isGrounded, isClimbing, isFacingRight;
    public bool isDashing, canDash;
    public bool isAttacking, isBlocking, isBlockingUp, isParrying;
    public int currentHealth, maxHealth;
    public bool isInvincible, isAlive;

    // boss 
    public bool bossActive;
    public float bossX, bossY;
    public int bossHealth, bossMaxHealth;

    public List<EntitySnapshot> enemies;
    public List<BulletSnapshot> bullets;
}

[Serializable]
public class EntitySnapshot
{
    public float x, y;
    public int health, maxHealth;
    public bool isAttacking;
    public bool hasDetectedPlayer;
    public float distanceToPlayer;
    public bool isBeingKnockedBack;
}

[Serializable]
public class BulletSnapshot
{
    public string tag;
    public float x, y, vx, vy, w, h;
}

[Serializable]
public class TelemetryEvent
{
    public float t;
    public string type;
    public string extra;
}