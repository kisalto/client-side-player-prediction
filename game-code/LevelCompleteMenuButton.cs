using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class LevelCompleteMenuButton : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler
{
	[Header("Navigation Order")]
	[Tooltip("Determines which button is selected first when the panel opens. 0 = first.")]
	[SerializeField]
	private int navigationOrder;

	[Header("Directional Navigation")]
	[Tooltip("Empty = navigation blocked in that direction.")]
	[SerializeField]
	private LevelCompleteMenuButton navigateUp;

	[SerializeField]
	private LevelCompleteMenuButton navigateDown;

	[SerializeField]
	private LevelCompleteMenuButton navigateLeft;

	[SerializeField]
	private LevelCompleteMenuButton navigateRight;

	[SerializeField]
	private LevelCompleteMenuButton navigateUpLeft;

	[SerializeField]
	private LevelCompleteMenuButton navigateUpRight;

	[SerializeField]
	private LevelCompleteMenuButton navigateDownLeft;

	[SerializeField]
	private LevelCompleteMenuButton navigateDownRight;

	[Header("Pulse Settings")]
	[SerializeField]
	private float pulseSpeed = 3f;

	[SerializeField]
	private float pulseAmount = 0.06f;

	[Header("Scale Settings")]
	[SerializeField]
	private float selectedScale = 1.08f;

	[SerializeField]
	private float scaleSpeed = 10f;

	[Header("Brightness Tint on Select")]
	[SerializeField]
	private float selectedBrightness = 1.25f;

	[SerializeField]
	private float colorSpeed = 10f;

	private Button button;

	private Graphic[] graphics;

	private Color[] originalColors;

	private Vector3 baseScale;

	private float currentMultiplier = 1f;

	private float pulseTime;

	private bool isActive;

	private LevelCompleteNavigationController navigationController;

	public int NavigationOrder => navigationOrder;

	private void Awake()
	{
		button = GetComponent<Button>();
		graphics = GetComponentsInChildren<Graphic>();
		baseScale = base.transform.localScale;
		originalColors = new Color[graphics.Length];
		for (int i = 0; i < graphics.Length; i++)
		{
			originalColors[i] = graphics[i].color;
		}
	}

	private void Start()
	{
		navigationController = GetComponentInParent<LevelCompleteNavigationController>();
		if (navigationController != null)
		{
			navigationController.RegisterButton(this);
		}
	}

	private void Update()
	{
		UpdateScale();
		UpdateColor();
	}

	public LevelCompleteMenuButton GetNeighbour(NavigationDirection direction)
	{
		return direction switch
		{
			NavigationDirection.Up => navigateUp, 
			NavigationDirection.Down => navigateDown, 
			NavigationDirection.Left => navigateLeft, 
			NavigationDirection.Right => navigateRight, 
			NavigationDirection.UpLeft => navigateUpLeft, 
			NavigationDirection.UpRight => navigateUpRight, 
			NavigationDirection.DownLeft => navigateDownLeft, 
			NavigationDirection.DownRight => navigateDownRight, 
			_ => null, 
		};
	}

	public void SetActive(bool active)
	{
		isActive = active;
		pulseTime = 0f;
	}

	public void Press()
	{
		if (!(ScreenFader.Instance != null) || !ScreenFader.Instance.IsFading)
		{
			button.onClick.Invoke();
		}
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		if ((!(ScreenFader.Instance != null) || !ScreenFader.Instance.IsFading) && navigationController != null)
		{
			navigationController.SelectButton(this);
		}
	}

	public void OnPointerExit(PointerEventData eventData)
	{
	}

	private void UpdateScale()
	{
		float b;
		if (isActive)
		{
			pulseTime += Time.unscaledDeltaTime * pulseSpeed;
			float num = Mathf.Sin(pulseTime * MathF.PI * 2f) * pulseAmount;
			b = selectedScale + num;
		}
		else
		{
			b = 1f;
		}
		currentMultiplier = Mathf.Lerp(currentMultiplier, b, Time.unscaledDeltaTime * scaleSpeed);
		base.transform.localScale = baseScale * currentMultiplier;
	}

	private void UpdateColor()
	{
		for (int i = 0; i < graphics.Length; i++)
		{
			Color b = (isActive ? (originalColors[i] * selectedBrightness) : originalColors[i]);
			b.a = originalColors[i].a;
			graphics[i].color = Color.Lerp(graphics[i].color, b, Time.unscaledDeltaTime * colorSpeed);
		}
	}

	private void OnDestroy()
	{
		if (navigationController != null)
		{
			navigationController.UnregisterButton(this);
		}
	}
}
