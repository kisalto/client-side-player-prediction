using UnityEngine;
using UnityEngine.Rendering.Universal;

public class BillboardLightTrigger : MonoBehaviour
{
	private enum LightState
	{
		Off,
		Delaying,
		Flickering,
		FadingIn,
		On,
		FadingOut
	}

	[Header("Light References")]
	[SerializeField]
	private Light2D targetLight;

	[Header("Player Settings")]
	[SerializeField]
	private string playerTag = "Player";

	[Header("Activation Settings")]
	[SerializeField]
	private float activationDelay = 0.5f;

	[Header("Flicker Settings")]
	[SerializeField]
	private float flickerDuration = 0.5f;

	[SerializeField]
	private float minFlickerIntensity = 0.2f;

	[SerializeField]
	private float maxFlickerIntensity = 1f;

	[SerializeField]
	private float flickerSpeed = 15f;

	[Header("Light Settings")]
	[SerializeField]
	private float targetIntensity = 1f;

	[SerializeField]
	private float fadeInSpeed = 2f;

	[Header("Options")]
	[SerializeField]
	private bool turnOffWhenPlayerLeaves;

	[SerializeField]
	private float fadeOutSpeed = 1f;

	[SerializeField]
	private bool triggerOnce = true;

	private float initialIntensity;

	private bool hasTriggered;

	private bool playerInTrigger;

	private float delayTimer;

	private float flickerTimer;

	private float flickerPhase;

	private LightState currentState;

	private void Awake()
	{
		if (targetLight == null)
		{
			targetLight = GetComponent<Light2D>();
		}
		if (targetLight != null)
		{
			initialIntensity = targetLight.intensity;
			targetLight.intensity = 0f;
		}
		else
		{
			Debug.LogError("BillboardLightTrigger: No Light2D component found!", this);
			base.enabled = false;
		}
	}

	private void Update()
	{
		switch (currentState)
		{
		case LightState.Delaying:
			UpdateDelay();
			break;
		case LightState.Flickering:
			UpdateFlicker();
			break;
		case LightState.FadingIn:
			UpdateFadeIn();
			break;
		case LightState.FadingOut:
			UpdateFadeOut();
			break;
		case LightState.On:
			break;
		}
	}

	private void UpdateDelay()
	{
		delayTimer += Time.deltaTime;
		if (delayTimer >= activationDelay)
		{
			currentState = LightState.Flickering;
			flickerTimer = 0f;
			flickerPhase = 0f;
		}
	}

	private void UpdateFlicker()
	{
		flickerTimer += Time.deltaTime;
		flickerPhase += Time.deltaTime * flickerSpeed;
		float t = Mathf.PerlinNoise(flickerPhase, 0f);
		float num = Mathf.Lerp(minFlickerIntensity, maxFlickerIntensity, t);
		targetLight.intensity = num * targetIntensity;
		if (flickerTimer >= flickerDuration)
		{
			currentState = LightState.FadingIn;
		}
	}

	private void UpdateFadeIn()
	{
		targetLight.intensity = Mathf.MoveTowards(targetLight.intensity, targetIntensity, fadeInSpeed * Time.deltaTime);
		if (Mathf.Approximately(targetLight.intensity, targetIntensity))
		{
			currentState = LightState.On;
		}
	}

	private void UpdateFadeOut()
	{
		targetLight.intensity = Mathf.MoveTowards(targetLight.intensity, 0f, fadeOutSpeed * Time.deltaTime);
		if (Mathf.Approximately(targetLight.intensity, 0f))
		{
			currentState = LightState.Off;
			if (!triggerOnce)
			{
				hasTriggered = false;
			}
		}
	}

	private void StartLightSequence()
	{
		if ((!triggerOnce || !hasTriggered) && currentState == LightState.Off)
		{
			currentState = LightState.Delaying;
			delayTimer = 0f;
			hasTriggered = true;
		}
	}

	private void StopLightSequence()
	{
		if (turnOffWhenPlayerLeaves && currentState != LightState.Off && currentState != LightState.FadingOut)
		{
			currentState = LightState.FadingOut;
		}
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (other.CompareTag(playerTag))
		{
			playerInTrigger = true;
			StartLightSequence();
		}
	}

	private void OnTriggerExit2D(Collider2D other)
	{
		if (other.CompareTag(playerTag))
		{
			playerInTrigger = false;
			StopLightSequence();
		}
	}

	private void OnDrawGizmos()
	{
		BoxCollider2D component = GetComponent<BoxCollider2D>();
		if (!(component != null))
		{
			return;
		}
		Color color = Color.cyan;
		if (Application.isPlaying)
		{
			switch (currentState)
			{
			case LightState.Delaying:
				color = Color.yellow;
				break;
			case LightState.Flickering:
				color = new Color(1f, 0.5f, 0f);
				break;
			case LightState.FadingIn:
			case LightState.On:
				color = Color.green;
				break;
			case LightState.FadingOut:
				color = Color.red;
				break;
			}
		}
		else if (playerInTrigger)
		{
			color = Color.yellow;
		}
		Gizmos.color = color;
		Gizmos.matrix = base.transform.localToWorldMatrix;
		Gizmos.DrawWireCube(component.offset, component.size);
	}
}
