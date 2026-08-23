using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class TopPortalBullet : MonoBehaviour, IPooledObject, IParryable
{
	[Header("Bullet Settings")]
	[SerializeField]
	private int damage = 5;

	[SerializeField]
	private float speed = 14f;

	[SerializeField]
	private float lifetime = 6f;

	[Header("Pool Settings")]
	[SerializeField]
	private string poolTag = "TopPortalBullet";

	[Header("Parry Settings")]
	[SerializeField]
	private float parryKnockback = 6f;

	[SerializeField]
	private float parryGraceDuration = 0.15f;

	[SerializeField]
	private int parriedDamageMultiplier = 2;

	[Header("Layers")]
	[SerializeField]
	private LayerMask playerLayer;

	[SerializeField]
	private LayerMask groundLayer;

	[Header("Ground Hit")]
	[Tooltip("Delay in seconds before the bullet returns to pool after hitting the ground.")]
	[SerializeField]
	private float groundHitDelay = 0.1f;

	[Header("VFX")]
	[SerializeField]
	private string blockEffectName = "Block";

	[SerializeField]
	private string destroyEffectName = "BulletDestroy";

	private const float PortalReachDistance = 0.5f;

	private Rigidbody2D rb;

	private Camera mainCamera;

	private float activeTimer;

	private bool isActive;

	private bool isParried;

	private bool isGroundHit;

	private float groundHitTimer;

	private int currentDamage;

	private Vector2 currentDirection;

	private Transform sourcePortal;

	private IVFXService vfxService;

	private BulletOutlineController outlineController;

	public bool IsAttacking
	{
		get
		{
			if (isActive && !isParried)
			{
				return !isGroundHit;
			}
			return false;
		}
	}

	private void Awake()
	{
		rb = GetComponent<Rigidbody2D>();
		rb.gravityScale = 0f;
		GetComponent<Collider2D>().isTrigger = true;
		vfxService = ServiceLocator.VFX ?? VFXManager.Instance;
		mainCamera = Camera.main;
		outlineController = GetComponent<BulletOutlineController>();
	}

	public void OnObjectSpawn()
	{
		activeTimer = 0f;
		isActive = true;
		isParried = false;
		isGroundHit = false;
		groundHitTimer = 0f;
		sourcePortal = null;
		currentDamage = damage;
		rb.linearVelocity = Vector2.zero;
		outlineController?.SetAttacking();
	}

	public void Initialize(Transform portal)
	{
		sourcePortal = portal;
		currentDirection = Vector2.down;
		rb.linearVelocity = currentDirection * speed;
		float z = Mathf.Atan2(currentDirection.y, currentDirection.x) * 57.29578f;
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
		else
		{
			if (!isParried)
			{
				return;
			}
			if (sourcePortal != null)
			{
				Vector2 vector = (Vector2)sourcePortal.position - (Vector2)base.transform.position;
				if (vector.magnitude <= 0.5f)
				{
					ReturnToPool();
					return;
				}
				Vector2 normalized = vector.normalized;
				rb.linearVelocity = normalized * speed;
				float z = Mathf.Atan2(normalized.y, normalized.x) * 57.29578f;
				base.transform.rotation = Quaternion.Euler(0f, 0f, z);
			}
			else if (IsOffCamera())
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
		}
		else
		{
			if (isParried || (num & (int)playerLayer) == 0)
			{
				return;
			}
			PlayerCombatController component = other.GetComponent<PlayerCombatController>();
			if (component != null && (component.IsParrying || component.IsBlockingUp) && activeTimer <= parryGraceDuration)
			{
				component.PerformParryUp();
				return;
			}
			if (component != null && component.IsBlockingUp)
			{
				vfxService?.PlayEffect(blockEffectName, base.transform.position);
				component.ApplyBlockKnockback(Vector2.down);
				ReturnToPool();
				return;
			}
			IDamageable component2 = other.GetComponent<IDamageable>();
			if (component2 != null)
			{
				Vector2 knockbackDirection = (other.transform.position - base.transform.position).normalized;
				component2.TakeDamage(currentDamage, knockbackDirection, 0f);
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
			rb.linearVelocity = Vector2.up * speed;
			float z = Mathf.Atan2(1f, 0f) * 57.29578f;
			base.transform.rotation = Quaternion.Euler(0f, 0f, z);
		}
	}

	public float GetParryKnockback()
	{
		return parryKnockback;
	}

	private bool IsOffCamera()
	{
		if (mainCamera == null)
		{
			return false;
		}
		return mainCamera.WorldToViewportPoint(base.transform.position).y > 1.1f;
	}

	private void ReturnToPool()
	{
		isActive = false;
		isParried = false;
		isGroundHit = false;
		sourcePortal = null;
		rb.linearVelocity = Vector2.zero;
		vfxService?.PlayEffect(destroyEffectName, base.transform.position);
		base.gameObject.SetActive(value: false);
	}
}
