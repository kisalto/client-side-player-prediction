using UnityEngine;
using UnityEngine.Rendering.Universal;

public class BillboardLightFlicker : MonoBehaviour
{
	public enum FlickerType
	{
		Smooth,
		Harsh,
		Random
	}

	[Header("Light Reference")]
	[SerializeField]
	private Light2D targetLight;

	[Header("Base Light Settings")]
	[SerializeField]
	private float baseIntensity = 1f;

	[SerializeField]
	private bool setBaseIntensityOnStart = true;

	[Header("Flicker Timing")]
	[SerializeField]
	private float minTimeBetweenFlickers = 3f;

	[SerializeField]
	private float maxTimeBetweenFlickers = 10f;

	[SerializeField]
	private float minFlickerDuration = 0.1f;

	[SerializeField]
	private float maxFlickerDuration = 0.5f;

	[Header("Flicker Intensity")]
	[SerializeField]
	private float minFlickerIntensity = 0.3f;

	[SerializeField]
	private float maxFlickerIntensity = 1f;

	[SerializeField]
	private float flickerSpeed = 20f;

	[Header("Flicker Pattern")]
	[SerializeField]
	private FlickerType flickerType;

	[SerializeField]
	private bool multipleFlickersPerEvent = true;

	[SerializeField]
	private int minFlickerBursts = 1;

	[SerializeField]
	private int maxFlickerBursts = 3;

	[SerializeField]
	private float timeBetweenBursts = 0.1f;

	[Header("Options")]
	[SerializeField]
	private bool startFlickeringOnEnable = true;

	[SerializeField]
	private float initialDelay;

	private float nextFlickerTime;

	private float flickerEndTime;

	private float flickerPhase;

	private bool isFlickering;

	private bool isActive;

	private int currentBurstCount;

	private int targetBurstCount;

	private float nextBurstTime;

	private bool waitingForNextBurst;

	private void Awake()
	{
		if (targetLight == null)
		{
			targetLight = GetComponent<Light2D>();
		}
		if (targetLight == null)
		{
			Debug.LogError("BillboardLightFlicker: No Light2D component found!", this);
			base.enabled = false;
		}
		else if (setBaseIntensityOnStart)
		{
			baseIntensity = targetLight.intensity;
		}
	}

	private void OnEnable()
	{
		if (startFlickeringOnEnable)
		{
			if (initialDelay > 0f)
			{
				Invoke("StartFlickering", initialDelay);
			}
			else
			{
				StartFlickering();
			}
		}
	}

	private void OnDisable()
	{
		StopFlickering();
	}

	private void Update()
	{
		if (!isActive)
		{
			return;
		}
		if (waitingForNextBurst)
		{
			if (Time.time >= nextBurstTime)
			{
				waitingForNextBurst = false;
				StartFlickerBurst();
			}
		}
		else if (isFlickering)
		{
			UpdateFlicker();
			if (Time.time >= flickerEndTime)
			{
				EndFlickerBurst();
			}
		}
		else if (Time.time >= nextFlickerTime)
		{
			BeginFlickerEvent();
		}
	}

	private void UpdateFlicker()
	{
		flickerPhase += Time.deltaTime * flickerSpeed;
		float t = 0f;
		switch (flickerType)
		{
		case FlickerType.Smooth:
			t = Mathf.PerlinNoise(flickerPhase, 0f);
			break;
		case FlickerType.Harsh:
			t = ((Random.value > 0.5f) ? 1f : 0f);
			break;
		case FlickerType.Random:
			t = Random.value;
			break;
		}
		float intensity = Mathf.Lerp(minFlickerIntensity, maxFlickerIntensity, t) * baseIntensity;
		targetLight.intensity = intensity;
	}

	private void BeginFlickerEvent()
	{
		if (multipleFlickersPerEvent)
		{
			targetBurstCount = Random.Range(minFlickerBursts, maxFlickerBursts + 1);
			currentBurstCount = 0;
		}
		else
		{
			targetBurstCount = 1;
			currentBurstCount = 0;
		}
		StartFlickerBurst();
	}

	private void StartFlickerBurst()
	{
		isFlickering = true;
		flickerPhase = Random.value * 100f;
		float num = Random.Range(minFlickerDuration, maxFlickerDuration);
		flickerEndTime = Time.time + num;
		currentBurstCount++;
	}

	private void EndFlickerBurst()
	{
		isFlickering = false;
		targetLight.intensity = baseIntensity;
		if (currentBurstCount < targetBurstCount)
		{
			waitingForNextBurst = true;
			nextBurstTime = Time.time + timeBetweenBursts;
		}
		else
		{
			ScheduleNextFlicker();
		}
	}

	private void ScheduleNextFlicker()
	{
		float num = Random.Range(minTimeBetweenFlickers, maxTimeBetweenFlickers);
		nextFlickerTime = Time.time + num;
	}

	public void StartFlickering()
	{
		isActive = true;
		targetLight.intensity = baseIntensity;
		ScheduleNextFlicker();
	}

	public void StopFlickering()
	{
		isActive = false;
		isFlickering = false;
		waitingForNextBurst = false;
		targetLight.intensity = baseIntensity;
	}

	public void SetBaseIntensity(float intensity)
	{
		baseIntensity = intensity;
		if (!isFlickering)
		{
			targetLight.intensity = baseIntensity;
		}
	}

	public void TriggerFlicker()
	{
		if (isActive)
		{
			BeginFlickerEvent();
		}
	}
}
