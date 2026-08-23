using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
	private const string BossSceneName = "SCENE_Ch1_Lv5";

	private const string MainMenuSceneName = "MainMenu";

	private const float NormalCutoff = 22000f;

	private const float MuffledCutoff = 600f;

	[Header("Tracks")]
	[SerializeField]
	private AudioClip mainTheme;

	[SerializeField]
	private AudioClip bossTheme;

	[Header("Settings")]
	[SerializeField]
	[Range(0f, 1f)]
	private float musicVolume = 0.7f;

	[SerializeField]
	private float fadeInDuration = 1.5f;

	[SerializeField]
	private float fadeOutDuration = 1.5f;

	private AudioSource mainSource;

	private AudioSource bossSource;

	private AudioLowPassFilter mainFilter;

	private AudioLowPassFilter bossFilter;

	private Coroutine mainFadeCoroutine;

	private Coroutine bossFadeCoroutine;

	private bool suppressNextAutoPlay;

	public static MusicManager Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
		Object.DontDestroyOnLoad(base.gameObject);
		mainSource = CreateAudioSource("MainThemeSource");
		bossSource = CreateAudioSource("BossThemeSource");
		mainFilter = CreateLowPassFilter(mainSource.gameObject);
		bossFilter = CreateLowPassFilter(bossSource.gameObject);
		mainSource.clip = mainTheme;
		bossSource.clip = bossTheme;
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnDestroy()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		SetMuffled(muffled: false);
		if (scene.name == "SCENE_Ch1_Lv5")
		{
			StopMain();
			return;
		}
		StopBossMusic();
		if (suppressNextAutoPlay)
		{
			suppressNextAutoPlay = false;
		}
		else if (!(scene.name == "MainMenu") || !HasNoSaveSlots())
		{
			PlayMain();
		}
	}

	public void StartBossMusic()
	{
		PlayBoss();
	}

	public void StopBossMusic()
	{
		FadeOut(bossSource, ref bossFadeCoroutine, fadeOutDuration);
	}

	public void FadeOutAll(float duration)
	{
		FadeOut(mainSource, ref mainFadeCoroutine, duration);
		FadeOut(bossSource, ref bossFadeCoroutine, duration);
	}

	public void SuppressNextAutoPlay()
	{
		suppressNextAutoPlay = true;
	}

	public void FadeOutThenSuppressNext()
	{
		FadeOut(mainSource, ref mainFadeCoroutine, fadeOutDuration);
		suppressNextAutoPlay = true;
	}

	public void ForcePlayMain()
	{
		if (mainFadeCoroutine != null)
		{
			StopCoroutine(mainFadeCoroutine);
		}
		mainSource.Stop();
		mainSource.volume = 0f;
		mainSource.loop = true;
		mainSource.Play();
		FadeIn(mainSource, ref mainFadeCoroutine, fadeInDuration);
	}

	public void SetMuffled(bool muffled)
	{
		float cutoffFrequency = (muffled ? 600f : 22000f);
		mainFilter.cutoffFrequency = cutoffFrequency;
		bossFilter.cutoffFrequency = cutoffFrequency;
	}

	private void PlayMain()
	{
		if (!mainSource.isPlaying)
		{
			mainSource.volume = 0f;
			mainSource.loop = true;
			mainSource.Play();
			FadeIn(mainSource, ref mainFadeCoroutine, fadeInDuration);
		}
	}

	private void StopMain()
	{
		FadeOut(mainSource, ref mainFadeCoroutine, fadeOutDuration);
	}

	private void PlayBoss()
	{
		if (!bossSource.isPlaying)
		{
			bossSource.volume = 0f;
			bossSource.loop = true;
			bossSource.Play();
			FadeIn(bossSource, ref bossFadeCoroutine, fadeInDuration);
		}
	}

	private void FadeIn(AudioSource source, ref Coroutine handle, float duration)
	{
		if (handle != null)
		{
			StopCoroutine(handle);
		}
		handle = StartCoroutine(FadeRoutine(source, source.volume, musicVolume, duration));
	}

	private void FadeOut(AudioSource source, ref Coroutine handle, float duration)
	{
		if (source.isPlaying)
		{
			if (handle != null)
			{
				StopCoroutine(handle);
			}
			handle = StartCoroutine(FadeOutRoutine(source, duration));
		}
	}

	private static bool HasNoSaveSlots()
	{
		if (SaveSlotManager.Instance == null)
		{
			return false;
		}
		SaveSlotInfo[] allSlotInfo = SaveSlotManager.Instance.GetAllSlotInfo();
		for (int i = 0; i < allSlotInfo.Length; i++)
		{
			if (!allSlotInfo[i].isEmpty)
			{
				return false;
			}
		}
		return true;
	}

	private IEnumerator FadeRoutine(AudioSource source, float from, float to, float duration)
	{
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			source.volume = Mathf.Lerp(from, to, elapsed / duration);
			yield return null;
		}
		source.volume = to;
	}

	private IEnumerator FadeOutRoutine(AudioSource source, float duration)
	{
		float startVolume = source.volume;
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
			yield return null;
		}
		source.volume = 0f;
		source.Stop();
	}

	private AudioSource CreateAudioSource(string sourceName)
	{
		GameObject obj = new GameObject(sourceName);
		obj.transform.SetParent(base.transform);
		AudioSource audioSource = obj.AddComponent<AudioSource>();
		audioSource.playOnAwake = false;
		audioSource.loop = true;
		audioSource.volume = 0f;
		return audioSource;
	}

	private AudioLowPassFilter CreateLowPassFilter(GameObject target)
	{
		AudioLowPassFilter audioLowPassFilter = target.AddComponent<AudioLowPassFilter>();
		audioLowPassFilter.cutoffFrequency = 22000f;
		audioLowPassFilter.lowpassResonanceQ = 1f;
		return audioLowPassFilter;
	}
}
