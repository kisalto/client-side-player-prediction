using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
	[SerializeField]
	private Image fadeImage;

	[SerializeField]
	private float fadeOutDuration = 0.6f;

	[SerializeField]
	private float fadeInDuration = 0.8f;

	private Color nextFadeFromColor = Color.black;

	private Canvas faderCanvas;

	public static ScreenFader Instance { get; private set; }

	public bool IsFading { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
		UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		faderCanvas = GetComponentInParent<Canvas>();
		if (faderCanvas == null)
		{
			faderCanvas = GetComponent<Canvas>();
		}
		if (faderCanvas != null)
		{
			faderCanvas.sortingOrder = 9999;
		}
		if (fadeImage != null)
		{
			fadeImage.color = Color.black;
			fadeImage.raycastTarget = true;
		}
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnDestroy()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	public void LoadScene(string sceneName)
	{
		if (!IsFading)
		{
			StartCoroutine(FadeOutAndLoad(sceneName));
		}
	}

	public void FadeToBlack(Action onComplete)
	{
		if (!IsFading)
		{
			StartCoroutine(FadeRoutine(Color.clear, Color.black, fadeOutDuration, onComplete));
		}
	}

	public void FadeFromBlack(Action onComplete = null)
	{
		if (!IsFading)
		{
			StartCoroutine(FadeRoutine(Color.black, Color.clear, fadeInDuration, onComplete));
		}
	}

	public void SetNextFadeInColor(Color color)
	{
		nextFadeFromColor = color;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		Time.timeScale = 1f;
		SFXManager.Instance?.StopAll();
		IsFading = false;
		Color color = nextFadeFromColor;
		nextFadeFromColor = Color.black;
		StartCoroutine(FadeRoutine(color, Color.clear, fadeInDuration, null));
	}

	private IEnumerator FadeOutAndLoad(string sceneName)
	{
		SFXManager.Instance?.Play2D("UI_SceneTransition");
		SFXManager.Instance?.FadeOutAll(fadeOutDuration);
		yield return FadeRoutine(Color.clear, Color.black, fadeOutDuration, null);
		SceneManager.LoadScene(sceneName);
	}

	private IEnumerator FadeRoutine(Color from, Color to, float duration, Action onComplete)
	{
		IsFading = true;
		fadeImage.raycastTarget = true;
		fadeImage.color = from;
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			fadeImage.color = Color.Lerp(from, to, elapsed / duration);
			yield return null;
		}
		fadeImage.color = to;
		if (to == Color.clear)
		{
			fadeImage.raycastTarget = false;
		}
		IsFading = false;
		onComplete?.Invoke();
	}
}
