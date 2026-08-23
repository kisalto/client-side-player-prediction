using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class DeathSequenceManager : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private BlockDeathTransition blockTransition;

	[SerializeField]
	private CanvasGroup deathUIGroup;

	[SerializeField]
	private GameObject deathUIContainer;

	[SerializeField]
	private TextMeshProUGUI deathText;

	[Header("Buttons")]
	[SerializeField]
	private CanvasGroup retryButtonGroup;

	[SerializeField]
	private CanvasGroup giveUpButtonGroup;

	[SerializeField]
	private float buttonFadeDuration = 0.5f;

	[SerializeField]
	private float delayBetweenButtons = 0.2f;

	[Header("Camera")]
	[SerializeField]
	private Camera mainCamera;

	[SerializeField]
	private Transform playerTransform;

	[SerializeField]
	private CinemachineVirtualCamera virtualCamera;

	[SerializeField]
	private float cameraCenterDuration = 0.5f;

	[Header("Dolly Zoom")]
	[SerializeField]
	private bool enableDollyZoom = true;

	[SerializeField]
	private float targetOrthographicSize = 2f;

	[SerializeField]
	private AnimationCurve dollyZoomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	[Header("Transition Settings")]
	[SerializeField]
	private float blockTransitionDuration = 2f;

	[SerializeField]
	private float delayBeforeUI = 0.5f;

	[SerializeField]
	private float uiFadeDuration = 1f;

	[SerializeField]
	private bool freezeTimeOnDeath = true;

	[SerializeField]
	private float effectSearchRadius = 10f;

	[Header("Typewriter Effect")]
	[SerializeField]
	private bool enableTypewriter = true;

	[SerializeField]
	private float typewriterSpeed = 0.1f;

	[SerializeField]
	private AudioClip typewriterSound;

	[SerializeField]
	private AudioSource typewriterAudioSource;

	[Header("UI Management")]
	[SerializeField]
	private bool hideOtherUIOnDeath = true;

	[SerializeField]
	private List<Canvas> excludedCanvases = new List<Canvas>();

	[Header("Lighting Control")]
	[SerializeField]
	private Light2D deathOverlayLight;

	[SerializeField]
	private bool disableLocalLightsOnPlayer = true;

	[SerializeField]
	private Transform localLightsParent;

	[SerializeField]
	private string[] playerSortingLayers = new string[2] { "Player", "Player Death" };

	[SerializeField]
	private List<Light2D> excludedLights = new List<Light2D>();

	[Header("Sorting Layer Control")]
	[SerializeField]
	private bool changePlayerSortingLayer = true;

	[SerializeField]
	private string deathSortingLayerName = "Player Death";

	private bool isSequencePlaying;

	private Vector3 targetCameraPosition;

	private CinemachineBrain cinemachineBrain;

	private bool originalCinemachineEnabled;

	private List<ParticleSystem> affectedParticleSystems = new List<ParticleSystem>();

	private Animator playerAnimator;

	private List<Canvas> hiddenCanvases = new List<Canvas>();

	private Canvas deathCanvas;

	private float originalOrthographicSize;

	private Coroutine dollyZoomCoroutine;

	private string originalDeathText;

	private SpriteRenderer playerSpriteRenderer;

	private string originalSortingLayerName;

	private int originalSortingOrder;

	private Dictionary<Light2D, int[]> modifiedLights = new Dictionary<Light2D, int[]>();

	private void Awake()
	{
		if (blockTransition == null)
		{
			blockTransition = GetComponent<BlockDeathTransition>();
		}
		if (deathUIGroup != null)
		{
			deathUIGroup.alpha = 0f;
			deathCanvas = deathUIGroup.GetComponent<Canvas>();
		}
		if (deathUIContainer != null)
		{
			deathUIContainer.SetActive(value: false);
		}
		if (mainCamera != null)
		{
			cinemachineBrain = mainCamera.GetComponent<CinemachineBrain>();
			originalOrthographicSize = mainCamera.orthographicSize;
		}
		if (deathText != null)
		{
			originalDeathText = deathText.text;
		}
		if (typewriterAudioSource == null && typewriterSound != null)
		{
			typewriterAudioSource = base.gameObject.AddComponent<AudioSource>();
			typewriterAudioSource.playOnAwake = false;
			typewriterAudioSource.clip = typewriterSound;
		}
		if (retryButtonGroup != null)
		{
			retryButtonGroup.alpha = 0f;
		}
		if (giveUpButtonGroup != null)
		{
			giveUpButtonGroup.alpha = 0f;
		}
		if (playerTransform != null)
		{
			playerSpriteRenderer = playerTransform.GetComponentInChildren<SpriteRenderer>();
			if (playerSpriteRenderer != null)
			{
				originalSortingLayerName = playerSpriteRenderer.sortingLayerName;
				originalSortingOrder = playerSpriteRenderer.sortingOrder;
			}
		}
		if (deathOverlayLight != null)
		{
			deathOverlayLight.enabled = false;
		}
		if (localLightsParent == null)
		{
			GameObject gameObject = GameObject.Find("Local Lights");
			if (gameObject != null)
			{
				localLightsParent = gameObject.transform;
			}
		}
	}

	public void TriggerDeathSequence()
	{
		if (!isSequencePlaying)
		{
			StartCoroutine(PlayDeathSequence());
		}
	}

	private IEnumerator PlayDeathSequence()
	{
		isSequencePlaying = true;
		SFXManager.Instance?.FadeOutAll(blockTransitionDuration);
		MusicManager.Instance?.FadeOutAll(blockTransitionDuration);
		if (hideOtherUIOnDeath)
		{
			HideAllOtherUI();
		}
		ConfigureDeathLighting();
		ChangePlayerSortingLayer();
		yield return StartCoroutine(CenterCameraOnPlayer());
		if (freezeTimeOnDeath)
		{
			SetupUnscaledTimeForPlayerEffects();
			Time.timeScale = 0f;
		}
		if (enableDollyZoom && mainCamera != null)
		{
			dollyZoomCoroutine = StartCoroutine(DollyZoomEffect(blockTransitionDuration));
		}
		if (blockTransition != null)
		{
			yield return StartCoroutine(blockTransition.PlayTransition((playerTransform != null) ? playerTransform.position : Vector3.zero, blockTransitionDuration));
		}
		if (dollyZoomCoroutine != null)
		{
			StopCoroutine(dollyZoomCoroutine);
			dollyZoomCoroutine = null;
		}
		float delayElapsed = 0f;
		while (delayElapsed < delayBeforeUI)
		{
			delayElapsed += Time.unscaledDeltaTime;
			yield return null;
		}
		if (deathUIContainer != null)
		{
			deathUIContainer.SetActive(value: true);
		}
		if (enableTypewriter && deathText != null)
		{
			yield return StartCoroutine(TypewriterEffect());
		}
		else if (deathUIGroup != null)
		{
			float uiElapsed = 0f;
			while (uiElapsed < uiFadeDuration)
			{
				uiElapsed += Time.unscaledDeltaTime;
				deathUIGroup.alpha = Mathf.Clamp01(uiElapsed / uiFadeDuration);
				yield return null;
			}
			deathUIGroup.alpha = 1f;
		}
		yield return StartCoroutine(FadeInButtons());
		isSequencePlaying = false;
	}

	private void ConfigureDeathLighting()
	{
		if (deathOverlayLight != null)
		{
			deathOverlayLight.enabled = true;
		}
		if (!disableLocalLightsOnPlayer || localLightsParent == null)
		{
			return;
		}
		modifiedLights.Clear();
		int[] array = new int[playerSortingLayers.Length];
		for (int i = 0; i < playerSortingLayers.Length; i++)
		{
			array[i] = SortingLayer.NameToID(playerSortingLayers[i]);
		}
		Light2D[] componentsInChildren = localLightsParent.GetComponentsInChildren<Light2D>(includeInactive: true);
		foreach (Light2D light2D in componentsInChildren)
		{
			if (excludedLights.Contains(light2D) || !light2D.enabled)
			{
				continue;
			}
			int[] targetSortingLayers = light2D.targetSortingLayers;
			List<int> list = new List<int>();
			bool flag = false;
			int[] array2 = targetSortingLayers;
			foreach (int num in array2)
			{
				bool flag2 = false;
				int[] array3 = array;
				foreach (int num2 in array3)
				{
					if (num == num2)
					{
						flag2 = true;
						flag = true;
						break;
					}
				}
				if (!flag2)
				{
					list.Add(num);
				}
			}
			if (flag)
			{
				modifiedLights[light2D] = targetSortingLayers;
				light2D.targetSortingLayers = list.ToArray();
			}
		}
		Debug.Log($"[DeathSequence] Removed player layers from {modifiedLights.Count} local lights (Global lights unaffected)");
	}

	private void RestoreLighting()
	{
		if (deathOverlayLight != null)
		{
			deathOverlayLight.enabled = false;
		}
		foreach (KeyValuePair<Light2D, int[]> modifiedLight in modifiedLights)
		{
			if (modifiedLight.Key != null)
			{
				modifiedLight.Key.targetSortingLayers = modifiedLight.Value;
			}
		}
		modifiedLights.Clear();
		Debug.Log("[DeathSequence] Restored all local light target layers");
	}

	private void ChangePlayerSortingLayer()
	{
		if (changePlayerSortingLayer && !(playerSpriteRenderer == null))
		{
			if (SortingLayer.IsValid(SortingLayer.NameToID(deathSortingLayerName)))
			{
				playerSpriteRenderer.sortingLayerName = deathSortingLayerName;
				Debug.Log("[DeathSequence] Changed player sorting layer to '" + deathSortingLayerName + "'");
			}
			else
			{
				Debug.LogWarning("[DeathSequence] Sorting layer '" + deathSortingLayerName + "' not found!");
			}
		}
	}

	private void RestorePlayerSortingLayer()
	{
		if (changePlayerSortingLayer && !(playerSpriteRenderer == null))
		{
			playerSpriteRenderer.sortingLayerName = originalSortingLayerName;
			playerSpriteRenderer.sortingOrder = originalSortingOrder;
			Debug.Log("[DeathSequence] Restored player sorting layer to '" + originalSortingLayerName + "'");
		}
	}

	private IEnumerator TypewriterEffect()
	{
		if (deathText == null)
		{
			yield break;
		}
		deathUIGroup.alpha = 1f;
		string fullText = originalDeathText;
		deathText.text = "";
		deathText.maxVisibleCharacters = 0;
		for (int i = 0; i <= fullText.Length; i++)
		{
			deathText.text = fullText;
			deathText.maxVisibleCharacters = i;
			if (typewriterSound != null && typewriterAudioSource != null && i < fullText.Length)
			{
				typewriterAudioSource.PlayOneShot(typewriterSound);
			}
			float elapsed = 0f;
			while (elapsed < typewriterSpeed)
			{
				elapsed += Time.unscaledDeltaTime;
				yield return null;
			}
		}
		deathText.maxVisibleCharacters = fullText.Length;
	}

	private IEnumerator FadeInButtons()
	{
		if (retryButtonGroup != null)
		{
			yield return StartCoroutine(FadeInCanvasGroup(retryButtonGroup, buttonFadeDuration));
		}
		float delayElapsed = 0f;
		while (delayElapsed < delayBetweenButtons)
		{
			delayElapsed += Time.unscaledDeltaTime;
			yield return null;
		}
		if (giveUpButtonGroup != null)
		{
			yield return StartCoroutine(FadeInCanvasGroup(giveUpButtonGroup, buttonFadeDuration));
		}
	}

	private IEnumerator FadeInCanvasGroup(CanvasGroup group, float duration)
	{
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			group.alpha = Mathf.Clamp01(elapsed / duration);
			yield return null;
		}
		group.alpha = 1f;
	}

	private IEnumerator CenterCameraOnPlayer()
	{
		if (!(playerTransform == null) && !(mainCamera == null))
		{
			targetCameraPosition = new Vector3(playerTransform.position.x, playerTransform.position.y, mainCamera.transform.position.z);
			if (cinemachineBrain != null)
			{
				originalCinemachineEnabled = cinemachineBrain.enabled;
				cinemachineBrain.enabled = false;
			}
			Vector3 startCameraPos = mainCamera.transform.position;
			float elapsed = 0f;
			while (elapsed < cameraCenterDuration)
			{
				elapsed += Time.deltaTime;
				float t = elapsed / cameraCenterDuration;
				t = Mathf.SmoothStep(0f, 1f, t);
				mainCamera.transform.position = Vector3.Lerp(startCameraPos, targetCameraPosition, t);
				yield return null;
			}
			mainCamera.transform.position = targetCameraPosition;
		}
	}

	private IEnumerator DollyZoomEffect(float duration)
	{
		if (!(mainCamera == null) && mainCamera.orthographic)
		{
			float startSize = mainCamera.orthographicSize;
			float elapsed = 0f;
			while (elapsed < duration)
			{
				elapsed += Time.unscaledDeltaTime;
				float t = dollyZoomCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
				mainCamera.orthographicSize = Mathf.Lerp(startSize, targetOrthographicSize, t);
				yield return null;
			}
			mainCamera.orthographicSize = targetOrthographicSize;
		}
	}

	private void HideAllOtherUI()
	{
		hiddenCanvases.Clear();
		Canvas[] array = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
		foreach (Canvas canvas in array)
		{
			if (!(canvas == deathCanvas) && !excludedCanvases.Contains(canvas) && canvas.enabled && !(canvas.GetComponent<ScreenFader>() != null) && !(canvas.GetComponentInChildren<ScreenFader>() != null))
			{
				canvas.enabled = false;
				hiddenCanvases.Add(canvas);
			}
		}
	}

	private void ShowHiddenUI()
	{
		foreach (Canvas hiddenCanvase in hiddenCanvases)
		{
			if (hiddenCanvase != null)
			{
				hiddenCanvase.enabled = true;
			}
		}
		hiddenCanvases.Clear();
	}

	private void SetupUnscaledTimeForPlayerEffects()
	{
		affectedParticleSystems.Clear();
		ParticleSystem[] componentsInChildren;
		if (playerTransform != null)
		{
			playerAnimator = playerTransform.GetComponentInChildren<Animator>();
			if (playerAnimator != null)
			{
				playerAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
			}
			componentsInChildren = playerTransform.GetComponentsInChildren<ParticleSystem>();
			foreach (ParticleSystem particleSystem in componentsInChildren)
			{
				ParticleSystem.MainModule main = particleSystem.main;
				main.useUnscaledTime = true;
				affectedParticleSystems.Add(particleSystem);
			}
		}
		componentsInChildren = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
		foreach (ParticleSystem particleSystem2 in componentsInChildren)
		{
			if (particleSystem2.isPlaying && playerTransform != null && Vector3.Distance(particleSystem2.transform.position, playerTransform.position) <= effectSearchRadius)
			{
				ParticleSystem.MainModule main2 = particleSystem2.main;
				main2.useUnscaledTime = true;
				affectedParticleSystems.Add(particleSystem2);
			}
		}
	}

	private void ResetPlayerEffectsTimeScale()
	{
		if (playerAnimator != null)
		{
			playerAnimator.updateMode = AnimatorUpdateMode.Normal;
		}
		foreach (ParticleSystem affectedParticleSystem in affectedParticleSystems)
		{
			if (affectedParticleSystem != null)
			{
				ParticleSystem.MainModule main = affectedParticleSystem.main;
				main.useUnscaledTime = false;
			}
		}
		affectedParticleSystems.Clear();
	}

	public void ResetSequence()
	{
		if (dollyZoomCoroutine != null)
		{
			StopCoroutine(dollyZoomCoroutine);
			dollyZoomCoroutine = null;
		}
		if (mainCamera != null)
		{
			mainCamera.orthographicSize = originalOrthographicSize;
		}
		if (blockTransition != null)
		{
			blockTransition.ResetTransition();
		}
		if (deathUIGroup != null)
		{
			deathUIGroup.alpha = 0f;
		}
		if (deathText != null)
		{
			deathText.text = originalDeathText;
			deathText.maxVisibleCharacters = 999;
		}
		if (retryButtonGroup != null)
		{
			retryButtonGroup.alpha = 0f;
		}
		if (giveUpButtonGroup != null)
		{
			giveUpButtonGroup.alpha = 0f;
		}
		if (deathUIContainer != null)
		{
			deathUIContainer.SetActive(value: false);
		}
		if (freezeTimeOnDeath)
		{
			Time.timeScale = 1f;
		}
		ResetPlayerEffectsTimeScale();
		RestoreLighting();
		RestorePlayerSortingLayer();
		ShowHiddenUI();
		if (cinemachineBrain != null)
		{
			cinemachineBrain.enabled = originalCinemachineEnabled;
		}
		isSequencePlaying = false;
	}

	private void OnDestroy()
	{
		if (mainCamera != null)
		{
			mainCamera.orthographicSize = originalOrthographicSize;
		}
		Time.timeScale = 1f;
		ResetPlayerEffectsTimeScale();
		RestoreLighting();
		RestorePlayerSortingLayer();
		ShowHiddenUI();
		if (cinemachineBrain != null)
		{
			cinemachineBrain.enabled = originalCinemachineEnabled;
		}
	}
}
