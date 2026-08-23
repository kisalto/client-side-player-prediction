using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Collider2D))]
public class MenuButton : MonoBehaviour
{
	public enum ButtonType
	{
		Start,
		Options,
		Exit
	}

	public enum PulseMode
	{
		Smooth,
		Heartbeat,
		Flicker,
		Wave
	}

	[Header("Button Settings")]
	[SerializeField]
	private ButtonType buttonType;

	[Header("Lighting")]
	[SerializeField]
	private Light2D highlightLight;

	[SerializeField]
	private float lightFadeSpeed = 5f;

	[Header("Pulse Settings")]
	[SerializeField]
	private PulseMode pulseMode;

	[SerializeField]
	private float minIntensity = 0.8f;

	[SerializeField]
	private float maxIntensity = 1.8f;

	[SerializeField]
	private float pulseSpeed = 2f;

	[SerializeField]
	private AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	[Header("Heartbeat Settings")]
	[SerializeField]
	private float heartbeatPauseDuration = 0.5f;

	[Header("Flicker Settings")]
	[SerializeField]
	private float flickerScale = 10f;

	[SerializeField]
	private float flickerSeed;

	[Header("Visual Feedback")]
	[SerializeField]
	private SpriteRenderer spriteRenderer;

	[SerializeField]
	private Color normalColor = Color.white;

	[SerializeField]
	private Color selectedColor = new Color(1f, 1f, 0.9f, 1f);

	[SerializeField]
	private float colorTransitionSpeed = 8f;

	[SerializeField]
	private TextMeshPro label;

	[SerializeField]
	private Color normalTextColor = Color.white;

	[SerializeField]
	private Color selectedTextColor = new Color(1f, 1f, 0.7f, 1f);

	[Header("Scale Animation")]
	[SerializeField]
	private bool enableScaleAnimation = true;

	[SerializeField]
	private Vector3 selectedScale = new Vector3(1.05f, 1.05f, 1f);

	[SerializeField]
	private float scaleSpeed = 8f;

	[Header("Events")]
	[SerializeField]
	private UnityEvent onButtonClick;

	[Header("Navigation")]
	[Tooltip("Uncheck for buttons managed by their own controller (e.g. Rewatch Cutscene).")]
	[SerializeField]
	private bool registerWithNavigationController = true;

	private bool isSelected;

	private bool isHovered;

	private float currentLightIntensity;

	private float pulseTime;

	private Color currentColor;

	private Vector3 originalScale;

	private MainMenuController menuController;

	private MenuNavigationController navigationController;

	private void Awake()
	{
		if (spriteRenderer == null)
		{
			spriteRenderer = GetComponent<SpriteRenderer>();
		}
		currentColor = normalColor;
		originalScale = base.transform.localScale;
		if (highlightLight != null)
		{
			highlightLight.intensity = 0f;
			highlightLight.enabled = true;
		}
		menuController = UnityEngine.Object.FindObjectOfType<MainMenuController>();
		navigationController = UnityEngine.Object.FindObjectOfType<MenuNavigationController>();
		flickerSeed = UnityEngine.Random.Range(0f, 1000f);
	}

	private void Start()
	{
		if (registerWithNavigationController && navigationController != null)
		{
			navigationController.RegisterButton(this);
		}
	}

	private void Update()
	{
		HandleMouseInput();
		UpdateLighting();
		UpdateColor();
		UpdateScale();
	}

