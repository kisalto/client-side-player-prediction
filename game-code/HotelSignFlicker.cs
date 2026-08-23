using UnityEngine;
using UnityEngine.Rendering.Universal;

public class HotelSignFlicker : MonoBehaviour
{
	public enum HotelSignMode
	{
		AlwaysOn,
		OccasionalFlicker,
		ConstantBuzzing,
		DyingSign,
		BuzzingWithFlicker
	}

	private enum FlickerState
	{
		On,
		FlickeringOut,
		Off,
		FlickeringBack
	}

	[Header("Light Reference")]
	[SerializeField]
	private Light2D targetLight;

	[Header("Base Light Settings")]
	[SerializeField]
	private float baseIntensity = 1f;

	[SerializeField]
	private bool setBaseIntensityOnStart = true;

	[Header("Hotel Sign Pattern")]
	[SerializeField]
	private HotelSignMode signMode = HotelSignMode.OccasionalFlicker;

	[Header("Occasional Flicker Settings")]
	[SerializeField]
	private float minTimeBetweenFlickers = 5f;

	[SerializeField]
	private float maxTimeBetweenFlickers = 20f;

	[SerializeField]
	private float flickerOutDuration = 0.2f;

	[SerializeField]
	private float offDuration = 0.3f;

	[SerializeField]
	private float flickerBackDuration = 0.4f;

	[SerializeField]
	private int minFlickerRepeats = 1;

	[SerializeField]
	private int maxFlickerRepeats = 3;

	[Header("Buzzing Settings")]
	[SerializeField]
	private float buzzIntensityVariation = 0.15f;

	[SerializeField]
	private float buzzSpeed = 25f;

	[Header("Dying Sign Settings")]
	[SerializeField]
	private float dyingFlickerChance = 0.3f;

	[SerializeField]
	private float minDyingInterval = 0.5f;

	[SerializeField]
	private float maxDyingInterval = 3f;

	[Header("Options")]
	[SerializeField]
	private bool startOnEnable = true;

	[SerializeField]
	private float initialDelay;

	private bool isActive;

	private float nextFlickerTime;

	private FlickerState currentState;

	private float stateTimer;

	private int remainingRepeats;

	private float buzzPhase;

	private void Awake()
	{
		if (targetLight == null)
		{
			targetLight = GetComponent<Light2D>();
		}
		if (targetLight == null)
		{
			Debug.LogError("HotelSignFlicker: No Light2D component found!", this);
			base.enabled = false;
		}
		else if (setBaseIntensityOnStart)
		{
			baseIntensity = targetLight.intensity;
		}
	}

	private void OnEnable()
	{
		if (startOnEnable)
		{
			if (initialDelay > 0f)
			{
				Invoke("StartSign", initialDelay);
			}
			else
			{
				StartSign();
			}
		}
	}

	private void OnDisable()
	{
		StopSign();
	}

	private void Update()
	{
		if (isActive)
		{
			switch (signMode)
			{
			case HotelSignMode.AlwaysOn:
				targetLight.intensity = baseIntensity;
				break;
			case HotelSignMode.OccasionalFlicker:
				UpdateOccasionalFlicker();
				break;
			case HotelSignMode.ConstantBuzzing:
				UpdateBuzzing();
				break;
			case HotelSignMode.DyingSign:
				UpdateDyingSign();
				break;
			case HotelSignMode.BuzzingWithFlicker:
				UpdateBuzzingWithFlicker();
				break;
			}
		}
	}

