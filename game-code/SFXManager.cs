using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SFXManager : MonoBehaviour
{
	private const int DefaultPoolSize = 16;

	private const float DefaultFadeOutDuration = 0.15f;

	[Header("Configuration")]
	[SerializeField]
	private SFXLibrary library;

	[Header("Pool Settings")]
	[SerializeField]
	private int initialPoolSize = 16;

	private readonly Queue<AudioSource> availableSources = new Queue<AudioSource>();

	private readonly Dictionary<int, AudioSource> activeSources = new Dictionary<int, AudioSource>();

	private readonly Dictionary<string, AudioSource> exclusiveSources = new Dictionary<string, AudioSource>();

	private int nextHandle = 1;

	private Transform poolParent;

	public static SFXManager Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
		Object.DontDestroyOnLoad(base.gameObject);
		poolParent = new GameObject("SFXPool").transform;
		poolParent.SetParent(base.transform);
		library?.Initialize();
		WarmPool(initialPoolSize);
	}

	public void Play(string id, Vector3 position)
	{
		SFXEntry entry = GetEntry(id);
		if (entry != null)
		{
			AudioSource source = GetSource();
			ConfigureSource(source, entry);
			source.transform.position = position;
			source.Play();
			ReturnWhenDone(source, entry);
		}
	}

	public void Play2D(string id)
	{
		SFXEntry entry = GetEntry(id);
		if (entry != null)
		{
			AudioSource source = GetSource();
			ConfigureSource(source, entry);
			source.spatialBlend = 0f;
			source.Play();
			ReturnWhenDone(source, entry);
		}
	}

	public void PlayExclusive(string id, Vector3 position)
	{
		SFXEntry entry = GetEntry(id);
		if (entry != null && (!exclusiveSources.TryGetValue(id, out var value) || !(value != null) || !value.isPlaying))
		{
			AudioSource source = GetSource();
			ConfigureSource(source, entry);
			source.transform.position = position;
			source.Play();
			exclusiveSources[id] = source;
			ReturnWhenDone(source, entry, id);
		}
	}

	public void PlayExclusive2D(string id)
	{
		SFXEntry entry = GetEntry(id);
		if (entry != null && (!exclusiveSources.TryGetValue(id, out var value) || !(value != null) || !value.isPlaying))
		{
			AudioSource source = GetSource();
			ConfigureSource(source, entry);
			source.spatialBlend = 0f;
			source.Play();
			exclusiveSources[id] = source;
			ReturnWhenDone(source, entry, id);
		}
	}

	public int PlayLooped(string id, Vector3 position)
	{
		SFXEntry entry = GetEntry(id);
		if (entry == null)
		{
			return -1;
		}
		AudioSource source = GetSource();
		ConfigureSource(source, entry);
		source.loop = true;
		source.transform.position = position;
		source.Play();
		int num = nextHandle++;
		activeSources[num] = source;
		return num;
	}

	public int PlayLooped2D(string id)
	{
		SFXEntry entry = GetEntry(id);
		if (entry == null)
		{
			return -1;
		}
		AudioSource source = GetSource();
		ConfigureSource(source, entry);
		source.loop = true;
		source.spatialBlend = 0f;
		source.Play();
		int num = nextHandle++;
		activeSources[num] = source;
		return num;
	}

	public void Stop(int handle)
	{
		Stop(handle, 0.15f);
	}

	public void Stop(int handle, float fadeDuration)
	{
		if (handle >= 0 && activeSources.TryGetValue(handle, out var value))
		{
			activeSources.Remove(handle);
			if (fadeDuration <= 0f)
			{
				value.Stop();
				value.loop = false;
				ReturnSource(value);
			}
			else
			{
				StartCoroutine(FadeOutAndReturnRoutine(value, fadeDuration));
			}
		}
	}

	public void UpdatePosition(int handle, Vector3 position)
	{
		if (handle >= 0 && activeSources.TryGetValue(handle, out var value))
		{
			value.transform.position = position;
		}
	}

	public void FadeOutAll(float duration)
	{
		StartCoroutine(FadeOutAllRoutine(duration));
	}

	public void StopAll()
	{
		foreach (KeyValuePair<int, AudioSource> activeSource in activeSources)
		{
			if (activeSource.Value != null)
			{
				activeSource.Value.Stop();
				activeSource.Value.loop = false;
				ReturnSource(activeSource.Value);
			}
		}
		activeSources.Clear();
		foreach (KeyValuePair<string, AudioSource> exclusiveSource in exclusiveSources)
		{
			if (exclusiveSource.Value != null && exclusiveSource.Value.isPlaying)
			{
				exclusiveSource.Value.Stop();
				ReturnSource(exclusiveSource.Value);
			}
		}
		exclusiveSources.Clear();
		AudioSource[] componentsInChildren = poolParent.GetComponentsInChildren<AudioSource>(includeInactive: true);
		foreach (AudioSource audioSource in componentsInChildren)
		{
			if (audioSource.isPlaying)
			{
				audioSource.Stop();
			}
		}
	}

	private IEnumerator FadeOutAllRoutine(float duration)
	{
		List<AudioSource> allPlaying = new List<AudioSource>();
		foreach (KeyValuePair<int, AudioSource> activeSource in activeSources)
		{
			if (activeSource.Value != null && activeSource.Value.isPlaying)
			{
				allPlaying.Add(activeSource.Value);
			}
		}
		foreach (KeyValuePair<string, AudioSource> exclusiveSource in exclusiveSources)
		{
			if (exclusiveSource.Value != null && exclusiveSource.Value.isPlaying)
			{
				allPlaying.Add(exclusiveSource.Value);
			}
		}
		AudioSource[] componentsInChildren = poolParent.GetComponentsInChildren<AudioSource>();
		foreach (AudioSource audioSource in componentsInChildren)
		{
			if (audioSource.isPlaying && !allPlaying.Contains(audioSource))
			{
				allPlaying.Add(audioSource);
			}
		}
		if (allPlaying.Count == 0)
		{
			yield break;
		}
		Dictionary<AudioSource, float> startVolumes = new Dictionary<AudioSource, float>();
		foreach (AudioSource item in allPlaying)
		{
			startVolumes[item] = item.volume;
		}
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			foreach (AudioSource item2 in allPlaying)
			{
				if (item2 != null && item2.isPlaying)
				{
					item2.volume = Mathf.Lerp(startVolumes[item2], 0f, t);
				}
			}
			yield return null;
		}
		foreach (AudioSource item3 in allPlaying)
		{
			if (item3 != null)
			{
				item3.Stop();
			}
		}
		activeSources.Clear();
		exclusiveSources.Clear();
	}

	private void WarmPool(int count)
	{
		for (int i = 0; i < count; i++)
		{
			availableSources.Enqueue(CreateSource());
		}
	}

	private AudioSource CreateSource()
	{
		GameObject obj = new GameObject("SFX_Source");
		obj.transform.SetParent(poolParent);
		AudioSource audioSource = obj.AddComponent<AudioSource>();
		audioSource.playOnAwake = false;
		obj.SetActive(value: false);
		return audioSource;
	}

	private AudioSource GetSource()
	{
		AudioSource obj = ((availableSources.Count > 0) ? availableSources.Dequeue() : CreateSource());
		obj.gameObject.SetActive(value: true);
		return obj;
	}

	private void ReturnSource(AudioSource source)
	{
		source.Stop();
		source.clip = null;
		source.loop = false;
		source.spatialBlend = 0f;
		source.gameObject.SetActive(value: false);
		source.transform.SetParent(poolParent);
		availableSources.Enqueue(source);
	}

	private SFXEntry GetEntry(string id)
	{
		if (library == null)
		{
			Debug.LogWarning("[SFXManager] No SFXLibrary assigned.");
			return null;
		}
		SFXEntry sFXEntry = library.Get(id);
		if (sFXEntry == null || sFXEntry.clip == null)
		{
			return null;
		}
		return sFXEntry;
	}

	private void ConfigureSource(AudioSource source, SFXEntry entry)
	{
		source.clip = entry.clip;
		source.volume = entry.volume;
		source.pitch = Random.Range(entry.minPitch, entry.maxPitch);
		source.spatialBlend = entry.spatialBlend;
		source.maxDistance = entry.maxDistance;
		source.rolloffMode = AudioRolloffMode.Linear;
		source.loop = entry.loop;
	}

	private void ReturnWhenDone(AudioSource source, SFXEntry entry, string exclusiveId = null)
	{
		if (!entry.loop)
		{
			StartCoroutine(ReturnAfterPlayback(source, exclusiveId));
		}
	}

	private IEnumerator ReturnAfterPlayback(AudioSource source, string exclusiveId = null)
	{
		while (source.isPlaying)
		{
			yield return null;
		}
		if (exclusiveId != null && exclusiveSources.TryGetValue(exclusiveId, out var value) && value == source)
		{
			exclusiveSources.Remove(exclusiveId);
		}
		ReturnSource(source);
	}

	private IEnumerator FadeOutAndReturnRoutine(AudioSource source, float duration)
	{
		float startVolume = source.volume;
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / duration));
			yield return null;
		}
		source.volume = 0f;
		source.Stop();
		source.loop = false;
		ReturnSource(source);
	}
}