	private void HandleMouseInput()
	{
		if (Mouse.current == null || (ScreenFader.Instance != null && ScreenFader.Instance.IsFading) || (menuController != null && menuController.IsMoving()))
		{
			return;
		}
		if (SaveSlotMenuController.IsOpen)
		{
			if (isHovered)
			{
				OnHoverExit();
			}
			return;
		}
		RaycastHit2D raycastHit2D = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()), Vector2.zero);
		if (raycastHit2D.collider != null && raycastHit2D.collider.gameObject == base.gameObject)
		{
			if (!isHovered)
			{
				OnHoverEnter();
			}
			if (Mouse.current.leftButton.wasPressedThisFrame)
			{
				PressButton();
			}
		}
		else if (isHovered)
		{
			OnHoverExit();
		}
	}

	private void OnHoverEnter()
	{
		isHovered = true;
		if (navigationController != null)
		{
			navigationController.SetSelectedButton(this);
		}
	}

	private void OnHoverExit()
	{
		isHovered = false;
	}

	public void SetSelected(bool selected)
	{
		isSelected = selected;
		if (selected)
		{
			SFXManager.Instance?.Play2D("UI_ButtonHover");
		}
		if (isSelected)
		{
			pulseTime = 0f;
		}
	}

	private void UpdateLighting()
	{
		if (!(highlightLight == null))
		{
			float b = 0f;
			if (isSelected || isHovered)
			{
				pulseTime += Time.deltaTime * pulseSpeed;
				float t = CalculatePulseValue();
				b = Mathf.Lerp(minIntensity, maxIntensity, t);
			}
			currentLightIntensity = Mathf.Lerp(currentLightIntensity, b, Time.deltaTime * lightFadeSpeed);
			highlightLight.intensity = currentLightIntensity;
		}
	}

	private float CalculatePulseValue()
	{
		return pulseMode switch
		{
			PulseMode.Smooth => CalculateSmoothPulse(), 
			PulseMode.Heartbeat => CalculateHeartbeatPulse(), 
			PulseMode.Flicker => CalculateFlickerPulse(), 
			PulseMode.Wave => CalculateWavePulse(), 
			_ => 0f, 
		};
	}

	private float CalculateSmoothPulse()
	{
		float time = Mathf.PingPong(pulseTime, 1f);
		return pulseCurve.Evaluate(time);
	}

	private float CalculateHeartbeatPulse()
	{
		float num = 1f + heartbeatPauseDuration;
		float num2 = pulseTime % num;
		if (num2 < 0.15f)
		{
			return Mathf.Sin(num2 / 0.15f * MathF.PI);
		}
		if (num2 < 0.45f)
		{
			return Mathf.Sin((num2 - 0.15f) / 0.3f * MathF.PI) * 0.6f;
		}
		return 0f;
	}

	private float CalculateFlickerPulse()
	{
		return Mathf.PerlinNoise(pulseTime * flickerScale, flickerSeed);
	}

	private float CalculateWavePulse()
	{
		return (Mathf.Sin(pulseTime * MathF.PI) + 1f) * 0.5f;
	}

	private void UpdateColor()
	{
		bool flag = isSelected || isHovered;
		if (spriteRenderer != null)
		{
			Color b = (flag ? selectedColor : normalColor);
			currentColor = Color.Lerp(currentColor, b, Time.deltaTime * colorTransitionSpeed);
			spriteRenderer.color = currentColor;
		}
		if (label != null)
		{
			Color b2 = (flag ? selectedTextColor : normalTextColor);
			label.color = Color.Lerp(label.color, b2, Time.deltaTime * colorTransitionSpeed);
		}
	}

	private void UpdateScale()
	{
		if (enableScaleAnimation)
		{
			Vector3 b = ((isSelected || isHovered) ? selectedScale : originalScale);
			base.transform.localScale = Vector3.Lerp(base.transform.localScale, b, Time.deltaTime * scaleSpeed);
		}
	}

	public void PressButton()
	{
		if (!(ScreenFader.Instance != null) || !ScreenFader.Instance.IsFading)
		{
			SFXManager.Instance?.Play2D("UI_ButtonPress");
			onButtonClick?.Invoke();
			switch (buttonType)
			{
			case ButtonType.Start:
				menuController?.OnStartButtonPressed();
				break;
			case ButtonType.Options:
				menuController?.OnOptionsButtonPressed();
				break;
			case ButtonType.Exit:
				OnExitPressed();
				break;
			}
		}
	}

	private void OnExitPressed()
	{
		if (ScreenFader.Instance != null)
		{
			ScreenFader.Instance.FadeToBlack(delegate
			{
				Application.Quit();
			});
		}
		else
		{
			Application.Quit();
		}
	}

	private void OnDestroy()
	{
		if (registerWithNavigationController && navigationController != null)
		{
			navigationController.UnregisterButton(this);
		}
	}

	private void OnDrawGizmosSelected()
	{
		Collider2D component = GetComponent<Collider2D>();
		if (component != null)
		{
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireCube(component.bounds.center, component.bounds.size);
		}
	}
}
