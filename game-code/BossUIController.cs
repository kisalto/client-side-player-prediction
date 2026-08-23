using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossUIController : MonoBehaviour
{
	[Header("Panel")]
	[SerializeField]
	private GameObject bossUIPanel;

	[SerializeField]
	private CanvasGroup playerUICanvasGroup;

	[SerializeField]
	private TimerUIController timerUIController;

	[Header("Health Bar")]
	[SerializeField]
	private Image healthBarFill;

	[SerializeField]
	private float healthBarLerpSpeed = 5f;

	[Header("Fade")]
	[SerializeField]
	private float fadeInDuration = 0.8f;

	[Header("Boss Name")]
	[SerializeField]
	private TMP_Text bossNameText;

	[SerializeField]
	private float typewriterDelay = 0.08f;

	[SerializeField]
	private float characterScaleStart = 3.5f;

	[SerializeField]
	private float characterScaleDuration = 0.3f;

	private CanvasGroup bossUIPanelGroup;

	private float targetFillAmount = 1f;

	private Coroutine healthBarCoroutine;

	private void Awake()
	{
		if ((bool)bossUIPanel)
		{
			bossUIPanel.SetActive(value: false);
			bossUIPanelGroup = bossUIPanel.GetComponent<CanvasGroup>();
		}
	}

	public void Show(int currentHealth, int maxHealth)
	{
		if ((bool)bossUIPanel)
		{
			if ((bool)bossNameText)
			{
				bossNameText.maxVisibleCharacters = 0;
			}
			bossUIPanel.SetActive(value: true);
			StartCoroutine(FadeInRoutine());
		}
		ResumeTimerAndPlayerUI();
		UpdateHealth(currentHealth, maxHealth);
	}

	public void Hide()
	{
		if ((bool)bossUIPanel)
		{
			bossUIPanel.SetActive(value: false);
		}
	}

	public void UpdateHealth(int currentHealth, int maxHealth)
	{
		if ((bool)healthBarFill)
		{
			targetFillAmount = (float)currentHealth / (float)maxHealth;
			if (healthBarCoroutine != null)
			{
				StopCoroutine(healthBarCoroutine);
			}
			healthBarCoroutine = StartCoroutine(AnimateHealthBar());
		}
	}

	public Coroutine FadeOut()
	{
		return StartCoroutine(FadeOutRoutine());
	}

	public Coroutine FadeIn()
	{
		return StartCoroutine(FadeInFromCurrentRoutine());
	}

	public void OnDialogueStarted()
	{
		if ((bool)playerUICanvasGroup)
		{
			StartCoroutine(FadeCanvasGroup(playerUICanvasGroup, playerUICanvasGroup.alpha, 0f, fadeInDuration));
		}
		if ((bool)timerUIController)
		{
			timerUIController.PauseTimer();
		}
	}

	public void ResumeTimerAndPlayerUI()
	{
		if ((bool)timerUIController)
		{
			timerUIController.ResumeTimer();
		}
		if ((bool)playerUICanvasGroup)
		{
			StartCoroutine(FadeCanvasGroup(playerUICanvasGroup, playerUICanvasGroup.alpha, 1f, fadeInDuration));
		}
	}

	private IEnumerator AnimateHealthBar()
	{
		while (!Mathf.Approximately(healthBarFill.fillAmount, targetFillAmount))
		{
			healthBarFill.fillAmount = Mathf.Lerp(healthBarFill.fillAmount, targetFillAmount, Time.deltaTime * healthBarLerpSpeed);
			yield return null;
		}
		healthBarFill.fillAmount = targetFillAmount;
		healthBarCoroutine = null;
	}

	private IEnumerator FadeInRoutine()
	{
		if ((bool)bossUIPanelGroup)
		{
			bossUIPanelGroup.alpha = 0f;
			float elapsed = 0f;
			while (elapsed < fadeInDuration)
			{
				elapsed += Time.deltaTime;
				bossUIPanelGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
				yield return null;
			}
			bossUIPanelGroup.alpha = 1f;
			if ((bool)bossNameText)
			{
				StartCoroutine(TypewriterEffect());
			}
		}
	}

	private IEnumerator FadeOutRoutine()
	{
		if ((bool)timerUIController)
		{
			timerUIController.PauseTimer();
		}
		if ((bool)playerUICanvasGroup)
		{
			StartCoroutine(FadeCanvasGroup(playerUICanvasGroup, playerUICanvasGroup.alpha, 0f, fadeInDuration));
		}
		if ((bool)bossUIPanelGroup)
		{
			float elapsed = 0f;
			float startAlpha = bossUIPanelGroup.alpha;
			while (elapsed < fadeInDuration)
			{
				elapsed += Time.deltaTime;
				bossUIPanelGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeInDuration);
				yield return null;
			}
			bossUIPanelGroup.alpha = 0f;
		}
	}

	private IEnumerator FadeInFromCurrentRoutine()
	{
		if ((bool)timerUIController)
		{
			timerUIController.ResumeTimer();
		}
		if ((bool)playerUICanvasGroup)
		{
			StartCoroutine(FadeCanvasGroup(playerUICanvasGroup, playerUICanvasGroup.alpha, 1f, fadeInDuration));
		}
		if ((bool)bossUIPanelGroup)
		{
			float elapsed = 0f;
			float startAlpha = bossUIPanelGroup.alpha;
			while (elapsed < fadeInDuration)
			{
				elapsed += Time.deltaTime;
				bossUIPanelGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / fadeInDuration);
				yield return null;
			}
			bossUIPanelGroup.alpha = 1f;
		}
	}

	private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
	{
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			group.alpha = Mathf.Lerp(from, to, elapsed / duration);
			yield return null;
		}
		group.alpha = to;
	}

	private IEnumerator TypewriterEffect()
	{
		bossNameText.ForceMeshUpdate();
		bossNameText.maxVisibleCharacters = 0;
		int totalCharacters = bossNameText.textInfo.characterCount;
		if (totalCharacters == 0)
		{
			yield break;
		}
		float[] scaleTimers = new float[totalCharacters];
		int visibleCount = 0;
		float typewriterTimer = typewriterDelay;
		while (visibleCount < totalCharacters || AnyCharacterStillAnimating(scaleTimers, visibleCount))
		{
			float deltaTime = Time.deltaTime;
			typewriterTimer += deltaTime;
			if (visibleCount < totalCharacters && typewriterTimer >= typewriterDelay)
			{
				typewriterTimer = 0f;
				scaleTimers[visibleCount] = 0f;
				visibleCount++;
				bossNameText.maxVisibleCharacters = visibleCount;
			}
			for (int i = 0; i < visibleCount; i++)
			{
				if (scaleTimers[i] < characterScaleDuration)
				{
					scaleTimers[i] += deltaTime;
				}
			}
			bossNameText.ForceMeshUpdate();
			TMP_TextInfo textInfo = bossNameText.textInfo;
			for (int j = 0; j < visibleCount; j++)
			{
				TMP_CharacterInfo tMP_CharacterInfo = textInfo.characterInfo[j];
				if (tMP_CharacterInfo.isVisible)
				{
					float num = Mathf.Clamp01(scaleTimers[j] / characterScaleDuration);
					float t = 1f - Mathf.Pow(1f - num, 3f);
					float num2 = Mathf.Lerp(characterScaleStart, 1f, t);
					int materialReferenceIndex = tMP_CharacterInfo.materialReferenceIndex;
					int vertexIndex = tMP_CharacterInfo.vertexIndex;
					Vector3[] vertices = textInfo.meshInfo[materialReferenceIndex].vertices;
					Vector3 vector = (vertices[vertexIndex] + vertices[vertexIndex + 2]) * 0.5f;
					for (int k = 0; k < 4; k++)
					{
						vertices[vertexIndex + k] = vector + (vertices[vertexIndex + k] - vector) * num2;
					}
				}
			}
			bossNameText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
			yield return null;
		}
	}

	private bool AnyCharacterStillAnimating(float[] timers, int count)
	{
		for (int i = 0; i < count; i++)
		{
			if (timers[i] < characterScaleDuration)
			{
				return true;
			}
		}
		return false;
	}
}
