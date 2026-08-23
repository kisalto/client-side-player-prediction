using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Collider2D))]
public class BackButton : MonoBehaviour
{
	private const string ButtonLabel = "BACK";

	[Header("Scene References")]
	[SerializeField]
	private MainMenuController mainMenuController;

	[SerializeField]
	private SaveSlotMenuController saveSlotMenuController;

	[SerializeField]
	private OptionsMenuController optionsMenuController;

	[Header("UI")]
	[SerializeField]
	private TextMeshProUGUI label;

	[Header("Lighting")]
	[SerializeField]
	private Light2D highlightLight;

	[SerializeField]
	private float lightFadeSpeed = 5f;

	[SerializeField]
	private float minIntensity = 0.8f;

	[SerializeField]
	private float maxIntensity = 1.8f;

	[SerializeField]
	private float pulseSpeed = 2f;

	[Header("Visual Feedback")]
	[SerializeField]
	private SpriteRenderer spriteRenderer;

	[SerializeField]
	private Color normalColor = Color.white;

	[SerializeField]
	private Color hoveredColor = new Color(1f, 0.85f, 0.85f, 1f);

	[SerializeField]
	private float colorTransitionSpeed = 8f;

	[Header("Scale Animation")]
	[SerializeField]
	private bool enableScaleAnimation = true;

	[SerializeField]
	private Vector3 hoveredScale = new Vector3(1.05f, 1.05f, 1f);

	[SerializeField]
	private float scaleSpeed = 8f;

	private bool isHovered;

	private bool isSelected;

	private float currentLightIntensity;

	private float pulseTime;

	private Color currentColor;

	private Vector3 originalScale;

	private void Awake()
	{
		if (spriteRenderer == null)
		{
			spriteRenderer = GetComponent<SpriteRenderer>();
		}
		if (mainMenuController == null)
		{
			mainMenuController = Object.FindAnyObjectByType<MainMenuController>();
		}
		if (saveSlotMenuController == null)
		{
			saveSlotMenuController = Object.FindAnyObjectByType<SaveSlotMenuController>();
		}
		currentColor = normalColor;
		originalScale = base.transform.localScale;
		if (highlightLight != null)
		{
			highlightLight.intensity = 0f;
			highlightLight.enabled = true;
		}
		if (label != null)
		{
			label.text = "BACK";
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
		if (Mouse.current == null)
		{
			return;
		}
		RaycastHit2D raycastHit2D = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()), Vector2.zero);
		if (raycastHit2D.collider != null && raycastHit2D.collider.gameObject == base.gameObject)
		{
			if (!isHovered)
			{
				isHovered = true;
				saveSlotMenuController?.SelectBackButton();
				optionsMenuController?.SelectBackButton();
			}
			if (Mouse.current.leftButton.wasPressedThisFrame)
			{
				PressBack();
			}
		}
		else
		{
			isHovered = false;
		}
	}

	public void SetSelected(bool selected)
	{
		isSelected = selected;
		if (isSelected)
		{
			pulseTime = 0f;
		}
	}

	public void PressBack()
	{
		if (!(ScreenFader.Instance != null) || !ScreenFader.Instance.IsFading)
		{
			SFXManager.Instance?.Play2D("UI_ButtonPress");
			saveSlotMenuController?.Deactivate();
			mainMenuController?.OnBackToMainMenu();
		}
	}

	private void UpdateLighting()
	{
		if (!(highlightLight == null))
		{
			float b = 0f;
			if (isHovered || isSelected)
			{
				pulseTime += Time.deltaTime * pulseSpeed;
				b = Mathf.Lerp(minIntensity, maxIntensity, Mathf.PingPong(pulseTime, 1f));
			}
			currentLightIntensity = Mathf.Lerp(currentLightIntensity, b, Time.deltaTime * lightFadeSpeed);
			highlightLight.intensity = currentLightIntensity;
		}
	}

	private void UpdateColor()
	{
		if (!(spriteRenderer == null))
		{
			Color b = ((isHovered || isSelected) ? hoveredColor : normalColor);
			currentColor = Color.Lerp(currentColor, b, Time.deltaTime * colorTransitionSpeed);
			spriteRenderer.color = currentColor;
		}
	}

	private void UpdateScale()
	{
		if (enableScaleAnimation)
		{
			Vector3 b = ((isHovered || isSelected) ? hoveredScale : originalScale);
			base.transform.localScale = Vector3.Lerp(base.transform.localScale, b, Time.deltaTime * scaleSpeed);
		}
	}
}
