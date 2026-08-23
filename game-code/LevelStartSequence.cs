using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class LevelStartSequence : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private Transform spawnPoint;

	[SerializeField]
	private GameObject portalPrefab;

	[SerializeField]
	private PlayerStateMachine player;

	[SerializeField]
	private CinemachineCamera cinemachineCamera;

	[Header("Portal Open")]
	[SerializeField]
	private float portalOpenDuration = 0.45f;

	[SerializeField]
	private float portalBounceOvershoot = 1.25f;

	[SerializeField]
	private float portalBounceDamping = 0.35f;

	[Header("Player Emerge")]
	[SerializeField]
	private float emergeDuration = 0.8f;

	[SerializeField]
	private float rotationSpeed = 720f;

	[SerializeField]
	private float rotationSnapDuration = 0.25f;

	[SerializeField]
	private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	[Header("Portal Close")]
	[SerializeField]
	private float delayBeforeClose = 0.25f;

	[SerializeField]
	private float portalCloseDuration = 0.45f;

	[SerializeField]
	private AnimationCurve portalCloseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	private Rigidbody2D playerRb;

	private BoxCollider2D playerCol;

	private SpriteRenderer playerSprite;

	private Animator playerAnimator;

	private float originalGravityScale;

	private void Start()
	{
		playerRb = player.GetComponent<Rigidbody2D>();
		playerCol = player.GetComponent<BoxCollider2D>();
		playerSprite = player.GetComponentInChildren<SpriteRenderer>();
		playerAnimator = player.GetComponentInChildren<Animator>();
		originalGravityScale = playerRb.gravityScale;
		StartCoroutine(PlaySequence());
	}

	private IEnumerator PlaySequence()
	{
		FreezePlayer();
		player.transform.position = spawnPoint.position;
		player.transform.rotation = Quaternion.identity;
		player.transform.localScale = Vector3.zero;
		playerSprite.enabled = false;
		GameObject portalGO = UnityEngine.Object.Instantiate(portalPrefab, spawnPoint.position, Quaternion.identity);
		SFXManager.Instance?.Play("LVL_PortalOpen", spawnPoint.position);
		BossPortal component = portalGO.GetComponent<BossPortal>();
		if (component != null)
		{
			component.enabled = false;
		}
		portalGO.transform.localScale = Vector3.zero;
		yield return StartCoroutine(ScaleTransform(portalGO.transform, Vector3.zero, Vector3.one, portalOpenDuration, portalBounceOvershoot, portalBounceDamping));
		playerSprite.enabled = true;
		yield return StartCoroutine(EmergeSequence());
		yield return new WaitForSeconds(delayBeforeClose);
		yield return StartCoroutine(ShrinkPortal(portalGO.transform));
		SFXManager.Instance?.Play("LVL_PortalClose", spawnPoint.position);
		UnityEngine.Object.Destroy(portalGO);
		UnfreezePlayer();
	}

	private IEnumerator ShrinkPortal(Transform portal)
	{
		Vector3 startScale = portal.localScale;
		float elapsed = 0f;
		while (elapsed < portalCloseDuration)
		{
			elapsed += Time.deltaTime;
			float time = Mathf.Clamp01(elapsed / portalCloseDuration);
			float t = portalCloseCurve.Evaluate(time);
			portal.localScale = Vector3.LerpUnclamped(startScale, Vector3.zero, t);
			yield return null;
		}
		portal.localScale = Vector3.zero;
	}

	private IEnumerator EmergeSequence()
	{
		Vector3 targetScale = Vector3.one;
		float elapsedTime = 0f;
		float currentZ = 0f;
		float freeSpinEnd = emergeDuration - rotationSnapDuration;
		while (elapsedTime < freeSpinEnd)
		{
			elapsedTime += Time.deltaTime;
			float time = elapsedTime / emergeDuration;
			player.transform.localScale = targetScale * scaleCurve.Evaluate(time);
			currentZ += rotationSpeed * Time.deltaTime;
			player.transform.rotation = Quaternion.Euler(0f, 0f, currentZ);
			yield return null;
		}
		float snapStartZ = currentZ;
		float nextFullRot = Mathf.Ceil(currentZ / 360f) * 360f;
		float snapElapsed = 0f;
		while (snapElapsed < rotationSnapDuration)
		{
			snapElapsed += Time.deltaTime;
			elapsedTime += Time.deltaTime;
			float time2 = Mathf.Clamp01(elapsedTime / emergeDuration);
			player.transform.localScale = targetScale * scaleCurve.Evaluate(time2);
			float num = Mathf.Clamp01(snapElapsed / rotationSnapDuration);
			float t = 1f - Mathf.Pow(1f - num, 2f);
			float z = Mathf.Lerp(snapStartZ, nextFullRot, t);
			player.transform.rotation = Quaternion.Euler(0f, 0f, z);
			yield return null;
		}
		player.transform.localScale = targetScale;
		player.transform.rotation = Quaternion.identity;
	}

	private void FreezePlayer()
	{
		player.enabled = false;
		playerRb.gravityScale = 0f;
		playerRb.bodyType = RigidbodyType2D.Kinematic;
		playerRb.linearVelocity = Vector2.zero;
		playerRb.angularVelocity = 0f;
		if (playerCol != null)
		{
			playerCol.enabled = false;
		}
		if (playerAnimator != null)
		{
			playerAnimator.enabled = false;
		}
	}

	private void UnfreezePlayer()
	{
		player.transform.rotation = Quaternion.identity;
		player.transform.localScale = Vector3.one;
		playerRb.bodyType = RigidbodyType2D.Dynamic;
		playerRb.gravityScale = originalGravityScale;
		playerRb.linearVelocity = Vector2.zero;
		playerRb.angularVelocity = 0f;
		if (playerCol != null)
		{
			playerCol.enabled = true;
		}
		if (playerAnimator != null)
		{
			playerAnimator.enabled = true;
		}
		player.enabled = true;
		player.ChangeState<PlayerIdleState>();
	}

	private IEnumerator ScaleTransform(Transform target, Vector3 from, Vector3 to, float duration, float overshoot, float damping)
	{
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			float t2 = SpringCurve(t, overshoot, damping);
			target.localScale = Vector3.LerpUnclamped(from, to, t2);
			yield return null;
		}
		target.localScale = to;
	}

	private float SpringCurve(float t, float overshoot, float damping)
	{
		return 1f - Mathf.Exp((0f - damping) * 10f * t) * Mathf.Cos(overshoot * MathF.PI * 2f * t);
	}
}
