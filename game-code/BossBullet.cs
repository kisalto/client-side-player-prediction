using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BossBullet : MonoBehaviour, IPooledObject, IParryable
{
	[Header("Bullet Settings")]
	[SerializeField]
	private int damage = 5;

	[SerializeField]
	private float speed = 15f;

	[SerializeField]
	private float lifetime = 5f;

	[Header("Pool Settings")]
	[SerializeField]
	private string poolTag = "BossBullet";

	[Header("Parry Settings")]
	[SerializeField]
	private float parryKnockback = 8f;

	[SerializeField]
	private int parriedDamageMultiplier = 2;

	[Tooltip("Seconds after spawn during which the bullet can always be parried, even if it has already overlapped the player. Handles point-blank parries.")]
	[SerializeField]
	private float parryGraceDuration = 0.15f;

	[Header("Layers")]
	[SerializeField]
	private LayerMask playerLayer;

	[SerializeField]
	private LayerMask enemyLayer;

	[SerializeField]
	private LayerMask groundLayer;

	[Header("VFX")]
	[SerializeField]
	private string blockEffectName = "Block";

	[SerializeField]
	private string armorBreakEffectName = "ArmorBreak";

	[SerializeField]
	private string destroyEffectName = "BulletDestroy";

	[Header("Ground Hit")]
	[Tooltip("Delay in seconds before the bullet is destroyed after hitting the ground.")]
	[SerializeField]
	private float groundHitDelay = 0.1f;

	private Rigidbody2D rb;

	private float activeTimer;

	private bool isActive;

	private bool isParried;

	private bool isGroundHit;

	private float groundHitTimer;

	private Vector2 targetDirection;

	private IVFXService vfxService;

	private int currentDamage;

	private BulletOutlineController outlineController;

	public bool IsAttacking
	{
		get
		{
			if (isActive)
			{
				return !isParried;
			}
			return false;
		}
	}

	private void Awake()
	{
		rb = GetComponent<Rigidbody2D>();
		vfxService = ServiceLocator.VFX ?? VFXManager.Instance;
		outlineController = GetComponent<BulletOutlineController>();
	}

	public void OnObjectSpawn()
	{
		activeTimer = 0f;
		isActive = true;
		isParried = false;
		isGroundHit = false;
		groundHitTimer = 0f;
		currentDamage = damage;
		rb.linearVelocity = Vector2.zero;
		outlineController?.SetAttacking();
	}

	public void Initialize(Vector2 direction)
	{
		targetDirection = direction.normalized;
		rb.linearVelocity = targetDirection * speed;
		float z = Mathf.Atan2(direction.y, direction.x) * 57.29578f;
		base.transform.rotation = Quaternion.Euler(0f, 0f, z);
	}

	private void Update()
	{
		if (!isActive)
		{
			return;
		}
		activeTimer += Time.deltaTime;
		if (activeTimer >= lifetime)
		{
			ReturnToPool();
		}
		else if (isGroundHit)
		{
			groundHitTimer += Time.deltaTime;
			if (groundHitTimer >= groundHitDelay)
			{
				ReturnToPool();
			}
		}
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (!isActive || isGroundHit)
		{
			return;
		}
		int num = 1 << other.gameObject.layer;
		if ((num & (int)groundLayer) != 0)
		{
			isGroundHit = true;
			groundHitTimer = 0f;
			rb.linearVelocity = Vector2.zero;
		}
		else if (isParried)
		{
			if ((num & (int)enemyLayer) != 0)
			{
				BigBobBoss component = other.GetComponent<BigBobBoss>();
				if (component != null)
				{
					component.OnParriedBulletHit();
				}
				ReturnToPool();
			}
		}
		else
		{
			if ((num & (int)playerLayer) == 0)
			{
				return;
			}
			PlayerCombatController component2 = other.GetComponent<PlayerCombatController>();
			if (component2 != null && component2.IsParrying && activeTimer <= parryGraceDuration)
			{
				component2.PerformParry();
				return;
			}
			IDamageable component3 = other.GetComponent<IDamageable>();
			if (component3 != null)
			{
				Vector2 knockbackDirection = (other.transform.position - base.transform.position).normalized;
				if (!component3.TakeDamage(currentDamage, knockbackDirection, 0f) && component2 != null && component2.IsBlocking)
				{
					vfxService?.PlayEffect(blockEffectName, base.transform.position);
				}
			}
			ReturnToPool();
		}
	}

	public void OnParried()
	{
		if (!isParried)
		{
			isParried = true;
			currentDamage = damage * parriedDamageMultiplier;
			outlineController?.SetParried();
			Vector2 vector = -targetDirection;
			rb.linearVelocity = vector * speed;
			float z = Mathf.Atan2(vector.y, vector.x) * 57.29578f;
			base.transform.rotation = Quaternion.Euler(0f, 0f, z);
		}
	}

	public float GetParryKnockback()
	{
		return parryKnockback;
	}

	private void ReturnToPool()
	{
		isActive = false;
		isParried = false;
		rb.linearVelocity = Vector2.zero;
		vfxService?.PlayEffect(destroyEffectName, base.transform.position);
		base.gameObject.SetActive(value: false);
	}
}
