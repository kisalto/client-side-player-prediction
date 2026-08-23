using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class PooledParticle : MonoBehaviour, IPooledObject
{
	private ParticleSystem particleSystem;

	private string poolTag;

	private float lifetime;

	private bool isLooping;

	private void Awake()
	{
		particleSystem = GetComponent<ParticleSystem>();
		ParticleSystem.MainModule main = particleSystem.main;
		isLooping = main.loop;
		lifetime = main.duration + main.startLifetime.constantMax;
	}

	public void OnObjectSpawn()
	{
		particleSystem.Clear();
		particleSystem.Play();
		if (!isLooping)
		{
			Invoke("ReturnToPool", lifetime);
		}
	}

	public void SetPoolTag(string tag)
	{
		poolTag = tag;
	}

	public void ManualReturn()
	{
		ReturnToPool();
	}

	private void ReturnToPool()
	{
		CancelInvoke();
		if (!string.IsNullOrEmpty(poolTag))
		{
			ObjectPool.Instance.ReturnToPool(poolTag, base.gameObject);
		}
		else
		{
			base.gameObject.SetActive(value: false);
		}
	}

	private void OnDisable()
	{
		CancelInvoke();
	}
}
