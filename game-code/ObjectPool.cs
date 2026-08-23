using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ObjectPool : MonoBehaviour
{
	[Header("Pool Configuration")]
	[Tooltip("List of pool configurations. Drag PoolConfigSO assets here.")]
	[SerializeField]
	private List<PoolConfig> poolConfigs;

	private Dictionary<string, Queue<GameObject>> poolDictionary = new Dictionary<string, Queue<GameObject>>();

	private Dictionary<string, PoolConfig> configDictionary = new Dictionary<string, PoolConfig>();

	private static ObjectPool instance;

	public static ObjectPool Instance
	{
		get
		{
			if (instance == null)
			{
				instance = UnityEngine.Object.FindFirstObjectByType<ObjectPool>();
				if (instance == null)
				{
					instance = new GameObject("ObjectPool").AddComponent<ObjectPool>();
				}
			}
			return instance;
		}
	}

	private void Awake()
	{
		if (instance != null && instance != this)
		{
			instance.MergePools(poolConfigs);
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		instance = this;
		if (base.transform.parent != null)
		{
			base.transform.SetParent(null);
		}
		UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		InitializePools();
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnDestroy()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		DeactivateAllPooledObjects();
	}

	public void DeactivateAllPooledObjects()
	{
		foreach (KeyValuePair<string, Queue<GameObject>> item in poolDictionary)
		{
			foreach (GameObject item2 in item.Value)
			{
				if (!(item2 == null))
				{
					if (item2.activeSelf)
					{
						item2.SetActive(value: false);
					}
					if (item2.transform.parent != base.transform)
					{
						item2.transform.SetParent(base.transform);
					}
				}
			}
		}
	}

	private void MergePools(List<PoolConfig> newConfigs)
	{
		if (newConfigs == null)
		{
			return;
		}
		foreach (PoolConfig newConfig in newConfigs)
		{
			if (!(newConfig == null) && !(newConfig.prefab == null) && !string.IsNullOrEmpty(newConfig.poolTag) && !poolDictionary.ContainsKey(newConfig.poolTag))
			{
				Queue<GameObject> queue = new Queue<GameObject>();
				for (int i = 0; i < newConfig.initialSize; i++)
				{
					GameObject item = CreatePooledObject(newConfig.prefab);
					queue.Enqueue(item);
				}
				poolDictionary.Add(newConfig.poolTag, queue);
				configDictionary.Add(newConfig.poolTag, newConfig);
			}
		}
	}

	private void InitializePools()
	{
		poolDictionary.Clear();
		configDictionary.Clear();
		foreach (PoolConfig poolConfig in poolConfigs)
		{
			if (poolConfig == null || poolConfig.prefab == null || string.IsNullOrEmpty(poolConfig.poolTag))
			{
				continue;
			}
			if (poolDictionary.ContainsKey(poolConfig.poolTag))
			{
				Debug.LogWarning("Duplicate pool tag '" + poolConfig.poolTag + "' found. Skipping duplicate.");
				continue;
			}
			Queue<GameObject> queue = new Queue<GameObject>();
			for (int i = 0; i < poolConfig.initialSize; i++)
			{
				GameObject item = CreatePooledObject(poolConfig.prefab);
				queue.Enqueue(item);
			}
			poolDictionary.Add(poolConfig.poolTag, queue);
			configDictionary.Add(poolConfig.poolTag, poolConfig);
		}
	}

	private GameObject CreatePooledObject(GameObject prefab)
	{
		GameObject obj = UnityEngine.Object.Instantiate(prefab);
		obj.SetActive(value: false);
		obj.transform.SetParent(base.transform);
		return obj;
	}

	public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
	{
		if (!poolDictionary.ContainsKey(tag))
		{
			Debug.LogWarning("[ObjectPool] Pool with tag '" + tag + "' does not exist.");
			return null;
		}
		Queue<GameObject> queue = poolDictionary[tag];
		int count = queue.Count;
		GameObject gameObject = null;
		for (int i = 0; i < count; i++)
		{
			GameObject gameObject2 = queue.Dequeue();
			if (!gameObject2.activeInHierarchy)
			{
				gameObject = gameObject2;
				break;
			}
			queue.Enqueue(gameObject2);
		}
		if (gameObject == null)
		{
			if (!configDictionary.TryGetValue(tag, out var value) || !(value.prefab != null))
			{
				Debug.LogError("[ObjectPool] Pool '" + tag + "' exhausted and cannot be expanded.");
				return null;
			}
			gameObject = CreatePooledObject(value.prefab);
			Debug.LogWarning("[ObjectPool] Pool '" + tag + "' exhausted — expanding by one.");
		}
		gameObject.transform.SetParent(null);
		gameObject.transform.position = position;
		gameObject.transform.rotation = rotation;
		gameObject.SetActive(value: true);
		gameObject.GetComponent<IPooledObject>()?.OnObjectSpawn();
		queue.Enqueue(gameObject);
		return gameObject;
	}

	public void ReturnToPool(string tag, GameObject obj)
	{
		obj.SetActive(value: false);
		obj.transform.SetParent(base.transform);
	}

	public int GetPoolSize(string tag)
	{
		if (!poolDictionary.ContainsKey(tag))
		{
			return 0;
		}
		return poolDictionary[tag].Count;
	}

	public int GetActiveCount(string tag)
	{
		if (!poolDictionary.ContainsKey(tag))
		{
			return 0;
		}
		int num = 0;
		foreach (GameObject item in poolDictionary[tag])
		{
			if (item.activeInHierarchy)
			{
				num++;
			}
		}
		return num;
	}

	public void ForEachActive(string tag, Action<GameObject> action)
	{
		if (!poolDictionary.ContainsKey(tag))
		{
			return;
		}
		foreach (GameObject item in poolDictionary[tag])
		{
			if (item != null && item.activeInHierarchy)
			{
				action(item);
			}
		}
	}
}