	private void UpdateOccasionalFlicker()
	{
		switch (currentState)
		{
		case FlickerState.On:
			targetLight.intensity = baseIntensity;
			if (Time.time >= nextFlickerTime)
			{
				StartFlickerSequence();
			}
			break;
		case FlickerState.FlickeringOut:
		{
			stateTimer += Time.deltaTime;
			float t2 = stateTimer / flickerOutDuration;
			targetLight.intensity = Mathf.Lerp(baseIntensity, 0f, t2) * Random.Range(0.3f, 1f);
			if (stateTimer >= flickerOutDuration)
			{
				currentState = FlickerState.Off;
				stateTimer = 0f;
			}
			break;
		}
		case FlickerState.Off:
			targetLight.intensity = 0f;
			stateTimer += Time.deltaTime;
			if (stateTimer >= offDuration)
			{
				currentState = FlickerState.FlickeringBack;
				stateTimer = 0f;
			}
			break;
		case FlickerState.FlickeringBack:
		{
			stateTimer += Time.deltaTime;
			float t = stateTimer / flickerBackDuration;
			targetLight.intensity = Mathf.Lerp(0f, baseIntensity, t) * Random.Range(0.5f, 1f);
			if (stateTimer >= flickerBackDuration)
			{
				remainingRepeats--;
				if (remainingRepeats > 0)
				{
					currentState = FlickerState.FlickeringOut;
					stateTimer = 0f;
				}
				else
				{
					currentState = FlickerState.On;
					ScheduleNextFlicker();
				}
			}
			break;
		}
		}
	}

	private void UpdateBuzzing()
	{
		buzzPhase += Time.deltaTime * buzzSpeed;
		float num = Mathf.PerlinNoise(buzzPhase, 0f);
		float num2 = 1f - buzzIntensityVariation * num;
		targetLight.intensity = baseIntensity * num2;
	}

	private void UpdateDyingSign()
	{
		if ((currentState == FlickerState.On || currentState == FlickerState.Off) && Time.time >= nextFlickerTime)
		{
			if (Random.value < dyingFlickerChance)
			{
				currentState = ((currentState == FlickerState.On) ? FlickerState.Off : FlickerState.On);
				targetLight.intensity = ((currentState == FlickerState.On) ? baseIntensity : 0f);
			}
			float num = Random.Range(minDyingInterval, maxDyingInterval);
			nextFlickerTime = Time.time + num;
		}
	}

	private void UpdateBuzzingWithFlicker()
	{
		switch (currentState)
		{
		case FlickerState.On:
		{
			buzzPhase += Time.deltaTime * buzzSpeed;
			float num = Mathf.PerlinNoise(buzzPhase, 0f);
			float num2 = 1f - buzzIntensityVariation * num;
			targetLight.intensity = baseIntensity * num2;
			if (Time.time >= nextFlickerTime)
			{
				StartFlickerSequence();
			}
			break;
		}
		case FlickerState.FlickeringOut:
		case FlickerState.Off:
		case FlickerState.FlickeringBack:
			UpdateOccasionalFlicker();
			break;
		}
	}

	private void StartFlickerSequence()
	{
		currentState = FlickerState.FlickeringOut;
		stateTimer = 0f;
		remainingRepeats = Random.Range(minFlickerRepeats, maxFlickerRepeats + 1);
	}

	private void ScheduleNextFlicker()
	{
		float num = Random.Range(minTimeBetweenFlickers, maxTimeBetweenFlickers);
		nextFlickerTime = Time.time + num;
	}

	public void StartSign()
	{
		isActive = true;
		currentState = FlickerState.On;
		targetLight.intensity = baseIntensity;
		ScheduleNextFlicker();
		buzzPhase = Random.value * 100f;
	}

	public void StopSign()
	{
		isActive = false;
		targetLight.intensity = 0f;
	}

	public void SetSignMode(HotelSignMode mode)
	{
		signMode = mode;
		currentState = FlickerState.On;
		stateTimer = 0f;
	}

	public void SetBaseIntensity(float intensity)
	{
		baseIntensity = intensity;
	}

	public void TurnOff()
	{
		isActive = false;
		targetLight.intensity = 0f;
	}

	public void TurnOn()
	{
		isActive = true;
		targetLight.intensity = baseIntensity;
	}
}
