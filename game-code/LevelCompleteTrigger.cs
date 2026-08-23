using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LevelCompleteTrigger : MonoBehaviour
{
	[Header("Trigger Settings")]
	[SerializeField]
	private bool requireAllEnemiesKilled;

	[SerializeField]
	private bool autoCompleteOnTrigger = true;

	[Header("Portal Animation")]
	[SerializeField]
	private bool usePortalAnimation = true;

	[SerializeField]
	private float suckDuration = 1.5f;

	[SerializeField]
	private float rotationSpeed = 720f;

	[SerializeField]
	private AnimationCurve suckCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	[SerializeField]
	private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

	[Header("Coin Portal Animation")]
	[SerializeField]
	private float coinDelayBeforeSuck = 0.5f;

	[SerializeField]
	private float coinSuckDuration = 1f;

	[SerializeField]
	private float coinRiseHeight = 1f;

	[SerializeField]
	private AnimationCurve coinSuckCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	[Header("Visual Feedback")]
	[SerializeField]
	private string completionEffectPoolTag = "LevelComplete";

	[SerializeField]
	private ParticleSystem portalParticles;

	private bool hasTriggered;

	private void Start()
	{
		GetComponent<Collider2D>().isTrigger = true;
		if (portalParticles != null)
		{
			portalParticles.Play();
		}
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (!hasTriggered && other.CompareTag("Player") && (!requireAllEnemiesKilled || !(LevelStatsManager.Instance != null) || LevelStatsManager.Instance.GetCurrentProgress().AllEnemiesDefeated))
		{
			hasTriggered = true;
			if (usePortalAnimation)
			{
				StartCoroutine(PortalSuckAnimation(other.gameObject));
			}
			else
			{
				CompleteLevel();
			}
		}
	}

	private IEnumerator PortalSuckAnimation(GameObject player)
	{
		PlayerMovementController component = player.GetComponent<PlayerMovementController>();
		PlayerStateMachine component2 = player.GetComponent<PlayerStateMachine>();
		Rigidbody2D component3 = player.GetComponent<Rigidbody2D>();
		if (component != null)
		{
			component.enabled = false;
		}
		if (component2 != null)
		{
			component2.enabled = false;
		}
		LiquidCourageEffect component4 = player.GetComponent<LiquidCourageEffect>();
		if (component4 != null)
		{
			component4.SuppressScalePulse();
		}
		if (component3 != null)
		{
			component3.linearVelocity = Vector2.zero;
			component3.angularVelocity = 0f;
			component3.bodyType = RigidbodyType2D.Kinematic;
		}
		CameraController cameraController = ((Camera.main != null) ? Camera.main.GetComponent<CameraController>() : null);
		if (cameraController != null)
		{
			cameraController.Freeze();
		}
		SFXManager.Instance?.Play("LVL_PortalSuck", base.transform.position);
		GameObject gameObject = FindCollectedCoin();
		Coroutine coinCoroutine = null;
		if (gameObject != null)
		{
			coinCoroutine = StartCoroutine(CoinPortalAnimation(gameObject));
		}
		Vector3 startPosition = player.transform.position;
		Vector3 targetPosition = base.transform.position;
		Vector3 startScale = player.transform.localScale;
		float elapsedTime = 0f;
		while (elapsedTime < suckDuration)
		{
			elapsedTime += Time.deltaTime;
			float time = elapsedTime / suckDuration;
			player.transform.position = Vector3.Lerp(startPosition, targetPosition, suckCurve.Evaluate(time));
			player.transform.localScale = startScale * scaleCurve.Evaluate(time);
			player.transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
			yield return null;
		}
		player.transform.position = targetPosition;
		player.transform.localScale = Vector3.zero;
		SpriteRenderer[] componentsInChildren = player.GetComponentsInChildren<SpriteRenderer>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].enabled = false;
		}
		if (coinCoroutine != null)
		{
			yield return coinCoroutine;
		}
		CompleteLevel();
	}

	private IEnumerator CoinPortalAnimation(GameObject coin)
	{
		CoinPickup component = coin.GetComponent<CoinPickup>();
		if (component != null)
		{
			component.enabled = false;
		}
		Vector3 startPosition = coin.transform.position;
		Vector3 abovePortalPosition = base.transform.position + Vector3.up * coinRiseHeight;
		Vector3 targetPosition = base.transform.position;
		float moveToAboveDuration = 0.3f;
		float elapsed = 0f;
		while (elapsed < moveToAboveDuration)
		{
			elapsed += Time.deltaTime;
			coin.transform.position = Vector3.Lerp(startPosition, abovePortalPosition, elapsed / moveToAboveDuration);
			yield return null;
		}
		coin.transform.position = abovePortalPosition;
		yield return new WaitForSeconds(coinDelayBeforeSuck);
		elapsed = 0f;
		Vector3 coinStartScale = coin.transform.localScale;
		while (elapsed < coinSuckDuration)
		{
			elapsed += Time.deltaTime;
			float num = elapsed / coinSuckDuration;
			coin.transform.position = Vector3.Lerp(abovePortalPosition, targetPosition, coinSuckCurve.Evaluate(num));
			coin.transform.localScale = coinStartScale * (1f - num);
			coin.transform.Rotate(0f, 0f, rotationSpeed * 0.5f * Time.deltaTime);
			yield return null;
		}
		coin.transform.position = targetPosition;
		coin.transform.localScale = Vector3.zero;
		if (VFXManager.Instance != null && !string.IsNullOrEmpty(completionEffectPoolTag))
		{
			VFXManager.Instance.PlayParticleEffect(completionEffectPoolTag, targetPosition);
		}
	}

	private GameObject FindCollectedCoin()
	{
		CoinPickup[] array = Object.FindObjectsByType<CoinPickup>(FindObjectsSortMode.None);
		foreach (CoinPickup coinPickup in array)
		{
			if (coinPickup.IsCollected)
			{
				return coinPickup.gameObject;
			}
		}
		return null;
	}

	private void CompleteLevel()
	{
		if (VFXManager.Instance != null && !string.IsNullOrEmpty(completionEffectPoolTag))
		{
			VFXManager.Instance.PlayParticleEffect(completionEffectPoolTag, base.transform.position);
		}
		if (LevelStatsManager.Instance != null)
		{
			LevelStatsManager.Instance.CompleteLevel();
		}
		SFXManager.Instance?.Play2D("LVL_Complete");
	}
}
