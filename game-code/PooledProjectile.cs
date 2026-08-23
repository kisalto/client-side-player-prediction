using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PooledProjectile : MonoBehaviour, IPooledObject, IParryable
{
	[Header("Projectile Settings")]
	[SerializeField]
	private int damage = 1;

	[SerializeField]
	private float lifetime = 5f;

	[SerializeField]
	private LayerMask hitLayers;

	[Header("Pool Settings")]
	[SerializeField]
	private string poolTag = "EnemyProjectile";

	[Header("Parry Settings")]
	[SerializeField]
	private float parryKnockback = 5f;

	[SerializeField]
	private LayerMask originalOwnerLayer;

	[SerializeField]
	private LayerMask parriedTargetLayer;

	[Header("VFX")]
	[SerializeField]
	private string blockEffectName = "Block";

	[SerializeField]
	private string destroyEffectName = "BulletDestroy";

	private Rigidbody2D rb;

	private float activeTimer;

	private bool isActive;

	private bool isParried;

	private int currentDamage;

	private IVFXService vfxService;

	private GameObject sourceEnemy;

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
		currentDamage = damage;
		hitLayers = originalOwnerLayer;
		sourceEnemy = null;
		rb.linearVelocity = Vector2.zero;
		outlineController?.SetAttacking();
	}

	public void SetSource(GameObject enemy)
	{
		sourceEnemy = enemy;
	}

	public void Launch(Vector2 direction, float speed)
	{
		rb.linearVelocity = direction.normalized * speed;
		float z = Mathf.Atan2(direction.y, direction.x) * 57.29578f;
		base.transform.rotation = Quaternion.Euler(0f, 0f, z);
	}

	private void Update()
	{
		if (isActive)
		{
			activeTimer += Time.deltaTime;
			if (activeTimer >= lifetime)
			{
				ReturnToPool();
			}
			else if (isParried && (sourceEnemy == null || !sourceEnemy.activeInHierarchy))
			{
				ReturnToPool();
			}
		}
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (!isActive || ((1 << other.gameObject.layer) & (int)hitLayers) == 0)
		{
			return;
		}
		IDamageable component = other.GetComponent<IDamageable>();
		if (component != null)
		{
			Vector2 knockbackDirection = (other.transform.position - base.transform.position).normalized;
			if (!component.TakeDamage(currentDamage, knockbackDirection, 0f))
			{
				PlayerCombatController component2 = other.GetComponent<PlayerCombatController>();
				if (component2 != null && component2.IsBlocking)
				{
					vfxService?.PlayEffect(blockEffectName, base.transform.position);
				}
			}
		}
		ReturnToPool();
	}

	public void OnParried()
	{
		if (!isParried)
		{
			isParried = true;
			currentDamage = damage * 3;
			hitLayers = parriedTargetLayer;
			outlineController?.SetParried();
			Vector2 linearVelocity = rb.linearVelocity;
			rb.linearVelocity = -linearVelocity;
			float z = Mathf.Atan2(0f - linearVelocity.y, 0f - linearVelocity.x) * 57.29578f;
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
		sourceEnemy = null;
		rb.linearVelocity = Vector2.zero;
		vfxService?.PlayEffect(destroyEffectName, base.transform.position);
		base.gameObject.SetActive(value: false);
	}
}
