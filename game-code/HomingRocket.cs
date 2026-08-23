using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class HomingRocket : MonoBehaviour, IPooledObject, IParryable
{
	[Header("Rocket Settings")]
	[SerializeField]
	private float speed = 5f;

	[SerializeField]
	private int damage = 1;

	[SerializeField]
	private float lifetime = 10f;

	[Header("Pool Settings")]
	[SerializeField]
	private string poolTag = "HomingRocket";

	[Header("Parry Settings")]
	[SerializeField]
	private float parryKnockback = 5f;

	[SerializeField]
	private LayerMask originalTargetLayer;

	[SerializeField]
	private LayerMask parriedTargetLayer;

	[Header("Collision Settings")]
	[SerializeField]
	private LayerMask ignoreLayerMask;

	[Header("VFX")]
	[SerializeField]
	private string explosionEffectTag = "Explosion";

	[SerializeField]
	private string destroyEffectName = "BulletDestroy";

	[Header("Camera Shake")]
	[SerializeField]
	private bool enableCameraShake = true;

	[SerializeField]
	private float shakeAmplitude = 1.5f;

	[SerializeField]
	private float shakeDuration = 0.2f;

	[Header("Debug")]
	[SerializeField]
	private bool showDebug;

	private const float ParriedImpactRadius = 0.5f;

	private Rigidbody2D rb;

	private Transform playerTransform;

	private GameObject sourceEnemy;

	private float activeTimer;

	private bool isActive;

	private bool isParried;

	private LayerMask currentTargetLayer;

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
		outlineController = GetComponent<BulletOutlineController>();
		if ((int)ignoreLayerMask == 0)
		{
			ignoreLayerMask = LayerMask.GetMask("Enemy", "Projectile", "Blocked");
		}
	}

	public void OnObjectSpawn()
	{
		SFXManager.Instance?.Play("ITEM_RocketLaunch", base.transform.position);
		activeTimer = 0f;
		isActive = true;
		isParried = false;
		currentTargetLayer = originalTargetLayer;
		rb.linearVelocity = Vector2.zero;
		outlineController?.SetAttacking();
		GameObject gameObject = GameObject.FindGameObjectWithTag("Player");
		if (gameObject != null)
		{
			playerTransform = gameObject.transform;
		}
	}

	public void Initialize(GameObject enemy)
	{
		sourceEnemy = enemy;
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
			Explode();
			return;
		}
		if (isParried && (sourceEnemy == null || !sourceEnemy.activeInHierarchy))
		{
			ReturnToPool();
			return;
		}
		Transform transform = ((!isParried) ? playerTransform : ((sourceEnemy != null) ? sourceEnemy.transform : null));
		if (transform == null)
		{
			ReturnToPool();
			return;
		}
		Vector2 vector = (transform.position - base.transform.position).normalized;
		rb.linearVelocity = vector * speed;
		float z = Mathf.Atan2(vector.y, vector.x) * 57.29578f;
		base.transform.rotation = Quaternion.Euler(0f, 0f, z);
		if (showDebug)
		{
			Debug.DrawLine(base.transform.position, transform.position, isParried ? Color.red : Color.yellow);
		}
		if (isParried && sourceEnemy != null)
		{
			CheckParriedImpact();
		}
	}

	private void CheckParriedImpact()
	{
		if (!(Vector2.Distance(base.transform.position, sourceEnemy.transform.position) > 0.5f))
		{
			sourceEnemy.GetComponent<IDamageable>()?.TakeDamage(damage * 3);
			Explode(sourceEnemy.transform.position);
		}
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (!isActive || ShouldIgnoreCollision(other))
		{
			return;
		}
		if (isParried && sourceEnemy != null && other.gameObject == sourceEnemy)
		{
			other.GetComponent<IDamageable>()?.TakeDamage(damage * 3);
			Explode(other.transform.position);
		}
		else if (((1 << other.gameObject.layer) & (int)currentTargetLayer) != 0)
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
			Explode(other.transform.position);
		}
		else
		{
			int num = LayerMask.NameToLayer("Ground");
			if (other.gameObject.layer == num)
			{
				Explode();
			}
		}
	}

	private bool ShouldIgnoreCollision(Collider2D other)
	{
		if (isParried)
		{
			if (sourceEnemy != null && other.gameObject == sourceEnemy)
			{
				return false;
			}
			int num = 1 << other.gameObject.layer;
			int mask = LayerMask.GetMask("Projectile");
			return (num & mask) != 0;
		}
		return ((1 << other.gameObject.layer) & (int)ignoreLayerMask) != 0;
	}

	public void OnParried()
	{
		if (!isParried)
		{
			SFXManager.Instance?.Play("ITEM_RocketParried", base.transform.position);
			isParried = true;
			currentTargetLayer = parriedTargetLayer;
			outlineController?.SetParried();
			if (showDebug)
			{
				Debug.Log("Rocket was parried! Retargeting enemy.");
			}
		}
	}

	public float GetParryKnockback()
	{
		return parryKnockback;
	}

	private void Explode(Vector3? explosionPosition = null)
	{
		Vector3 position = explosionPosition ?? base.transform.position;
		SFXManager.Instance?.Play("ITEM_RocketExplosion", base.transform.position);
		if (ObjectPool.Instance != null && !string.IsNullOrEmpty(explosionEffectTag))
		{
			ObjectPool.Instance.SpawnFromPool(explosionEffectTag, position, Quaternion.identity);
		}
		if (enableCameraShake && CameraShakeManager.Instance != null)
		{
			Vector3 velocity = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f).normalized * shakeAmplitude;
			CameraShakeManager.Instance.ShakeCamera(velocity, shakeDuration);
		}
		ReturnToPool();
	}

	private void ReturnToPool()
	{
		isActive = false;
		isParried = false;
		rb.linearVelocity = Vector2.zero;
		VFXManager.Instance?.PlayEffect(destroyEffectName, base.transform.position);
		base.gameObject.SetActive(value: false);
	}
}
