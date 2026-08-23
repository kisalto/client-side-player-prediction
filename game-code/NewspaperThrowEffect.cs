using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class NewspaperThrowEffect : MonoBehaviour
{
	[Header("Animation Settings")]
	[SerializeField]
	private float fallbackThrowDuration = 1.2f;

	[SerializeField]
	private float spinRotations = 2f;

	[SerializeField]
	private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	[SerializeField]
	private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f);

	[SerializeField]
	private AnimationCurve rotationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

	[Header("Sound")]
	[SerializeField]
	private AudioClip throwClip;

	[SerializeField]
	[Range(0f, 1f)]
	private float throwVolume = 0.8f;

	[Tooltip("Delay in seconds before the throw sound plays after the animation starts.")]
	[SerializeField]
	private float throwSoundDelay;

	[Header("Start Position")]
	[SerializeField]
	private Vector2 startOffset = new Vector2(-800f, 600f);

	[SerializeField]
	private float startRotation = -45f;

	[Header("Settling Effect")]
	[SerializeField]
	private float settleBounceDuration = 0.3f;

	[SerializeField]
	private float settleRotationAmount = 5f;

	[SerializeField]
	private AnimationCurve settleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

	private RectTransform rectTransform;

	private Vector2 originalPosition;

	private Vector2 originalSizeDelta;

	private Vector3 originalScale;

	private Quaternion originalRotation;

	private CanvasGroup canvasGroup;

	private Image imageComponent;

	private AudioSource audioSource;

	private bool isInitialized;

	private Coroutine delayedSoundCoroutine;

	private void Awake()
	{
		rectTransform = GetComponent<RectTransform>();
		if (rectTransform == null)
		{
			Debug.LogError("NewspaperThrowEffect requires a RectTransform component!");
			base.enabled = false;
			return;
		}
		canvasGroup = GetComponent<CanvasGroup>();
		if (canvasGroup == null)
		{
			canvasGroup = base.gameObject.AddComponent<CanvasGroup>();
		}
		imageComponent = GetComponent<Image>();
		if (imageComponent != null && imageComponent.preserveAspect)
		{
			Debug.Log("NewspaperThrowEffect: Disabling preserveAspect to prevent layout issues.");
			imageComponent.preserveAspect = false;
		}
		audioSource = GetComponent<AudioSource>();
		if (audioSource == null)
		{
			audioSource = base.gameObject.AddComponent<AudioSource>();
		}
		audioSource.playOnAwake = false;
		audioSource.spatialBlend = 0f;
	}

	private void CaptureOriginalState()
	{
		if (!isInitialized && rectTransform != null)
		{
			originalPosition = rectTransform.anchoredPosition;
			originalSizeDelta = rectTransform.sizeDelta;
			originalScale = rectTransform.localScale;
			originalRotation = rectTransform.localRotation;
			isInitialized = true;
		}
	}

	private float GetThrowDuration()
	{
		if (throwClip != null)
		{
			return throwClip.length;
		}
		return fallbackThrowDuration;
	}

	public IEnumerator PlayThrowAnimation()
	{
		CaptureOriginalState();
		float throwDuration = GetThrowDuration();
		canvasGroup.alpha = 0f;
		Vector2 startPosition = originalPosition + startOffset;
		rectTransform.anchoredPosition = startPosition;
		rectTransform.localScale = Vector3.zero;
		rectTransform.localRotation = Quaternion.Euler(0f, 0f, startRotation);
		if (throwClip != null && audioSource != null)
		{
			if (throwSoundDelay <= 0f)
			{
				audioSource.clip = throwClip;
				audioSource.volume = throwVolume;
				audioSource.Play();
			}
			else
			{
				delayedSoundCoroutine = StartCoroutine(PlaySoundAfterDelay());
			}
		}
		float elapsed = 0f;
		while (elapsed < throwDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			float num = elapsed / throwDuration;
			float t = movementCurve.Evaluate(num);
			rectTransform.anchoredPosition = Vector2.Lerp(startPosition, originalPosition, t);
			float num2 = scaleCurve.Evaluate(num);
			rectTransform.localScale = originalScale * num2;
			float t2 = rotationCurve.Evaluate(num);
			float z = Mathf.Lerp(startRotation, spinRotations * 360f, t2);
			rectTransform.localRotation = Quaternion.Euler(0f, 0f, z);
			canvasGroup.alpha = Mathf.Clamp01(num * 2f);
			yield return null;
		}
		RestoreOriginalState();
		yield return StartCoroutine(PlaySettleAnimation());
	}

	private IEnumerator PlaySoundAfterDelay()
	{
		yield return new WaitForSecondsRealtime(throwSoundDelay);
		if (audioSource != null && throwClip != null)
		{
			audioSource.clip = throwClip;
			audioSource.volume = throwVolume;
			audioSource.Play();
		}
		delayedSoundCoroutine = null;
	}

	private IEnumerator PlaySettleAnimation()
	{
		float elapsed = 0f;
		while (elapsed < settleBounceDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			float time = elapsed / settleBounceDuration;
			float z = settleCurve.Evaluate(time) * settleRotationAmount;
			rectTransform.localRotation = Quaternion.Euler(0f, 0f, z) * originalRotation;
			yield return null;
		}
		RestoreOriginalState();
	}

	private void RestoreOriginalState()
	{
		if (isInitialized && rectTransform != null)
		{
			rectTransform.anchoredPosition = originalPosition;
			rectTransform.sizeDelta = originalSizeDelta;
			rectTransform.localScale = originalScale;
			rectTransform.localRotation = originalRotation;
		}
		if (canvasGroup != null)
		{
			canvasGroup.alpha = 1f;
		}
	}

	public void ResetToOriginal()
	{
		RestoreOriginalState();
	}

	private void OnDisable()
	{
		if (delayedSoundCoroutine != null)
		{
			StopCoroutine(delayedSoundCoroutine);
			delayedSoundCoroutine = null;
		}
		if (isInitialized)
		{
			RestoreOriginalState();
		}
	}

	private void LateUpdate()
	{
		if (isInitialized && rectTransform != null && rectTransform.sizeDelta != originalSizeDelta)
		{
			rectTransform.sizeDelta = originalSizeDelta;
		}
	}
}
