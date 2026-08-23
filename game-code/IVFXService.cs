using UnityEngine;

public interface IVFXService
{
	void PlayEffect(string effectName, Vector3 position);

	void PlayEffect(string effectName, Vector3 position, Transform parent);
}
