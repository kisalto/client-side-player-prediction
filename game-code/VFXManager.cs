using UnityEngine;

public class VFXManager : MonoBehaviour, IVFXService
{
	private static VFXManager instance;

	public static VFXManager Instance
	{
		get
		{
			if (instance == null)
			{
				instance = Object.FindFirstObjectByType<VFXManager>();
			}
			return instance;
		}
	}

	private void Awake()
	{
		if (instance != null && instance != this)
		{
			Object.Destroy(base.gameObject);
		}
		else
		{
			instance = this;
		}
	}

	public void PlayParticleEffect(string effectTag, Vector3 position, Quaternion rotation)
	{
		ObjectPool.Instance.SpawnFromPool(effectTag, position, rotation);
	}

	public void PlayParticleEffect(string effectTag, Vector3 position)
	{
		PlayParticleEffect(effectTag, position, Quaternion.identity);
	}

	public void PlayEffect(string effectName, Vector3 position)
	{
		PlayParticleEffect(effectName, position);
	}

	public void PlayEffect(string effectName, Vector3 position, Transform parent)
	{
		PlayParticleEffect(effectName, position);
	}
}
