using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
	[Header("Scale Settings")]
	[SerializeField]
	private float hoverScale = 1.15f;

	[SerializeField]
	private float scaleDuration = 0.2f;

	[SerializeField]
	private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	[Header("Shake Settings")]
	[SerializeField]
	private bool enableShake = true;

	[SerializeField]
	private float shakeAmount = 3f;

	[SerializeField]
	private float shakeSpeed = 20f;

	[Header("Audio")]
	[SerializeField]
	private AudioClip hoverSound;

	[SerializeField]
	private AudioSource audioSource;

	private Vector3 originalScale;

	private Vector3 originalPosition;

	private Coroutine scaleCoroutine;

	private Coroutine shakeCoroutine;

	private bool isHovering;

	private RectTransform rectTransform;

	private void Awake()
	{
		rectTransform = GetComponent<RectTransform>();
		originalScale = base.transform.localScale;
		originalPosition = rectTransform.anchoredPosition;
		if (audioSource == null && hoverSound != null)
		{
			audioSource = base.gameObject.AddComponent<AudioSource>();
			audioSource.playOnAwake = false;
			audioSource.clip = hoverSound;
		}
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		OnHoverStart();
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		OnHoverEnd();
	}

	public void OnSelect(BaseEventData eventData)
	{
		OnHoverStart();
	}

	public void OnDeselect(BaseEventData eventData)
	{
		OnHoverEnd();
	}

	private void OnHoverStart()
	{
		if (isHovering)
		{
			return;
		}
		isHovering = true;
		if (hoverSound != null && audioSource != null)
		{
			audioSource.PlayOneShot(hoverSound);
		}
		if (scaleCoroutine != null)
		{
			StopCoroutine(scaleCoroutine);
		}
		scaleCoroutine = StartCoroutine(ScaleTo(originalScale * hoverScale));
		if (enableShake)
		{
			if (shakeCoroutine != null)
			{
				StopCoroutine(shakeCoroutine);
			}
			shakeCoroutine = StartCoroutine(ShakeEffect());
		}
	}

	private void OnHoverEnd()
	{
		if (isHovering)
		{
			isHovering = false;
			if (scaleCoroutine != null)
			{
				StopCoroutine(scaleCoroutine);
			}
			scaleCoroutine = StartCoroutine(ScaleTo(originalScale));
			if (shakeCoroutine != null)
			{
				StopCoroutine(shakeCoroutine);
				shakeCoroutine = null;
			}
			rectTransform.anchoredPosition = originalPosition;
		}
	}

	private IEnumerator ScaleTo(Vector3 targetScale)
	{
		Vector3 startScale = base.transform.localScale;
		float elapsed = 0f;
		while (elapsed < scaleDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = scaleCurve.Evaluate(elapsed / scaleDuration);
			base.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
			yield return null;
		}
		base.transform.localScale = targetScale;
		scaleCoroutine = null;
	}

	private IEnumerator ShakeEffect()
	{
		while (isHovering)
		{
			float x = Mathf.Sin(Time.unscaledTime * shakeSpeed) * shakeAmount;
			float y = Mathf.Cos(Time.unscaledTime * shakeSpeed * 1.3f) * shakeAmount * 0.5f;
			rectTransform.anchoredPosition = originalPosition + new Vector3(x, y, 0f);
			yield return null;
		}
		rectTransform.anchoredPosition = originalPosition;
		shakeCoroutine = null;
	}

	private void OnDisable()
	{
		if (scaleCoroutine != null)
		{
			StopCoroutine(scaleCoroutine);
			scaleCoroutine = null;
		}
		if (shakeCoroutine != null)
		{
			StopCoroutine(shakeCoroutine);
			shakeCoroutine = null;
		}
		base.transform.localScale = originalScale;
		if (rectTransform != null)
		{
			rectTransform.anchoredPosition = originalPosition;
		}
		isHovering = false;
	}
}
