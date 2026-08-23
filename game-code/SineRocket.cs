using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SineRocket : MonoBehaviour, IPooledObject, IParryable
{
	[Header("Rocket Settings")]
	[SerializeField]
	private int damage = 10;

	[SerializeField]
	private float lifetime = 8f;

	[Header("Arc Settings")]
	[Tooltip("Horizontal speed toward the target.")]
	[SerializeField]
	private float horizontalSpeed = 6f;

	[Tooltip("Initial upward launch speed.")]
	[SerializeField]
	private float launchSpeed = 12f;

	[Tooltip("Gravity pulling the rocket downward during the arc.")]
	[SerializeField]
	private float gravity = 15f;

	[Tooltip("Random spread added to launch speed for volley variety.")]
	[SerializeField]
	private float launchSpeedVariance = 3f;

	[Header("Parry Settings")]
	[SerializeField]
	private float parryKnockback = 6f;

	[SerializeField]
	private float parriedHomingSpeed = 14f;

	[SerializeField]
	private int parriedDamageMultiplier = 3;

	[Header("Pool Settings")]
	[SerializeField]
	private string poolTag = "SineRocket";

	[Header("Layers")]
	[SerializeField]
	private LayerMask playerLayer;

	[SerializeField]
	private LayerMask enemyLayer;

	[SerializeField]
	private LayerMask groundLayer;

	[Header("VFX")]
	[SerializeField]
	private string explosionEffectTag = "Explosion";

	[SerializeField]
	private string destroyEffectName = "BulletDestroy";

	[Header("Camera Shake")]
	[SerializeField]
	private bool enableCameraShake = true;

	[SerializeField]
	private float shakeAmplitude = 1.2f;

	[SerializeField]
	private float shakeDuration = 0.2f;

	private Rigidbody2D rb;

	private float activeTimer;

	private bool isActive;

	private bool isParried;

	private int currentDamage;

	private Vector2 velocity;

	private GameObject sourceEnemy;

	private IVFXService vfxService;

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
		velocity = Vector2.zero;
		rb.linearVelocity = Vector2.zero;
		outlineController?.SetAttacking();
	}

	public void Initialize(Vector2 direction, GameObject enemy = null)
	{
		sourceEnemy = enemy;
		float num = Mathf.Sign(direction.x);
		float y = launchSpeed + Random.Range(0f - launchSpeedVariance, launchSpeedVariance);
		velocity = new Vector2(num * horizontalSpeed, y);
		rb.linearVelocity = velocity;
	}

	private void Update()
	{
		if (isActive)
		{
			activeTimer += Time.deltaTime;
			if (activeTimer >= lifetime)
			{
				Explode();
			}
			else if (isParried)
			{
				UpdateParriedMovement();
			}
			else
			{
				UpdateArcMovement();
			}
		}
	}

	private void UpdateArcMovement()
	{
		velocity.y -= gravity * Time.deltaTime;
		rb.linearVelocity = velocity;
		float z = Mathf.Atan2(velocity.y, velocity.x) * 57.29578f;
		base.transform.rotation = Quaternion.Euler(0f, 0f, z);
	}

	private void UpdateParriedMovement()
	{
		if (sourceEnemy == null || !sourceEnemy.activeInHierarchy)
		{
			Explode();
			return;
		}
		Vector2 normalized = ((Vector2)sourceEnemy.transform.position - (Vector2)base.transform.position).normalized;
		rb.linearVelocity = normalized * parriedHomingSpeed;
		float z = Mathf.Atan2(normalized.y, normalized.x) * 57.29578f;
		base.transform.rotation = Quaternion.Euler(0f, 0f, z);
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (!isActive)
		{
			return;
		}
		int num = 1 << other.gameObject.layer;
		if ((num & (int)groundLayer) != 0)
		{
			Explode();
		}
		else if (isParried)
		{
			if ((num & (int)enemyLayer) != 0)
			{
				other.GetComponent<IDamageable>()?.TakeDamage(currentDamage);
				Explode(other.transform.position);
			}
		}
		else if ((num & (int)playerLayer) != 0)
		{
			IDamageable component = other.GetComponent<IDamageable>();
			if (component != null)
			{
				Vector2 knockbackDirection = (other.transform.position - base.transform.position).normalized;
				component.TakeDamage(currentDamage, knockbackDirection, parryKnockback);
			}
			Explode(other.transform.position);
		}
	}

	public void OnParried()
	{
		if (!isParried)
		{
			SFXManager.Instance?.Play("ITEM_RocketParried", base.transform.position);
			isParried = true;
			currentDamage = damage * parriedDamageMultiplier;
			velocity = Vector2.zero;
			rb.linearVelocity = Vector2.zero;
			outlineController?.SetParried();
		}
	}

	public float GetParryKnockback()
	{
		return parryKnockback;
	}

	private void Explode(Vector3? explosionPosition = null)
	{
		if (isActive)
		{
			Vector3 position = explosionPosition ?? base.transform.position;
			SFXManager.Instance?.Play("ITEM_RocketExplosion", position);
			if (ObjectPool.Instance != null && !string.IsNullOrEmpty(explosionEffectTag))
			{
				ObjectPool.Instance.SpawnFromPool(explosionEffectTag, position, Quaternion.identity);
			}
			if (enableCameraShake && CameraShakeManager.Instance != null)
			{
				Vector3 vector = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f).normalized * shakeAmplitude;
				CameraShakeManager.Instance.ShakeCamera(vector, shakeDuration);
			}
			ReturnToPool();
		}
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
