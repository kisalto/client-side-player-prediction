using System;
using System.Collections;
using UnityEngine;

public class PlayerVisualEffectsController : MonoBehaviour
{
	private PlayerData data;

	[SerializeField]
	private SpriteRenderer playerSpriteRenderer;

	private PlayerHealthController healthController;

	private PlayerStateMachine stateMachine;

	private Coroutine blinkCoroutine;

	private Coroutine shakeCoroutine;

	public event Action OnDeflectShakeEnd;

	private void Awake()
	{
		healthController = GetComponent<PlayerHealthController>();
		stateMachine = GetComponent<PlayerStateMachine>();
		if (playerSpriteRenderer == null)
		{
			playerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
		}
	}

	public void Init(PlayerData playerData)
	{
		data = playerData;
	}

	private void OnEnable()
	{
		if (healthController != null)
		{
			healthController.OnInvincibilityEnd += StopBlinking;
			healthController.OnHurt += TriggerCameraShake;
		}
		if (stateMachine != null)
		{
			stateMachine.OnHurtEnd += StartBlinking;
		}
	}

	private void OnDisable()
	{
		if (healthController != null)
		{
			healthController.OnInvincibilityEnd -= StopBlinking;
			healthController.OnHurt -= TriggerCameraShake;
		}
		if (stateMachine != null)
		{
			stateMachine.OnHurtEnd -= StartBlinking;
		}
	}

	private void StartBlinking()
	{
		if (healthController.IsInvincible)
		{
			if (blinkCoroutine != null)
			{
				StopCoroutine(blinkCoroutine);
			}
			blinkCoroutine = StartCoroutine(BlinkRoutine());
		}
	}

	private void StopBlinking()
	{
		if (blinkCoroutine != null)
		{
			StopCoroutine(blinkCoroutine);
			blinkCoroutine = null;
		}
		if (playerSpriteRenderer != null)
		{
			playerSpriteRenderer.enabled = true;
		}
	}

	private IEnumerator BlinkRoutine()
	{
		while (healthController.IsInvincible)
		{
			playerSpriteRenderer.enabled = !playerSpriteRenderer.enabled;
			yield return new WaitForSeconds(data.blinkInterval);
		}
		playerSpriteRenderer.enabled = true;
	}

	private void TriggerCameraShake()
	{
		CameraShakeManager.Instance.ShakeCamera(data.cameraShakeAmplitude, data.cameraShakeFrequency, data.cameraShakeDuration);
	}

	public void StartHurtShake()
	{
		if (!(playerSpriteRenderer == null))
		{
			if (shakeCoroutine != null)
			{
				StopCoroutine(shakeCoroutine);
			}
			shakeCoroutine = StartCoroutine(HurtShakeRoutine());
		}
	}

	public void OnDeflectedShake()
	{
		if (!(playerSpriteRenderer == null))
		{
			if (shakeCoroutine != null)
			{
				StopCoroutine(shakeCoroutine);
			}
			if (!(data == null))
			{
				shakeCoroutine = StartCoroutine(DeflectShakeRoutine(data.deflectShakeStrength, data.deflectShakeDuration));
			}
		}
	}

	private IEnumerator DeflectShakeRoutine(float strength, float duration)
	{
		Transform spriteTransform = playerSpriteRenderer.transform;
		Vector3 currentOffset = Vector3.zero;
		float elapsed = 0f;
		while (elapsed < duration)
		{
			spriteTransform.localPosition -= currentOffset;
			float num = 1f - elapsed / duration;
			currentOffset = new Vector3(UnityEngine.Random.Range(-1f, 1f) * strength * num, UnityEngine.Random.Range(-1f, 1f) * strength * num, 0f);
			spriteTransform.localPosition += currentOffset;
			elapsed += Time.deltaTime;
			yield return null;
		}
		spriteTransform.localPosition -= currentOffset;
		shakeCoroutine = null;
		OnDeflectShakeEnd?.Invoke();
	}

	private IEnumerator HurtShakeRoutine()
	{
		Transform spriteTransform = playerSpriteRenderer.transform;
		Vector3 currentOffset = Vector3.zero;
		float elapsed = 0f;
		while (elapsed < data.hurtShakeDuration)
		{
			spriteTransform.localPosition -= currentOffset;
			float num = 1f - elapsed / data.hurtShakeDuration;
			currentOffset = new Vector3(UnityEngine.Random.Range(-1f, 1f) * data.hurtShakeStrength * num, UnityEngine.Random.Range(-1f, 1f) * data.hurtShakeStrength * num, 0f);
			spriteTransform.localPosition += currentOffset;
			elapsed += Time.deltaTime;
			yield return null;
		}
		spriteTransform.localPosition -= currentOffset;
		shakeCoroutine = null;
	}
}
