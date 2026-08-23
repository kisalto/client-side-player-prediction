using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class BarSignLightPulse : MonoBehaviour
{
	public enum PulsePattern
	{
		SmoothPulse,
		FastPulse,
		SlowBreathing,
		DoublePulse,
		TriplePulse,
		Shimmer
	}

	[Header("Light Reference")]
	[SerializeField]
	private Light2D targetLight;

	[Header("Base Light Settings")]
	[SerializeField]
	private float baseIntensity = 1f;

	[SerializeField]
	private bool setBaseIntensityOnStart = true;

	[Header("Pulse Settings")]
	[SerializeField]
	private PulsePattern pulsePattern = PulsePattern.TriplePulse;

	[SerializeField]
	private float minPulseSpeed = 0.5f;

	[SerializeField]
	private float maxPulseSpeed = 2f;

	[SerializeField]
	private float minPulseIntensity = 0.6f;

	[SerializeField]
	private float maxPulseIntensity = 2f;

	[Header("Pattern Variation")]
	[SerializeField]
	private bool randomizePattern = true;

	[SerializeField]
	private float minPatternChangeTime = 5f;

	[SerializeField]
	private float maxPatternChangeTime = 15f;

	[Header("Options")]
	[SerializeField]
	private bool startPulsingOnEnable = true;

	[SerializeField]
	private float initialDelay;

	private bool isActive;

	private float pulsePhase;

	private float currentPulseSpeed = 1f;

	private float nextPatternChangeTime;

	private PulsePattern currentPattern;

	private void Awake()
	{
		if (targetLight == null)
		{
			targetLight = GetComponent<Light2D>();
		}
		if (targetLight == null)
		{
			base.enabled = false;
			return;
		}
		if (setBaseIntensityOnStart)
		{
			baseIntensity = targetLight.intensity;
		}
		currentPattern = pulsePattern;
	}

	private void OnEnable()
	{
		if (startPulsingOnEnable)
		{
			if (initialDelay > 0f)
			{
				Invoke("StartPulsing", initialDelay);
			}
			else
			{
				StartPulsing();
			}
		}
	}

	private void OnDisable()
	{
		StopPulsing();
	}

	private void Update()
	{
		if (isActive)
		{
			if (randomizePattern && Time.time >= nextPatternChangeTime)
			{
				ChangePattern();
			}
			pulsePhase += Time.deltaTime * currentPulseSpeed;
			float num = CalculatePulseIntensity();
			targetLight.intensity = num * baseIntensity;
		}
	}

	private float CalculatePulseIntensity()
	{
		float t = 0f;
		switch (currentPattern)
		{
		case PulsePattern.SmoothPulse:
			t = (Mathf.Sin(pulsePhase) + 1f) * 0.5f;
			break;
		case PulsePattern.FastPulse:
			t = (Mathf.Sin(pulsePhase * 2f) + 1f) * 0.5f;
			break;
		case PulsePattern.SlowBreathing:
			t = (Mathf.Sin(pulsePhase * 0.5f) + 1f) * 0.5f;
			t = Mathf.Pow(t, 2f);
			break;
		case PulsePattern.DoublePulse:
		{
			float num6 = Mathf.Sin(pulsePhase);
			float num7 = Mathf.Sin(pulsePhase * 2f);
			t = ((num6 + num7) * 0.5f + 1f) * 0.5f;
			break;
		}
		case PulsePattern.TriplePulse:
		{
			float num3 = Mathf.Sin(pulsePhase);
			float num4 = Mathf.Sin(pulsePhase * 1.5f);
			float num5 = Mathf.Sin(pulsePhase * 2.5f);
			t = ((num3 + num4 + num5) / 3f + 1f) * 0.5f;
			break;
		}
		case PulsePattern.Shimmer:
		{
			float num = (Mathf.Sin(pulsePhase) + 1f) * 0.5f;
			float num2 = Mathf.PerlinNoise(pulsePhase * 5f, 0f);
			t = num * 0.7f + num2 * 0.3f;
			break;
		}
		}
		return Mathf.Lerp(minPulseIntensity, maxPulseIntensity, t);
	}

	private void ChangePattern()
	{
		int length = Enum.GetValues(typeof(PulsePattern)).Length;
		int num = UnityEngine.Random.Range(0, length);
		currentPattern = (PulsePattern)num;
		currentPulseSpeed = UnityEngine.Random.Range(minPulseSpeed, maxPulseSpeed);
		float num2 = UnityEngine.Random.Range(minPatternChangeTime, maxPatternChangeTime);
		nextPatternChangeTime = Time.time + num2;
	}

	public void StartPulsing()
	{
		isActive = true;
		currentPulseSpeed = UnityEngine.Random.Range(minPulseSpeed, maxPulseSpeed);
		pulsePhase = UnityEngine.Random.value * MathF.PI * 2f;
		if (randomizePattern)
		{
			float num = UnityEngine.Random.Range(minPatternChangeTime, maxPatternChangeTime);
			nextPatternChangeTime = Time.time + num;
		}
	}

	public void StopPulsing()
	{
		isActive = false;
		targetLight.intensity = baseIntensity;
	}

	public void SetBaseIntensity(float intensity)
	{
		baseIntensity = intensity;
	}

	public void SetPattern(PulsePattern pattern)
	{
		currentPattern = pattern;
		pulsePattern = pattern;
	}

	public void SetPulseSpeed(float speed)
	{
		currentPulseSpeed = speed;
	}
}
