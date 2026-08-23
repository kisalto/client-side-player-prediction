using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
	private const string LevelSelectSceneName = "LevelSelect";

	private const string MainMenuSceneName = "MainMenu";

	private const float NavigationCooldown = 0.15f;

	private const float StickDeadzone = 0.3f;

	[Header("References")]
	[Tooltip("The PauseCanvas root GameObject to show/hide.")]
	[SerializeField]
	private GameObject pauseCanvas;

	[SerializeField]
	private PlayerInputReader inputReader;

	[Header("Buttons - left to right order defines navigation")]
	[Tooltip("Drag Play, Restart, Level Select buttons here in left-to-right order.")]
	[SerializeField]
	private Button[] buttons;

	[Header("Selection Visuals")]
	[SerializeField]
	private Color normalColor = Color.white;

	[SerializeField]
	private Color selectedColor = new Color(1f, 0.75f, 0.2f, 1f);

	[SerializeField]
	private float selectedScale = 1.15f;

	[SerializeField]
	private float transitionSpeed = 12f;

	[Header("Demo Mode")]
	[SerializeField]
	private bool isDemoMode;

	private bool isPaused;

	private int selectedIndex;

	private float lastNavigateTime;

	private Vector3[] originalScales;

	private bool skipCancelThisFrame;

	private void Awake()
	{
		isDemoMode = true;
		originalScales = new Vector3[buttons.Length];
		for (int i = 0; i < buttons.Length; i++)
		{
			if (buttons[i] != null)
			{
				originalScales[i] = buttons[i].transform.localScale;
			}
		}
		if (pauseCanvas != null)
		{
			pauseCanvas.SetActive(value: false);
		}
	}

	private void OnEnable()
	{
		if (inputReader != null)
		{
			inputReader.OnPausePressed += OnPausePerformed;
		}
	}

	private void OnDisable()
	{
		if (inputReader != null)
		{
			inputReader.OnPausePressed -= OnPausePerformed;
		}
	}

	private void Update()
	{
		if (isPaused)
		{
			HandleNavigation();
			HandleSubmit();
			HandleCancel();
			UpdateButtonVisuals();
		}
	}

	private void OnPausePerformed()
	{
		if (isPaused)
		{
			Unpause();
		}
		else
		{
			Pause();
		}
	}

	public void Pause()
	{
		if (!isPaused)
		{
			SFXManager.Instance?.Play2D("UI_Pause");
			MusicManager.Instance?.SetMuffled(muffled: true);
			isPaused = true;
			skipCancelThisFrame = true;
			Time.timeScale = 0f;
			inputReader.DisableInput();
			if (pauseCanvas != null)
			{
				pauseCanvas.SetActive(value: true);
			}
			selectedIndex = 0;
			lastNavigateTime = -0.15f;
		}
	}

	public void Unpause()
	{
		if (isPaused)
		{
			SFXManager.Instance?.Play2D("UI_Unpause");
			MusicManager.Instance?.SetMuffled(muffled: false);
			isPaused = false;
			Time.timeScale = 1f;
			for (int i = 0; i < buttons.Length; i++)
			{
				ApplyVisual(i, isSelected: false, instant: true);
			}
			if (pauseCanvas != null)
			{
				pauseCanvas.SetActive(value: false);
			}
			inputReader.EnableInput();
			inputReader.ConsumeJumpBuffer();
		}
	}

	public void OnPlayPressed()
	{
		Unpause();
	}

	public void OnRestartPressed()
	{
		if (!(ScreenFader.Instance != null) || !ScreenFader.Instance.IsFading)
		{
			MusicManager.Instance?.SetMuffled(muffled: false);
			Time.timeScale = 1f;
			ScreenFader.Instance.LoadScene(SceneManager.GetActiveScene().name);
		}
	}

	public void OnLevelSelectPressed()
	{
		if (!(ScreenFader.Instance != null) || !ScreenFader.Instance.IsFading)
		{
			MusicManager.Instance?.SetMuffled(muffled: false);
			Time.timeScale = 1f;
			string sceneName = (isDemoMode ? "MainMenu" : "LevelSelect");
			ScreenFader.Instance.LoadScene(sceneName);
		}
	}

	public void OnExitPressed()
	{
		Time.timeScale = 1f;
		ScreenFader.Instance.LoadScene("MainMenu");
	}

	private void HandleNavigation()
	{
		if (Time.unscaledTime - lastNavigateTime < 0.15f)
		{
			return;
		}
		float num = 0f;
		if (Gamepad.current != null)
		{
			num = Gamepad.current.leftStick.ReadValue().x;
			if (Mathf.Abs(num) < 0.3f)
			{
				num = Gamepad.current.dpad.ReadValue().x;
			}
		}
		if (Mathf.Abs(num) < 0.3f && Keyboard.current != null)
		{
			if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)
			{
				num = 1f;
			}
			if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed)
			{
				num = -1f;
			}
		}
		if (!(Mathf.Abs(num) < 0.3f))
		{
			int num2 = ((num > 0f) ? 1 : (-1));
			selectedIndex = (selectedIndex + num2 + buttons.Length) % buttons.Length;
			SFXManager.Instance?.Play2D("UI_Navigate");
			lastNavigateTime = Time.unscaledTime;
		}
	}

	private void HandleSubmit()
	{
		if (!(ScreenFader.Instance != null) || !ScreenFader.Instance.IsFading)
		{
			bool flag = false;
			if (Gamepad.current != null)
			{
				flag = Gamepad.current.buttonSouth.wasPressedThisFrame;
			}
			if (Keyboard.current != null)
			{
				flag |= Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame;
			}
			if (flag && buttons.Length != 0)
			{
				buttons[selectedIndex]?.onClick.Invoke();
			}
		}
	}

	private void HandleCancel()
	{
		if (skipCancelThisFrame)
		{
			skipCancelThisFrame = false;
			return;
		}
		bool flag = false;
		if (Gamepad.current != null)
		{
			flag = Gamepad.current.buttonEast.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame;
		}
		if (Keyboard.current != null)
		{
			flag |= Keyboard.current.escapeKey.wasPressedThisFrame;
		}
		if (flag)
		{
			Unpause();
		}
	}

	private void UpdateButtonVisuals()
	{
		for (int i = 0; i < buttons.Length; i++)
		{
			ApplyVisual(i, i == selectedIndex, instant: false);
		}
	}

	private void ApplyVisual(int index, bool isSelected, bool instant)
	{
		if (index >= 0 && index < buttons.Length && !(buttons[index] == null))
		{
			Button button = buttons[index];
			Color color = (isSelected ? selectedColor : normalColor);
			Vector3 vector = (isSelected ? (originalScales[index] * selectedScale) : originalScales[index]);
			Image component = button.GetComponent<Image>();
			if (component != null)
			{
				component.color = (instant ? color : Color.Lerp(component.color, color, Time.unscaledDeltaTime * transitionSpeed));
			}
			button.transform.localScale = (instant ? vector : Vector3.Lerp(button.transform.localScale, vector, Time.unscaledDeltaTime * transitionSpeed));
		}
	}
}
