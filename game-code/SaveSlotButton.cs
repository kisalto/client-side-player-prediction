using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Collider2D))]
public class SaveSlotButton : MonoBehaviour
{
	[Header("Slot Settings")]
	[SerializeField]
	private int slotIndex;

	[Header("UI")]
	[SerializeField]
	private TextMeshProUGUI slotLabel;

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
	private Color selectedColor = new Color(1f, 1f, 0.9f, 1f);

	[SerializeField]
	private float colorTransitionSpeed = 8f;

	[Header("Scale Animation")]
	[SerializeField]
	private bool enableScaleAnimation = true;

	[SerializeField]
	private Vector3 selectedScale = new Vector3(1.05f, 1.05f, 1f);

	[SerializeField]
	private float scaleSpeed = 8f;

	private const string EmptyLabel = "EMPTY";

	private bool isSelected;

	private bool isHovered;

	private bool isEmpty;

	private float currentLightIntensity;

	private float pulseTime;

	private Color currentColor;

	private Vector3 originalScale;

	private SaveSlotMenuController menuController;

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
		menuController = Object.FindAnyObjectByType<SaveSlotMenuController>();
	}

	private void Start()
	{
		menuController?.RegisterSlotButton(this);
		RefreshDisplay();
	}

	private void Update()
	{
		HandleMouseInput();
		UpdateLighting();
		UpdateColor();
		UpdateScale();
	}

	public void RefreshDisplay()
	{
		if (!(SaveSlotManager.Instance == null))
		{
			SaveSlotInfo slotInfo = SaveSlotManager.Instance.GetSlotInfo(slotIndex);
			isEmpty = slotInfo.isEmpty;
			if (slotLabel != null)
			{
				slotLabel.text = (isEmpty ? "EMPTY" : $"Slot {slotIndex + 1}");
			}
		}
	}

	private void HandleMouseInput()
	{
		if (Mouse.current == null)
		{
			return;
		}
		if (ScreenFader.Instance != null && ScreenFader.Instance.IsFading)
		{
			if (isHovered)
			{
				isHovered = false;
			}
			return;
		}
		RaycastHit2D raycastHit2D = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()), Vector2.zero);
		if (raycastHit2D.collider != null && raycastHit2D.collider.gameObject == base.gameObject)
		{
			if (!isHovered)
			{
				isHovered = true;
				menuController?.SetSelectedButton(this);
			}
			if (Mouse.current.leftButton.wasPressedThisFrame)
			{
				PressButton();
			}
		}
		else if (isHovered)
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

	public void PressButton()
	{
		if (!(ScreenFader.Instance != null) || !ScreenFader.Instance.IsFading)
		{
			SFXManager.Instance?.Play2D("UI_ButtonPress");
			menuController?.OnSlotPressed(slotIndex, isEmpty);
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
				float t = Mathf.PingPong(pulseTime, 1f);
				b = Mathf.Lerp(minIntensity, maxIntensity, t);
			}
			currentLightIntensity = Mathf.Lerp(currentLightIntensity, b, Time.deltaTime * lightFadeSpeed);
			highlightLight.intensity = currentLightIntensity;
		}
	}

	private void UpdateColor()
	{
		if (!(spriteRenderer == null))
		{
			Color b = ((isSelected || isHovered) ? selectedColor : normalColor);
			currentColor = Color.Lerp(currentColor, b, Time.deltaTime * colorTransitionSpeed);
			spriteRenderer.color = currentColor;
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

	private void OnDestroy()
	{
		menuController?.UnregisterSlotButton(this);
	}
}
