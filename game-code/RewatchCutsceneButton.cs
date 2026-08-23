using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Collider2D))]
public class RewatchCutsceneButton : MonoBehaviour
{
	[Header("Label")]
	[SerializeField]
	private TextMeshProUGUI label;

	[SerializeField]
	private string buttonText = "REWATCH CUTSCENE";

	[SerializeField]
	private Color normalColor = Color.white;

	[SerializeField]
	private Color selectedColor = new Color(1f, 1f, 0.6f, 1f);

	[SerializeField]
	private float colorTransitionSpeed = 8f;

	[Header("Lighting")]
	[SerializeField]
	private Light2D highlightLight;

	[SerializeField]
	private float lightFadeSpeed = 5f;

	[SerializeField]
	private float minIntensity = 0.2f;

	[SerializeField]
	private float maxIntensity = 1f;

	[SerializeField]
	private float pulseSpeed = 2f;

	[Header("Scale Animation")]
	[SerializeField]
	private bool enableScaleAnimation = true;

	[SerializeField]
	private Vector3 selectedScale = new Vector3(1.05f, 1.05f, 1f);

	[SerializeField]
	private float scaleSpeed = 8f;

	private OptionsMenuController optionsMenuController;

	private bool isSelected;

	private bool isHovered;

	private float currentLightIntensity;

	private float pulseTime;

	private Vector3 originalScale;

	private void Awake()
	{
		optionsMenuController = Object.FindAnyObjectByType<OptionsMenuController>();
		originalScale = base.transform.localScale;
		if (label != null)
		{
			label.text = buttonText;
		}
		if (highlightLight != null)
		{
			highlightLight.intensity = 0f;
			highlightLight.enabled = true;
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
		if (Mouse.current == null || (ScreenFader.Instance != null && ScreenFader.Instance.IsFading) || !OptionsMenuController.IsOpen)
		{
			return;
		}
		RaycastHit2D raycastHit2D = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()), Vector2.zero);
		if (raycastHit2D.collider != null && raycastHit2D.collider.gameObject == base.gameObject)
		{
			if (!isHovered)
			{
				isHovered = true;
				optionsMenuController?.SelectRewatchButton();
			}
			if (Mouse.current.leftButton.wasPressedThisFrame)
			{
				optionsMenuController?.OnRewatchPressed();
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

	private void UpdateLighting()
	{
		if (!(highlightLight == null))
		{
			float b = 0f;
			if (isSelected || isHovered)
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
		if (!(label == null))
		{
			Color b = ((isSelected || isHovered) ? selectedColor : normalColor);
			label.color = Color.Lerp(label.color, b, Time.deltaTime * colorTransitionSpeed);
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
}
