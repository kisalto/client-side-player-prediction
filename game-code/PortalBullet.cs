using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PortalBullet : MonoBehaviour, IPooledObject
{
	[Header("Bullet Settings")]
	[SerializeField]
	private int damage = 5;

	[SerializeField]
	private float speed = 12f;

	[SerializeField]
	private float lifetime = 4f;

	[Header("Pool Settings")]
	[SerializeField]
	private string poolTag = "PortalBullet";

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
	private string destroyEffectName = "BulletDestroy";

	private Rigidbody2D rb;

	private float activeTimer;

	private bool isActive;

	private bool isGroundHit;

	private float groundHitTimer;

	private IVFXService vfxService;

	private void Awake()
	{
		rb = GetComponent<Rigidbody2D>();
		GetComponent<Collider2D>().isTrigger = true;
		rb.gravityScale = 0f;
		vfxService = ServiceLocator.VFX ?? VFXManager.Instance;
	}

	public void OnObjectSpawn()
	{
		activeTimer = 0f;
		isActive = false;
		isGroundHit = false;
		groundHitTimer = 0f;
		rb.linearVelocity = Vector2.zero;
	}

	public void Initialize(Vector2 direction)
	{
		isActive = true;
		activeTimer = 0f;
		rb.linearVelocity = direction.normalized * speed;
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
		}
		else if ((num & (int)playerLayer) != 0)
		{
			PlayerHealthController component = other.GetComponent<PlayerHealthController>();
			if (component != null)
			{
				component.TakeDamage(damage, bypassBlock: true);
			}
			else
			{
				other.GetComponent<IDamageable>()?.TakeDamage(damage);
			}
			ReturnToPool();
		}
	}

	private void ReturnToPool()
	{
		isActive = false;
		isGroundHit = false;
		rb.linearVelocity = Vector2.zero;
		vfxService?.PlayEffect(destroyEffectName, base.transform.position);
		base.gameObject.SetActive(value: false);
	}
}
