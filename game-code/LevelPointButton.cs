using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class LevelPointButton : MonoBehaviour
{
	public enum PointType
	{
		Level,
		MainMenu,
		Inactive
	}

	private const float LightFadeSpeed = 5f;

	private const float MinLightIntensity = 0.8f;

	private const float MaxLightIntensity = 1.8f;

	private const float LightPulseSpeed = 2f;

	private const float ColorTransitionSpeed = 8f;

	private const float ScaleSpeed = 8f;

	private const float ScalePulseSpeed = 1f;

	private const float CoinFadeSpeed = 6f;

	private const string MainMenuSceneName = "MainMenu";

	[Header("Point Type")]
	[SerializeField]
	private PointType pointType;

	[Header("Level Data")]
	[SerializeField]
	private string targetSceneName;

	[SerializeField]
	private int pointIndex;

	[SerializeField]
	private bool isUnlocked = true;

	[Header("Directional Navigation")]
	[SerializeField]
	private LevelPointButton navigateUp;

	[SerializeField]
	private LevelPointButton navigateDown;

	[SerializeField]
	private LevelPointButton navigateLeft;

	[SerializeField]
	private LevelPointButton navigateRight;

	[SerializeField]
	private LevelPointButton navigateUpLeft;

	[SerializeField]
	private LevelPointButton navigateUpRight;

	[SerializeField]
	private LevelPointButton navigateDownLeft;

	[SerializeField]
	private LevelPointButton navigateDownRight;

	[Header("Visuals")]
	[SerializeField]
	private SpriteRenderer nodeRenderer;

	[SerializeField]
	private Transform nodeVisualTransform;

	[SerializeField]
	private Color unlockedColor = Color.white;

	[SerializeField]
	private Color selectedColor = new Color(1f, 1f, 0.75f, 1f);

	[SerializeField]
	private float selectedScaleMultiplier = 1.15f;

	[Header("Lighting")]
	[SerializeField]
	private Light2D highlightLight;

	[Header("Coin Display")]
	[SerializeField]
	private SpriteRenderer[] coinSlots = new SpriteRenderer[3];

	[SerializeField]
	private Sprite coinEarnedSprite;

	[SerializeField]
	private Sprite coinEmptySprite;

	[Header("Sequential Unlock - Path Lines")]
	[SerializeField]
	private GameObject[] pathLinesToThisNode;

	[SerializeField]
	private float lineRevealInterval = 0.25f;

	[SerializeField]
	private float nodeFadeInDuration = 0.4f;

	private bool isSelected;

	private bool isMarkerHere;

	private Vector3 originalVisualScale;

	private float lightPulseTimer;

	private float scalePulseTimer;

	private float currentLightIntensity;

	private float currentCoinAlpha;

	public int PointIndex => pointIndex;

	public bool IsUnlocked => isUnlocked;

	public PointType Type => pointType;

	public bool IsMarkerHere => isMarkerHere;

	private Transform VisualTransform
	{
		get
		{
			if (!(nodeVisualTransform != null))
			{
				return base.transform;
			}
			return nodeVisualTransform;
		}
	}

	private Vector3 ScaledUp => originalVisualScale * selectedScaleMultiplier;

	public event Action OnRevealComplete;

	private void Awake()
	{
		originalVisualScale = VisualTransform.localScale;
		if (nodeRenderer == null)
		{
			nodeRenderer = GetComponent<SpriteRenderer>();
		}
		if (highlightLight != null)
		{
			highlightLight.intensity = 0f;
			highlightLight.enabled = true;
		}
		if (!isUnlocked)
		{
			HidePathLines();
			base.gameObject.SetActive(value: false);
		}
		else
		{
			currentCoinAlpha = 0f;
			ApplyCoinAlpha(0f);
		}
	}

	private void Start()
	{
		RefreshCoinDisplay();
	}

	private void Update()
	{
		UpdateLighting();
		UpdateColor();
		UpdateScale();
		UpdateCoinVisibility();
	}

	public LevelPointButton GetNeighbour(LevelSelectDirection direction)
	{
		LevelPointButton levelPointButton = direction switch
		{
			LevelSelectDirection.Up => navigateUp, 
			LevelSelectDirection.Down => navigateDown, 
			LevelSelectDirection.Left => navigateLeft, 
			LevelSelectDirection.Right => navigateRight, 
			LevelSelectDirection.UpLeft => navigateUpLeft, 
			LevelSelectDirection.UpRight => navigateUpRight, 
			LevelSelectDirection.DownLeft => navigateDownLeft, 
			LevelSelectDirection.DownRight => navigateDownRight, 
			_ => null, 
		};
		if (!(levelPointButton != null) || !levelPointButton.IsUnlocked)
		{
			return null;
		}
		return levelPointButton;
	}

	public void SetSelected(bool selected)
	{
		isSelected = selected;
	}

	public void SetMarkerPresent(bool present)
	{
		isMarkerHere = present;
		if (isMarkerHere)
		{
			lightPulseTimer = 0f;
			scalePulseTimer = 0f;
		}
	}

	public void UnlockInstant()
	{
		if (isUnlocked && base.gameObject.activeSelf)
		{
			return;
		}
		isUnlocked = true;
		if (pathLinesToThisNode != null)
		{
			GameObject[] array = pathLinesToThisNode;
			foreach (GameObject gameObject in array)
			{
				if (gameObject != null)
				{
					gameObject.SetActive(value: true);
				}
			}
		}
		base.gameObject.SetActive(value: true);
		currentCoinAlpha = 0f;
		ApplyCoinAlpha(0f);
		RefreshCoinDisplay();
	}

	public void UnlockWithAnimation()
	{
		if (isUnlocked)
		{
			OnRevealComplete?.Invoke();
			return;
		}
		if (nodeRenderer != null)
		{
			Color color = nodeRenderer.color;
			color.a = 0f;
			nodeRenderer.color = color;
		}
		currentCoinAlpha = 0f;
		ApplyCoinAlpha(0f);
		base.gameObject.SetActive(value: true);
		StartCoroutine(RevealSequence());
	}

	public void PressButton()
	{
		if (!(ScreenFader.Instance != null) || !ScreenFader.Instance.IsFading)
		{
			SFXManager.Instance?.Play2D("UI_ButtonPress");
			switch (pointType)
			{
			case PointType.Level:
				PressLevelPoint();
				break;
			case PointType.MainMenu:
				PressMainMenuPoint();
				break;
			case PointType.Inactive:
				break;
			}
		}
	}

	public void RefreshCoinDisplay()
	{
		if (pointType != PointType.Level || coinSlots == null || coinSlots.Length < 3)
		{
			return;
		}
		SpriteRenderer[] array = coinSlots;
		foreach (SpriteRenderer spriteRenderer in array)
		{
			if (spriteRenderer != null)
			{
				spriteRenderer.gameObject.SetActive(value: true);
			}
		}
		if (SaveSlotManager.Instance == null || SaveSlotManager.Instance.GetCurrentSlot() == null)
		{
			SetAllCoinsEmpty();
			return;
		}
		LevelSaveData levelProgress = SaveSlotManager.Instance.GetLevelProgress(pointIndex);
		if (levelProgress == null)
		{
			SetAllCoinsEmpty();
			return;
		}
		SetCoinSlot(0, levelProgress.coinEnemiesKilled);
		SetCoinSlot(1, levelProgress.coinTimerRequirement);
		SetCoinSlot(2, levelProgress.coinBonusFound);
	}

	private IEnumerator RevealSequence()
	{
		if (pathLinesToThisNode != null)
		{
			GameObject[] array = pathLinesToThisNode;
			foreach (GameObject gameObject in array)
			{
				if (!(gameObject == null))
				{
					gameObject.SetActive(value: true);
					yield return new WaitForSeconds(lineRevealInterval);
				}
			}
		}
		isUnlocked = true;
		if (nodeRenderer != null)
		{
			Color c = nodeRenderer.color;
			c.a = 0f;
			nodeRenderer.color = c;
			float elapsed = 0f;
			while (elapsed < nodeFadeInDuration)
			{
				elapsed += Time.deltaTime;
				c.a = Mathf.Clamp01(elapsed / nodeFadeInDuration);
				nodeRenderer.color = c;
				yield return null;
			}
			c.a = 1f;
			nodeRenderer.color = c;
		}
		RefreshCoinDisplay();
		OnRevealComplete?.Invoke();
	}

	private void PressLevelPoint()
	{
		if (!isUnlocked)
		{
			Debug.Log($"[LEVEL SELECT] Level {pointIndex + 1} is locked.");
			return;
		}
		if (ScreenFader.Instance != null)
		{
			ScreenFader.Instance.LoadScene(targetSceneName);
			return;
		}
		Debug.LogWarning("[LEVEL SELECT] ScreenFader not found. Loading scene directly.");
		SceneManager.LoadScene(targetSceneName);
	}

	private void PressMainMenuPoint()
	{
		if (ScreenFader.Instance != null)
		{
			ScreenFader.Instance.LoadScene("MainMenu");
			return;
		}
		Debug.LogWarning("[LEVEL SELECT] ScreenFader not found. Loading scene directly.");
		SceneManager.LoadScene("MainMenu");
	}

	private void UpdateScale()
	{
		if (isMarkerHere)
		{
			scalePulseTimer += Time.deltaTime * 1f;
			float t = (Mathf.Sin(scalePulseTimer * MathF.PI * 2f) + 1f) * 0.5f;
			VisualTransform.localScale = Vector3.Lerp(originalVisualScale, ScaledUp, t);
		}
		else if (isSelected)
		{
			VisualTransform.localScale = Vector3.Lerp(VisualTransform.localScale, ScaledUp, Time.deltaTime * 8f);
		}
		else
		{
			VisualTransform.localScale = Vector3.Lerp(VisualTransform.localScale, originalVisualScale, Time.deltaTime * 8f);
		}
	}

	private void UpdateLighting()
	{
		if (!(highlightLight == null))
		{
			float b = 0f;
			if (isMarkerHere)
			{
				lightPulseTimer += Time.deltaTime * 2f;
				float t = (Mathf.Sin(lightPulseTimer * MathF.PI) + 1f) * 0.5f;
				b = Mathf.Lerp(0.8f, 1.8f, t);
			}
			currentLightIntensity = Mathf.Lerp(currentLightIntensity, b, Time.deltaTime * 5f);
			highlightLight.intensity = currentLightIntensity;
		}
	}

	private void UpdateColor()
	{
		if (!(nodeRenderer == null))
		{
			Color b = (isSelected ? selectedColor : unlockedColor);
			nodeRenderer.color = Color.Lerp(nodeRenderer.color, b, Time.deltaTime * 8f);
		}
	}

	private void UpdateCoinVisibility()
	{
		if (pointType == PointType.Level)
		{
			float b = (isMarkerHere ? 1f : 0f);
			currentCoinAlpha = Mathf.Lerp(currentCoinAlpha, b, Time.deltaTime * 6f);
			ApplyCoinAlpha(currentCoinAlpha);
		}
	}

	private void ApplyCoinAlpha(float alpha)
	{
		if (coinSlots == null)
		{
			return;
		}
		SpriteRenderer[] array = coinSlots;
		foreach (SpriteRenderer spriteRenderer in array)
		{
			if (!(spriteRenderer == null))
			{
				Color color = spriteRenderer.color;
				color.a = alpha;
				spriteRenderer.color = color;
			}
		}
	}

	private void SetCoinSlot(int index, bool earned)
	{
		if (index >= 0 && index < coinSlots.Length && !(coinSlots[index] == null))
		{
			coinSlots[index].sprite = (earned ? coinEarnedSprite : coinEmptySprite);
		}
	}

	private void SetAllCoinsEmpty()
	{
		for (int i = 0; i < coinSlots.Length; i++)
		{
			SetCoinSlot(i, earned: false);
		}
	}

	private void HidePathLines()
	{
		if (pathLinesToThisNode == null)
		{
			return;
		}
		GameObject[] array = pathLinesToThisNode;
		foreach (GameObject gameObject in array)
		{
			if (gameObject != null)
			{
				gameObject.SetActive(value: false);
			}
		}
	}
}
