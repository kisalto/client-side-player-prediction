using System;
using System.Collections;
using UnityEngine;

public class LiquidCourage : MonoBehaviour
{
	[Header("Bobbing Animation")]
	[SerializeField]
	private float bobbingSpeed = 2f;

	[SerializeField]
	private float bobbingAmount = 0.3f;

	[Header("Pickup Animation")]
	[SerializeField]
	private float pickupMoveUpDistance = 1f;

	[SerializeField]
	private float pickupMoveUpDuration = 0.2f;

	[SerializeField]
	private float followPlayerDuration = 0.5f;

	[SerializeField]
	private float pickupMoveDownDuration = 0.15f;

	[SerializeField]
	private float pickupShrinkDuration = 0.2f;

	[SerializeField]
	private float followSmoothSpeed = 10f;

	[Header("Effect Shrink Settings")]
	[SerializeField]
	private Transform effectsTransform;

	[SerializeField]
	private float effectShrinkDuration = 0.3f;

	[SerializeField]
	private float effectShrinkDelay = 0.1f;

	[Header("Respawn Settings")]
	[SerializeField]
	private float respawnDelay = 3f;

	[SerializeField]
	private float respawnScaleUpDuration = 0.5f;

	private Vector3 startPosition;

	private Vector3 originalScale;

	private Vector3 originalEffectScale;

	private float bobbingTimer;

	private bool isPickedUp;

	public static event Action OnDrunk;

	private void Start()
	{
		startPosition = base.transform.position;
		originalScale = base.transform.localScale;
		if (effectsTransform == null)
		{
			Transform transform = base.transform.Find("Effects");
			if (transform != null)
			{
				effectsTransform = transform;
			}
		}
		if (effectsTransform != null)
		{
			originalEffectScale = effectsTransform.localScale;
		}
	}

	private void Update()
	{
		if (!isPickedUp)
		{
			bobbingTimer += Time.deltaTime * bobbingSpeed;
			float y = Mathf.Sin(bobbingTimer) * bobbingAmount;
			base.transform.position = startPosition + new Vector3(0f, y, 0f);
		}
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (other.CompareTag("Player") && !isPickedUp)
		{
			LiquidCourageEffect liquidCourageEffect = other.GetComponent<LiquidCourageEffect>();
			if (liquidCourageEffect == null)
			{
				liquidCourageEffect = other.gameObject.AddComponent<LiquidCourageEffect>();
			}
			isPickedUp = true;
			SFXManager.Instance?.Play("ITEM_LCPickup", base.transform.position);
			OnDrunk?.Invoke();
			GetComponent<Collider2D>().enabled = false;
			StartCoroutine(PickupAnimation(other.transform, liquidCourageEffect));
		}
	}

	private IEnumerator PickupAnimation(Transform playerTransform, LiquidCourageEffect effectManager)
	{
		if (effectsTransform != null)
		{
			StartCoroutine(ShrinkEffectCoroutine());
		}
		Vector3 currentPosition = base.transform.position;
		Vector3 targetUpPosition = currentPosition + Vector3.up * pickupMoveUpDistance;
		float elapsedTime = 0f;
		while (elapsedTime < pickupMoveUpDuration)
		{
			elapsedTime += Time.deltaTime;
			float t = elapsedTime / pickupMoveUpDuration;
			base.transform.position = Vector3.Lerp(currentPosition, targetUpPosition, t);
			yield return null;
		}
		elapsedTime = 0f;
		while (elapsedTime < followPlayerDuration)
		{
			elapsedTime += Time.deltaTime;
			Vector3 b = playerTransform.position + Vector3.up * pickupMoveUpDistance;
			base.transform.position = Vector3.Lerp(base.transform.position, b, followSmoothSpeed * Time.deltaTime);
			yield return null;
		}
		currentPosition = base.transform.position;
		Vector3 targetDownPosition = playerTransform.position;
		elapsedTime = 0f;
		while (elapsedTime < pickupMoveDownDuration)
		{
			elapsedTime += Time.deltaTime;
			float t2 = elapsedTime / pickupMoveDownDuration;
			targetDownPosition = playerTransform.position;
			base.transform.position = Vector3.Lerp(currentPosition, targetDownPosition, t2);
			yield return null;
		}
		base.transform.position = targetDownPosition;
		Vector3 currentScale = base.transform.localScale;
		elapsedTime = 0f;
		while (elapsedTime < pickupShrinkDuration)
		{
			elapsedTime += Time.deltaTime;
			float t3 = elapsedTime / pickupShrinkDuration;
			base.transform.position = playerTransform.position;
			base.transform.localScale = Vector3.Lerp(currentScale, Vector3.zero, t3);
			yield return null;
		}
		if (effectManager.IsEffectActive())
		{
			effectManager.ResetDuration();
		}
		else
		{
			effectManager.ActivateEffect();
		}
		StartCoroutine(RespawnAfterDelay());
	}

	private IEnumerator ShrinkEffectCoroutine()
	{
		if (!(effectsTransform == null))
		{
			yield return new WaitForSeconds(effectShrinkDelay);
			Vector3 startScale = effectsTransform.localScale;
			float elapsedTime = 0f;
			while (elapsedTime < effectShrinkDuration)
			{
				elapsedTime += Time.deltaTime;
				float t = elapsedTime / effectShrinkDuration;
				float t2 = Mathf.SmoothStep(0f, 1f, t);
				effectsTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, t2);
				yield return null;
			}
			effectsTransform.localScale = Vector3.zero;
		}
	}

	private IEnumerator RespawnAfterDelay()
	{
		yield return new WaitForSeconds(respawnDelay);
		base.transform.position = startPosition;
		base.transform.localScale = Vector3.zero;
		if (effectsTransform != null)
		{
			effectsTransform.localScale = Vector3.zero;
		}
		float elapsedTime = 0f;
		while (elapsedTime < respawnScaleUpDuration)
		{
			elapsedTime += Time.deltaTime;
			float t = elapsedTime / respawnScaleUpDuration;
			float t2 = Mathf.SmoothStep(0f, 1f, t);
			base.transform.localScale = Vector3.Lerp(Vector3.zero, originalScale, t2);
			if (effectsTransform != null)
			{
				effectsTransform.localScale = Vector3.Lerp(Vector3.zero, originalEffectScale, t2);
			}
			yield return null;
		}
		base.transform.localScale = originalScale;
		if (effectsTransform != null)
		{
			effectsTransform.localScale = originalEffectScale;
		}
		GetComponent<Collider2D>().enabled = true;
		isPickedUp = false;
	}
}
