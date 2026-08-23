using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class CoinPickup : MonoBehaviour
{
	private enum CoinState
	{
		Idle,
		Rising,
		Hovering,
		FadingToFollow,
		Following
	}

	[Header("Follow Settings")]
	[SerializeField]
	private Vector3 followOffset = new Vector3(-0.5f, 0.5f, 0f);

	[SerializeField]
	private float followSpeed = 5f;

	[SerializeField]
	private float transparency = 0.5f;

	[Header("Bobbing Animation")]
	[SerializeField]
	private bool enableBobbing = true;

	[SerializeField]
	private float bobbingSpeed = 3f;

	[SerializeField]
	private float bobbingAmount = 0.1f;

	[Header("Pickup Animation")]
	[SerializeField]
	private float riseHeight = 1.5f;

	[SerializeField]
	private float riseSpeed = 8f;

	[SerializeField]
	private float hoverDuration = 1f;

	[SerializeField]
	private float fadeDuration = 0.5f;

	[Header("Visual Effects")]
	[SerializeField]
	private string collectEffectPoolTag = "CoinCollect";

	private CoinState currentState;

	private Transform playerTransform;

	private SpriteRenderer spriteRenderer;

	private PlayerMovementController playerMovement;

	private Vector3 velocity = Vector3.zero;

	private Vector3 initialPosition;

	private Vector3 riseTargetPosition;

	private float hoverTimer;

	private float fadeTimer;

	private float initialAlpha = 1f;

	public bool IsCollected => currentState != CoinState.Idle;

	private void Start()
	{
		GetComponent<Collider2D>().isTrigger = true;
		spriteRenderer = GetComponent<SpriteRenderer>();
		initialPosition = base.transform.position;
		initialAlpha = spriteRenderer.color.a;
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (currentState == CoinState.Idle && other.CompareTag("Player"))
		{
			playerTransform = other.transform;
			playerMovement = other.GetComponent<PlayerMovementController>();
			GetComponent<Collider2D>().enabled = false;
			riseTargetPosition = playerTransform.position + Vector3.up * riseHeight;
			currentState = CoinState.Rising;
			OnCoinCollected();
		}
	}

	private void Update()
	{
		switch (currentState)
		{
		case CoinState.Idle:
			UpdateIdleState();
			break;
		case CoinState.Rising:
			UpdateRisingState();
			break;
		case CoinState.Hovering:
			UpdateHoveringState();
			break;
		case CoinState.FadingToFollow:
			UpdateFadingState();
			break;
		case CoinState.Following:
			UpdateFollowingState();
			break;
		}
	}

	private void UpdateIdleState()
	{
		if (enableBobbing)
		{
			float num = Mathf.Sin(Time.time * bobbingSpeed) * bobbingAmount;
			base.transform.position = new Vector3(initialPosition.x, initialPosition.y + num, initialPosition.z);
		}
	}

	private void UpdateRisingState()
	{
		if (!(playerTransform == null))
		{
			riseTargetPosition = playerTransform.position + Vector3.up * riseHeight;
			base.transform.position = Vector3.MoveTowards(base.transform.position, riseTargetPosition, riseSpeed * Time.deltaTime);
			if (Vector3.Distance(base.transform.position, riseTargetPosition) < 0.1f)
			{
				currentState = CoinState.Hovering;
				hoverTimer = 0f;
			}
		}
	}

	private void UpdateHoveringState()
	{
		if (!(playerTransform == null))
		{
			riseTargetPosition = playerTransform.position + Vector3.up * riseHeight;
			base.transform.position = riseTargetPosition;
			if (enableBobbing)
			{
				float num = Mathf.Sin(Time.time * bobbingSpeed) * bobbingAmount;
				base.transform.position = new Vector3(base.transform.position.x, base.transform.position.y + num, base.transform.position.z);
			}
			hoverTimer += Time.deltaTime;
			if (hoverTimer >= hoverDuration)
			{
				currentState = CoinState.FadingToFollow;
				fadeTimer = 0f;
			}
		}
	}

	private void UpdateFadingState()
	{
		if (!(playerTransform == null))
		{
			fadeTimer += Time.deltaTime;
			float num = Mathf.Clamp01(fadeTimer / fadeDuration);
			float a = Mathf.Lerp(initialAlpha, transparency, num);
			Color color = spriteRenderer.color;
			color.a = a;
			spriteRenderer.color = color;
			Vector3 target = CalculateFollowPosition();
			if (enableBobbing)
			{
				float num2 = Mathf.Sin(Time.time * bobbingSpeed) * bobbingAmount;
				target.y += num2;
			}
			base.transform.position = Vector3.SmoothDamp(base.transform.position, target, ref velocity, 1f / followSpeed);
			if (num >= 1f)
			{
				currentState = CoinState.Following;
			}
		}
	}

	private void UpdateFollowingState()
	{
		if (!(playerTransform == null))
		{
			Vector3 target = CalculateFollowPosition();
			if (enableBobbing)
			{
				float num = Mathf.Sin(Time.time * bobbingSpeed) * bobbingAmount;
				target.y += num;
			}
			base.transform.position = Vector3.SmoothDamp(base.transform.position, target, ref velocity, 1f / followSpeed);
		}
	}

	private Vector3 CalculateFollowPosition()
	{
		Vector3 vector = followOffset;
		if (playerMovement != null && !playerMovement.IsFacingRight)
		{
			vector.x = 0f - vector.x;
		}
		return playerTransform.position + vector;
	}

	private void OnCoinCollected()
	{
		SFXManager.Instance?.Play("ITEM_CoinPickup", base.transform.position);
		if (VFXManager.Instance != null)
		{
			VFXManager.Instance.PlayParticleEffect(collectEffectPoolTag, base.transform.position);
		}
		if (LevelStatsManager.Instance != null)
		{
			LevelStatsManager.Instance.RegisterSecretCoinCollected();
		}
	}
}
