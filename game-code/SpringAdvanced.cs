using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SpringAdvanced : MonoBehaviour
{
	[Header("Spring Settings")]
	[SerializeField]
	private float springForce = 20f;

	[SerializeField]
	private bool overridePlayerVelocity = true;

	[SerializeField]
	private bool requireDownwardVelocity = true;

	[SerializeField]
	private float minDownwardVelocity = -0.5f;

	[Header("Visual Feedback")]
	[SerializeField]
	private Transform visualTransform;

	[SerializeField]
	private float squashAmount = 0.7f;

	[SerializeField]
	private float squashDuration = 0.1f;

	[SerializeField]
	private AnimationCurve squashCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	[Header("Animation")]
	[SerializeField]
	private Animator animator;

	[SerializeField]
	private string bounceAnimationTrigger = "Bounce";

	[Header("Audio")]
	[SerializeField]
	private AudioSource audioSource;

	[SerializeField]
	private AudioClip bounceSound;

	[Header("Particle Effects")]
	[SerializeField]
	private bool spawnParticles = true;

	[SerializeField]
	private bool useObjectPooling;

	[SerializeField]
	private string poolTag = "SpringParticles";

	[SerializeField]
	private ParticleSystem bounceParticlesPrefab;

	[SerializeField]
	private Transform particleSpawnPoint;

	[Header("Continuous Particles")]
	[SerializeField]
	private bool spawnParticlesOnStart;

	[SerializeField]
	private bool continuousParticles;

	[SerializeField]
	private float particleSpawnInterval = 1f;

	private const string PLAYER_TAG = "Player";

	private Vector3 originalScale;

	private float squashTimer;

	private bool isSquashing;

	private Rigidbody2D rb;

	private float nextParticleSpawnTime;

	private void Awake()
	{
		rb = GetComponent<Rigidbody2D>();
		if (rb != null)
		{
			rb.bodyType = RigidbodyType2D.Kinematic;
			rb.simulated = true;
		}
		if (visualTransform != null)
		{
			originalScale = visualTransform.localScale;
		}
		Collider2D component = GetComponent<Collider2D>();
		if (component != null)
		{
			component.isTrigger = true;
		}
		else
		{
			Debug.LogError("[SpringAdvanced] No Collider2D found on " + base.gameObject.name + "!", this);
		}
		if (particleSpawnPoint == null)
		{
			GameObject gameObject = new GameObject("ParticleSpawnPoint");
			gameObject.transform.SetParent(base.transform);
			gameObject.transform.localPosition = Vector3.up * 0.5f;
			particleSpawnPoint = gameObject.transform;
		}
	}

	private void Start()
	{
		if (spawnParticlesOnStart && spawnParticles)
		{
			SpawnBounceParticles();
			if (continuousParticles)
			{
				nextParticleSpawnTime = Time.time + particleSpawnInterval;
			}
		}
	}

	private void Update()
	{
		if (isSquashing)
		{
			UpdateSquashEffect();
		}
		if (continuousParticles && spawnParticles && Time.time >= nextParticleSpawnTime)
		{
			SpawnBounceParticles();
			nextParticleSpawnTime = Time.time + particleSpawnInterval;
		}
	}

	private void OnTriggerEnter2D(Collider2D collision)
	{
		if (collision.gameObject.CompareTag("Player"))
		{
			Rigidbody2D component = collision.gameObject.GetComponent<Rigidbody2D>();
			if (component != null && CanBounce(component))
			{
				LaunchPlayer(collision.gameObject);
			}
		}
	}

	private bool CanBounce(Rigidbody2D playerRb)
	{
		if (!requireDownwardVelocity)
		{
			return true;
		}
		return playerRb.linearVelocity.y <= minDownwardVelocity;
	}

	private void LaunchPlayer(GameObject playerObject)
	{
		PlayerMovementController component = playerObject.GetComponent<PlayerMovementController>();
		PlayerStateMachine component2 = playerObject.GetComponent<PlayerStateMachine>();
		if (component != null)
		{
			component.ExternalLaunch(springForce);
		}
		if (component2 != null)
		{
			component2.ChangeState<PlayerJumpState>();
		}
		TriggerBounceEffects();
	}

	private void TriggerBounceEffects()
	{
		StartSquashEffect();
		PlayBounceAnimation();
		PlayBounceSound();
		SpawnBounceParticles();
	}

	private void StartSquashEffect()
	{
		if (visualTransform != null)
		{
			squashTimer = 0f;
			isSquashing = true;
		}
	}

	private void UpdateSquashEffect()
	{
		squashTimer += Time.deltaTime;
		float num = squashTimer / squashDuration;
		if (num >= 1f)
		{
			visualTransform.localScale = originalScale;
			isSquashing = false;
			return;
		}
		float t = squashCurve.Evaluate(num);
		float num2 = Mathf.Lerp(squashAmount, 1f, t);
		float num3 = Mathf.Lerp(1f + (1f - squashAmount) * 0.5f, 1f, t);
		visualTransform.localScale = new Vector3(originalScale.x * num3, originalScale.y * num2, originalScale.z);
	}

	private void PlayBounceAnimation()
	{
		if (animator != null && !string.IsNullOrEmpty(bounceAnimationTrigger))
		{
			animator.SetTrigger(bounceAnimationTrigger);
		}
	}

	private void PlayBounceSound()
	{
		if (SFXManager.Instance != null)
		{
			SFXManager.Instance.Play("ENV_Bounce", base.transform.position);
		}
		else if (audioSource != null && bounceSound != null)
		{
			audioSource.PlayOneShot(bounceSound);
		}
	}

	private void SpawnBounceParticles()
	{
		if (!spawnParticles)
		{
			return;
		}
		Vector3 position = ((particleSpawnPoint != null) ? particleSpawnPoint.position : base.transform.position);
		Quaternion rotation = ((particleSpawnPoint != null) ? particleSpawnPoint.rotation : Quaternion.identity);
		if (useObjectPooling)
		{
			if (!string.IsNullOrEmpty(poolTag))
			{
				SpawnFromPool(position, rotation);
			}
		}
		else if (!(bounceParticlesPrefab == null))
		{
			SpawnDirectly(position, rotation);
		}
	}

	private void SpawnFromPool(Vector3 position, Quaternion rotation)
	{
		if (!(ObjectPool.Instance == null))
		{
			ObjectPool.Instance.SpawnFromPool(poolTag, position, rotation);
		}
	}

	private void SpawnDirectly(Vector3 position, Quaternion rotation)
	{
		ParticleSystem particleSystem = Object.Instantiate(bounceParticlesPrefab, position, rotation);
		particleSystem.Play();
		ParticleSystem.MainModule main = particleSystem.main;
		if (!main.loop)
		{
			Object.Destroy(particleSystem.gameObject, main.duration + main.startLifetime.constantMax);
		}
	}

	private float CalculateJumpHeight()
	{
		float num = Mathf.Abs(Physics2D.gravity.y);
		if (num < 0.01f)
		{
			num = 9.81f;
		}
		return springForce * springForce / (2f * num);
	}

	private void OnDrawGizmosSelected()
	{
		Vector3 position = base.transform.position;
		float num = CalculateJumpHeight();
		Vector3 vector = position + Vector3.up * num;
		Gizmos.color = Color.green;
		Gizmos.DrawWireSphere(position, 0.3f);
		Gizmos.color = Color.yellow;
		Gizmos.DrawLine(position, vector);
		Gizmos.color = Color.cyan;
		Gizmos.DrawWireSphere(vector, 0.5f);
		Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
		for (int i = 1; i <= 10; i++)
		{
			float t = (float)i / 10f;
			Gizmos.DrawWireSphere(Vector3.Lerp(position, vector, t), 0.15f);
		}
		if (particleSpawnPoint != null)
		{
			Gizmos.color = Color.magenta;
			Gizmos.DrawWireSphere(particleSpawnPoint.position, 0.2f);
			Gizmos.DrawLine(base.transform.position, particleSpawnPoint.position);
		}
	}
}
