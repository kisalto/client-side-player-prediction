using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class CameraShakeManager : MonoBehaviour
{
	[SerializeField]
	private NoiseSettings noiseSettings;

	[Header("Pixel Snap Transition")]
	[Tooltip("Time in seconds to fade out pixel snapping at the start of a shake.")]
	[SerializeField]
	private float pixelSnapFadeOutDuration = 0.05f;

	[Tooltip("Extra time to wait after the impulse duration before starting recovery, to let the Cinemachine impulse fully decay.")]
	[SerializeField]
	private float postImpulseBuffer = 0.15f;

	[Tooltip("Time in seconds for pixel snapping to smoothly return after a shake ends.")]
	[SerializeField]
	private float pixelSnapRecoveryDuration = 0.3f;

	private CinemachinePixelPerfect pixelPerfectComponent;

	private Coroutine currentShakeCoroutine;

	private Coroutine currentRecoveryCoroutine;

	private static CameraShakeManager instance;

	private CinemachineImpulseSource impulseSource;

	public static CameraShakeManager Instance
	{
		get
		{
			if (instance == null)
			{
				Debug.LogWarning("[CameraShakeManager] Instance was null — lazy-creating. Place it in the scene to avoid this.");
				GameObject obj = new GameObject("CameraShakeManager");
				instance = obj.AddComponent<CameraShakeManager>();
				instance.Initialize();
				Object.DontDestroyOnLoad(obj);
			}
			return instance;
		}
	}

	private void Awake()
	{
		if (instance == null)
		{
			instance = this;
			Initialize();
			Object.DontDestroyOnLoad(base.gameObject);
		}
		else if (instance != this)
		{
			Object.Destroy(base.gameObject);
		}
	}

	private void Initialize()
	{
		if (impulseSource == null)
		{
			impulseSource = base.gameObject.AddComponent<CinemachineImpulseSource>();
			SetupImpulseSource();
		}
	}

	private void SetupImpulseSource()
	{
		impulseSource.DefaultVelocity = Vector3.one;
		impulseSource.ImpulseDefinition.ImpulseChannel = 1;
		impulseSource.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
		impulseSource.ImpulseDefinition.ImpulseDuration = 0.3f;
		impulseSource.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Dissipating;
	}

	public void ShakeCamera(float amplitude, float frequency, float duration)
	{
		if (impulseSource == null)
		{
			Initialize();
		}
		if (currentShakeCoroutine != null)
		{
			StopCoroutine(currentShakeCoroutine);
		}
		Vector3 velocity = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f).normalized * amplitude;
		currentShakeCoroutine = StartCoroutine(ShakeWithPixelPerfectToggle(velocity, duration));
	}

	public void ShakeCamera(Vector3 velocity, float duration)
	{
		if (impulseSource == null)
		{
			Initialize();
		}
		if (currentShakeCoroutine != null)
		{
			StopCoroutine(currentShakeCoroutine);
		}
		currentShakeCoroutine = StartCoroutine(ShakeWithPixelPerfectToggle(velocity, duration));
	}

	private IEnumerator ShakeWithPixelPerfectToggle(Vector3 velocity, float duration)
	{
		CinemachinePixelPerfect pixelPerfect = FindPixelPerfectComponent();
		if (currentRecoveryCoroutine != null)
		{
			StopCoroutine(currentRecoveryCoroutine);
			currentRecoveryCoroutine = null;
		}
		bool hadCinemachinePP = false;
		if (pixelPerfect != null)
		{
			hadCinemachinePP = true;
			yield return StartCoroutine(FadeOutPixelSnap(pixelPerfect));
		}
		impulseSource.ImpulseDefinition.ImpulseDuration = duration;
		impulseSource.GenerateImpulseWithVelocity(velocity);
		yield return new WaitForSeconds(duration + postImpulseBuffer);
		if (hadCinemachinePP && pixelPerfect != null)
		{
			currentRecoveryCoroutine = StartCoroutine(RecoverPixelSnap(pixelPerfect));
		}
		currentShakeCoroutine = null;
	}

	private IEnumerator FadeOutPixelSnap(CinemachinePixelPerfect pixelPerfect)
	{
		float startBlend = pixelPerfect.PixelSnapBlend;
		if (!(startBlend <= 0f))
		{
			float elapsed = 0f;
			while (elapsed < pixelSnapFadeOutDuration)
			{
				elapsed += Time.deltaTime;
				float num = Mathf.SmoothStep(1f, 0f, elapsed / pixelSnapFadeOutDuration);
				pixelPerfect.PixelSnapBlend = startBlend * num;
				yield return null;
			}
			pixelPerfect.PixelSnapBlend = 0f;
		}
	}

	private IEnumerator RecoverPixelSnap(CinemachinePixelPerfect pixelPerfect)
	{
		float elapsed = 0f;
		while (elapsed < pixelSnapRecoveryDuration)
		{
			elapsed += Time.deltaTime;
			float pixelSnapBlend = Mathf.SmoothStep(0f, 1f, elapsed / pixelSnapRecoveryDuration);
			pixelPerfect.PixelSnapBlend = pixelSnapBlend;
			yield return null;
		}
		pixelPerfect.PixelSnapBlend = 1f;
		currentRecoveryCoroutine = null;
	}

	private CinemachinePixelPerfect FindPixelPerfectComponent()
	{
		if (pixelPerfectComponent == null)
		{
			CinemachineCamera cinemachineCamera = Object.FindFirstObjectByType<CinemachineCamera>();
			if (cinemachineCamera != null)
			{
				pixelPerfectComponent = cinemachineCamera.GetComponent<CinemachinePixelPerfect>();
			}
		}
		return pixelPerfectComponent;
	}
}
