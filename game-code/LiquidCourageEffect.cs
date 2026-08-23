using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class LiquidCourageEffect : MonoBehaviour
{
	[Header("Effect Settings")]
	[SerializeField]
	private float speedMultiplier = 1.5f;

	[SerializeField]
	private float effectDuration = 10f;

	[SerializeField]
	private float lerpSpeed = 2f;

	[Header("Visual Effects")]
	[SerializeField]
	private Color powerUpColor = new Color(1f, 0.85f, 0f, 1f);

	[SerializeField]
	private float glowIntensity = 0.8f;

	[SerializeField]
	private float startPulseSpeed = 2f;

	[SerializeField]
	private float endPulseSpeed = 8f;

	[SerializeField]
	private float urgencyStartTime = 3f;

	[SerializeField]
	private bool enableScalePulse = true;

	[SerializeField]
	private float scalePulseAmount = 0.08f;

	[SerializeField]
	private float fadeOutDuration = 0.3f;

	[Header("Sprite Visual Transform")]
	[Tooltip("Assign the child Transform that holds the SpriteRenderer. Scale pulse is applied here so the collider on the root is unaffected.")]
	[SerializeField]
	private Transform spriteVisualTransform;

	[Header("Burst Effect")]
	[SerializeField]
	private GameObject powerUpEffectObject;

	[SerializeField]
	private float burstDuration = 1.5f;

	[SerializeField]
	private float burstStartScale = 0.5f;

	[SerializeField]
	private float burstEndScale = 2f;

	[SerializeField]
	private AnimationCurve burstScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	[SerializeField]
	private AnimationCurve burstAlphaCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

	[Header("Post-Processing")]
	[SerializeField]
	private Volume postProcessVolume;

	[SerializeField]
	private float fadeInDuration = 0.5f;

	[SerializeField]
	private float chromaticAberrationIntensity = 0.4f;

	[SerializeField]
	private float lensDistortionIntensity = -0.25f;

	[SerializeField]
	private float vignetteIntensity = 0.35f;

	[SerializeField]
	private float saturationBoost = 1.15f;

	private PlayerData playerData;

	private PlayerDashController dashController;

	private SpriteRenderer playerSpriteRenderer;

	private SpriteRenderer powerUpEffectRenderer;

	private Color originalColor;

	private Vector3 originalPowerUpEffectScale;

	private float savedOriginalSpeed;

	private bool hasInitializedOriginalSpeed;

	private bool isEffectActive;

	private bool isEnding;

	private Coroutine effectCoroutine;

	private float remainingDuration;

	private ChromaticAberration chromaticAberration;

	private LensDistortion lensDistortion;

	private Vignette vignette;

	private ColorAdjustments colorAdjustments;

	private void Awake()
	{
		playerData = GetComponent<PlayerStateMachine>().Data;
		dashController = GetComponent<PlayerDashController>();
		playerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
		if (playerSpriteRenderer != null)
		{
			originalColor = playerSpriteRenderer.color;
		}
		if (spriteVisualTransform == null)
		{
			Debug.LogWarning("[LiquidCourageEffect] spriteVisualTransform is not assigned. Scale pulse will be disabled.", this);
		}
		if (powerUpEffectObject == null)
		{
			Transform transform = base.transform.Find("Power Up Effect");
			if (transform != null)
			{
				powerUpEffectObject = transform.gameObject;
			}
		}
		if (powerUpEffectObject != null)
		{
			powerUpEffectRenderer = powerUpEffectObject.GetComponent<SpriteRenderer>();
			originalPowerUpEffectScale = powerUpEffectObject.transform.localScale;
			powerUpEffectObject.SetActive(value: false);
		}
		SetupPostProcessing();
	}

	private void Start()
	{
		if (!hasInitializedOriginalSpeed)
		{
			savedOriginalSpeed = playerData.moveSpeed;
			hasInitializedOriginalSpeed = true;
		}
	}

	private void SetupPostProcessing()
	{
		if (postProcessVolume == null)
		{
			Debug.LogWarning("Post Process Volume not assigned! Post-processing effects will not work.");
			return;
		}
		if (postProcessVolume.profile.TryGet<ChromaticAberration>(out chromaticAberration))
		{
			chromaticAberration.intensity.overrideState = true;
		}
		if (postProcessVolume.profile.TryGet<LensDistortion>(out lensDistortion))
		{
			lensDistortion.intensity.overrideState = true;
		}
		if (postProcessVolume.profile.TryGet<Vignette>(out vignette))
		{
			vignette.intensity.overrideState = true;
		}
		if (postProcessVolume.profile.TryGet<ColorAdjustments>(out colorAdjustments))
		{
			colorAdjustments.saturation.overrideState = true;
		}
	}

	public void ActivateEffect()
	{
		if (isEffectActive && !isEnding)
		{
			remainingDuration = effectDuration;
			StartCoroutine(AnimatePowerUpEffect());
			return;
		}
		if (effectCoroutine != null)
		{
			StopCoroutine(effectCoroutine);
		}
		effectCoroutine = StartCoroutine(LiquidCourageEffectRoutine());
	}

	public void SuppressScalePulse()
	{
		enableScalePulse = false;
		ResetSpriteVisualScale();
	}

	public void ResetDuration()
	{
		if (isEffectActive && !isEnding)
		{
			remainingDuration = effectDuration;
			StartCoroutine(AnimatePowerUpEffect());
			return;
		}
		if (effectCoroutine != null)
		{
			StopCoroutine(effectCoroutine);
		}
		effectCoroutine = StartCoroutine(LiquidCourageEffectRoutine());
	}

	private IEnumerator AnimatePowerUpEffect()
	{
		if (powerUpEffectObject == null)
		{
			Debug.LogWarning("Power Up Effect object not assigned!");
			yield break;
		}
		powerUpEffectObject.SetActive(value: true);
		powerUpEffectObject.transform.localPosition = Vector3.zero;
		powerUpEffectObject.transform.localScale = originalPowerUpEffectScale * burstStartScale;
		if (powerUpEffectRenderer != null)
		{
			Color color = powerUpEffectRenderer.color;
			color.a = 1f;
			powerUpEffectRenderer.color = color;
		}
		float elapsedTime = 0f;
		while (elapsedTime < burstDuration)
		{
			elapsedTime += Time.deltaTime;
			float time = elapsedTime / burstDuration;
			float num = Mathf.Lerp(burstStartScale, burstEndScale, burstScaleCurve.Evaluate(time));
			powerUpEffectObject.transform.localScale = originalPowerUpEffectScale * num;
			if (powerUpEffectRenderer != null)
			{
				float a = burstAlphaCurve.Evaluate(time);
				Color color2 = powerUpEffectRenderer.color;
				color2.a = a;
				powerUpEffectRenderer.color = color2;
			}
			yield return null;
		}
		powerUpEffectObject.SetActive(value: false);
	}

	private IEnumerator LiquidCourageEffectRoutine()
	{
		float targetMoveSpeed = savedOriginalSpeed * speedMultiplier;
		isEffectActive = true;
		isEnding = false;
		remainingDuration = effectDuration;
		dashController.enabled = true;
		StartCoroutine(AnimatePowerUpEffect());
		if (postProcessVolume != null)
		{
			postProcessVolume.weight = 1f;
		}
		Debug.Log($"LiquidCourage Effect Started! Speed: {savedOriginalSpeed} -> {targetMoveSpeed}");
		float fadeInTime = 0f;
		while (fadeInTime < fadeInDuration)
		{
			fadeInTime += Time.deltaTime;
			float intensity = fadeInTime / fadeInDuration;
			UpdatePostProcessing(intensity);
			yield return null;
		}
		UpdatePostProcessing(1f);
		float pulseTime = 0f;
		while (remainingDuration > 0f)
		{
			remainingDuration -= Time.deltaTime;
			playerData.moveSpeed = Mathf.Lerp(playerData.moveSpeed, targetMoveSpeed, lerpSpeed * Time.deltaTime);
			float num = startPulseSpeed;
			if (remainingDuration <= urgencyStartTime)
			{
				float t = 1f - remainingDuration / urgencyStartTime;
				num = Mathf.Lerp(startPulseSpeed, endPulseSpeed, t);
			}
			pulseTime += Time.deltaTime * num;
			float t2 = Mathf.PingPong(pulseTime, 1f);
			float num2 = Mathf.SmoothStep(0f, 1f, t2);
			if (playerSpriteRenderer != null)
			{
				Color color = Color.Lerp(originalColor, powerUpColor, glowIntensity * num2);
				playerSpriteRenderer.color = color;
			}
			if (enableScalePulse && spriteVisualTransform != null)
			{
				float num3 = 1f + scalePulseAmount * num2;
				spriteVisualTransform.localScale = Vector3.one * num3;
			}
			yield return null;
		}
		isEnding = true;
		float fadeTime = 0f;
		Color startFadeColor = ((playerSpriteRenderer != null) ? playerSpriteRenderer.color : originalColor);
		while (fadeTime < fadeOutDuration)
		{
			fadeTime += Time.deltaTime;
			float num4 = fadeTime / fadeOutDuration;
			if (playerSpriteRenderer != null)
			{
				playerSpriteRenderer.color = Color.Lerp(startFadeColor, originalColor, num4);
			}
			if (enableScalePulse && spriteVisualTransform != null)
			{
				float num5 = Mathf.Lerp(1f + scalePulseAmount, 1f, num4);
				spriteVisualTransform.localScale = Vector3.one * num5;
			}
			UpdatePostProcessing(1f - num4);
			yield return null;
		}
		while (Mathf.Abs(playerData.moveSpeed - savedOriginalSpeed) > 0.01f)
		{
			playerData.moveSpeed = Mathf.Lerp(playerData.moveSpeed, savedOriginalSpeed, lerpSpeed * Time.deltaTime);
			yield return null;
		}
		playerData.moveSpeed = savedOriginalSpeed;
		dashController.enabled = false;
		if (playerSpriteRenderer != null)
		{
			playerSpriteRenderer.color = originalColor;
		}
		ResetSpriteVisualScale();
		if (postProcessVolume != null)
		{
			postProcessVolume.weight = 0f;
		}
		ResetPostProcessing();
		Debug.Log($"LiquidCourage Effect Ended! Speed restored to: {savedOriginalSpeed}");
		isEffectActive = false;
		isEnding = false;
		effectCoroutine = null;
	}

	private void ResetSpriteVisualScale()
	{
		if (spriteVisualTransform != null)
		{
			spriteVisualTransform.localScale = Vector3.one;
		}
	}

	private void UpdatePostProcessing(float intensity)
	{
		if (!(postProcessVolume == null))
		{
			if (chromaticAberration != null)
			{
				chromaticAberration.intensity.value = chromaticAberrationIntensity * intensity;
			}
			if (lensDistortion != null)
			{
				lensDistortion.intensity.value = lensDistortionIntensity * intensity;
			}
			if (vignette != null)
			{
				vignette.intensity.value = vignetteIntensity * intensity;
			}
			if (colorAdjustments != null)
			{
				colorAdjustments.saturation.value = (saturationBoost - 1f) * 100f * intensity;
			}
		}
	}

	private void ResetPostProcessing()
	{
		if (chromaticAberration != null)
		{
			chromaticAberration.intensity.value = 0f;
		}
		if (lensDistortion != null)
		{
			lensDistortion.intensity.value = 0f;
		}
		if (vignette != null)
		{
			vignette.intensity.value = 0f;
		}
		if (colorAdjustments != null)
		{
			colorAdjustments.saturation.value = 0f;
		}
	}

	private void OnDestroy()
	{
		if (playerData != null && hasInitializedOriginalSpeed)
		{
			playerData.moveSpeed = savedOriginalSpeed;
		}
		if (playerSpriteRenderer != null)
		{
			playerSpriteRenderer.color = originalColor;
		}
		ResetSpriteVisualScale();
		if (postProcessVolume != null)
		{
			postProcessVolume.weight = 0f;
		}
		ResetPostProcessing();
	}

	public bool IsEffectActive()
	{
		return isEffectActive;
	}

	public bool IsEnding()
	{
		return isEnding;
	}
}
