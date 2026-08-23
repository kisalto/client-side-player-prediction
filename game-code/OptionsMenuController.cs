using UnityEngine;
using UnityEngine.InputSystem;

public class OptionsMenuController : MonoBehaviour
{
	private const string MainMenuScene = "MainMenu";

	private const string CutsceneScene = "SCENE_Cutscene";

	public const string ReturnToOptionsKey = "ReturnToOptions";

	[Header("Scene References")]
	[SerializeField]
	private MainMenuController mainMenuController;

	[SerializeField]
	private BackButton backButton;

	[Header("Rewatch Button")]
	[Tooltip("Assign the RewatchCutsceneButton in the Inspector.")]
	[SerializeField]
	private RewatchCutsceneButton rewatchButton;

	[Header("Navigation Settings")]
	[SerializeField]
	private float navigationCooldown = 0.2f;

	private bool isActive;

	private float lastNavigateTime;

	private int selectedIndex = -1;

	public static bool IsOpen { get; private set; }

	private void Update()
	{
		if (isActive)
		{
			HandleNavigationInput();
			HandleSubmitInput();
			HandleCancelInput();
		}
	}

	public void PrepareVisibility()
	{
		if (rewatchButton == null)
		{
			return;
		}
		bool active = false;
		if (SaveSlotManager.Instance != null)
		{
			SaveSlotInfo[] allSlotInfo = SaveSlotManager.Instance.GetAllSlotInfo();
			for (int i = 0; i < allSlotInfo.Length; i++)
			{
				if (!allSlotInfo[i].isEmpty)
				{
					active = true;
					break;
				}
			}
		}
		rewatchButton.gameObject.SetActive(active);
	}

	public void Activate()
	{
		isActive = true;
		IsOpen = true;
		if (rewatchButton != null && rewatchButton.gameObject.activeSelf)
		{
			SelectRewatch();
		}
		else
		{
			SelectBack();
		}
	}

	public void Deactivate()
	{
		isActive = false;
		IsOpen = false;
		backButton?.SetSelected(selected: false);
		rewatchButton?.SetSelected(selected: false);
	}

	public void SelectBackButton()
	{
		SelectBack();
	}

	public void SelectRewatchButton()
	{
		SelectRewatch();
	}

	public void OnRewatchPressed()
	{
		if (isActive)
		{
			PlayerPrefs.SetInt("ReturnToOptions", 1);
			PlayerPrefs.Save();
			Deactivate();
			CutscenePlayer.RewatchThenReturn("SCENE_Cutscene", "MainMenu");
		}
	}

	private void HandleNavigationInput()
	{
		if (rewatchButton == null || !rewatchButton.gameObject.activeSelf || Time.time - lastNavigateTime < navigationCooldown)
		{
			return;
		}
		int num = 0;
		if (Gamepad.current != null)
		{
			Vector2 vector = Gamepad.current.leftStick.ReadValue();
			Vector2 vector2 = Gamepad.current.dpad.ReadValue();
			Vector2 obj = ((vector.magnitude > vector2.magnitude) ? vector : vector2);
			if (obj.x > 0.3f)
			{
				num = 1;
			}
			if (obj.x < -0.3f)
			{
				num = -1;
			}
		}
		if (Keyboard.current != null)
		{
			if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
			{
				num = 1;
			}
			if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
			{
				num = -1;
			}
		}
		switch (num)
		{
		case 0:
			return;
		case 1:
			if (selectedIndex == 0)
			{
				SelectBack();
				lastNavigateTime = Time.time;
				return;
			}
			break;
		}
		if (num == -1 && selectedIndex == -1)
		{
			SelectRewatch();
			lastNavigateTime = Time.time;
		}
	}

	private void HandleSubmitInput()
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
		if (flag)
		{
			if (selectedIndex == 0)
			{
				OnRewatchPressed();
			}
			else
			{
				PressBack();
			}
		}
	}

	private void HandleCancelInput()
	{
		bool flag = false;
		if (Gamepad.current != null)
		{
			flag = Gamepad.current.buttonEast.wasPressedThisFrame;
		}
		if (Keyboard.current != null)
		{
			flag |= Keyboard.current.escapeKey.wasPressedThisFrame;
		}
		if (flag)
		{
			PressBack();
		}
	}

	private void PressBack()
	{
		if (!(ScreenFader.Instance != null) || !ScreenFader.Instance.IsFading)
		{
			SFXManager.Instance?.Play2D("UI_ButtonPress");
			mainMenuController?.OnBackToMainMenu();
			Deactivate();
		}
	}

	private void SelectBack()
	{
		int num = selectedIndex;
		selectedIndex = -1;
		backButton?.SetSelected(selected: true);
		rewatchButton?.SetSelected(selected: false);
		if (num != selectedIndex)
		{
			SFXManager.Instance?.Play2D("UI_ButtonHover");
		}
	}

	private void SelectRewatch()
	{
		int num = selectedIndex;
		selectedIndex = 0;
		rewatchButton?.SetSelected(selected: true);
		backButton?.SetSelected(selected: false);
		if (num != selectedIndex)
		{
			SFXManager.Instance?.Play2D("UI_ButtonHover");
		}
	}
}
