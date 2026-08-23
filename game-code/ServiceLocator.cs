using UnityEngine;

public class ServiceLocator : MonoBehaviour
{
	private static ServiceLocator instance;

	[SerializeField]
	private VFXManager vfxManager;

	public static IVFXService VFX { get; private set; }

	private void Awake()
	{
		if (instance != null && instance != this)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		instance = this;
		Object.DontDestroyOnLoad(base.gameObject);
		RegisterServices();
	}

	private void RegisterServices()
	{
		VFX = vfxManager;
	}
}
