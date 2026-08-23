using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SaveSlotMenuController : MonoBehaviour
{
	[Header("Scene References")]
	[SerializeField]
	private MainMenuController mainMenuController;

	[Header("Back Button")]
	[SerializeField]
	private BackButton backButton;

	[Header("Navigation Settings")]
	[SerializeField]
	private float navigationCooldown = 0.2f;

	[SerializeField]
	private float inputDeadzone = 0.3f;

	[Header("Scene Names")]
	[SerializeField]
	private string newGameSceneName = "SCENE_Ch1_Lv1";

	[SerializeField]
	private string levelSelectSceneName = "Level Select";

	[SerializeField]
	private string cutsceneSceneName = "SCENE_Cutscene";

	private readonly List<SaveSlotButton> slotButtons = new List<SaveSlotButton>();

	private int selectedIndex;

	private int lastSelectedSlotIndex;

	private float lastNavigateTime;

	private bool isActive;

	private bool inputConsumed;

	private const int BackButtonIndex = -1;

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

	private void OnDestroy()
	{
		IsOpen = false;
	}

	public void RegisterSlotButton(SaveSlotButton button)
	{
		if (!slotButtons.Contains(button))
		{
			slotButtons.Add(button);
		}
		slotButtons.Sort((SaveSlotButton a, SaveSlotButton b) => a.transform.position.x.CompareTo(b.transform.position.x));
	}

	public void UnregisterSlotButton(SaveSlotButton button)
	{
		slotButtons.Remove(button);
	}

	public void SetSelectedButton(SaveSlotButton button)
	{
		int num = slotButtons.IndexOf(button);
		if (num >= 0)
		{
			Select(num);
		}
	}

	public void SelectBackButton()
	{
		Select(-1);
	}

	public void Activate()
	{
		isActive = true;
		IsOpen = true;
		RefreshAll();
		Select(0);
	}

	public void Deactivate()
	{
		isActive = false;
		IsOpen = false;
		DeselectAll();
	}

	public void OnSlotPressed(int slotIndex, bool isEmpty)
	{
		if (!inputConsumed)
		{
			inputConsumed = true;
			isActive = false;
			IsOpen = false;
			if (isEmpty)
			{
				CreateAndLoadNewGame(slotIndex);
			}
			else
			{
				LoadExistingGame(slotIndex);
			}
		}
	}

	private void CreateAndLoadNewGame(int slotIndex)
	{
		string slotName = $"Save {slotIndex + 1}";
		SaveSlotManager.Instance.CreateNewSlot(slotIndex, slotName);
		SaveSlotManager.Instance.LoadSlot(slotIndex);
		MusicManager.Instance?.FadeOutThenSuppressNext();
		CutscenePlayer.PlayThenLoad(cutsceneSceneName, newGameSceneName);
	}

	private void LoadExistingGame(int slotIndex)
	{
		SaveSlotManager.Instance.LoadSlot(slotIndex);
		ScreenFader.Instance.LoadScene(levelSelectSceneName);
	}

	private void HandleNavigationInput()
	{
		if (Time.time - lastNavigateTime < navigationCooldown)
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		if (Gamepad.current != null)
		{
			Vector2 vector = Gamepad.current.leftStick.ReadValue();
			Vector2 vector2 = Gamepad.current.dpad.ReadValue();
			Vector2 obj = ((vector.magnitude > vector2.magnitude) ? vector : vector2);
			if (obj.x > inputDeadzone)
			{
				num = 1;
			}
			if (obj.x < 0f - inputDeadzone)
			{
				num = -1;
			}
			if (obj.y < 0f - inputDeadzone)
			{
				num2 = -1;
			}
			if (obj.y > inputDeadzone)
			{
				num2 = 1;
			}
		}
		if (Keyboard.current != null)
		{
			if (num == 0)
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
			if (num2 == 0)
			{
				if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
				{
					num2 = -1;
				}
				if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
				{
					num2 = 1;
				}
			}
		}
		if (num == 0 && num2 == 0)
		{
			return;
		}
		if (selectedIndex == -1)
		{
			if (num2 == 1 && slotButtons.Count > 0)
			{
				Select(Mathf.Clamp(lastSelectedSlotIndex, 0, slotButtons.Count - 1));
				lastNavigateTime = Time.time;
			}
			return;
		}
		if (num != 0)
		{
			int index = Mathf.Clamp(selectedIndex + num, 0, slotButtons.Count - 1);
			Select(index);
			lastNavigateTime = Time.time;
		}
		if (num2 == -1 && backButton != null)
		{
			lastSelectedSlotIndex = selectedIndex;
			Select(-1);
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
			if (selectedIndex == -1)
			{
				backButton?.PressBack();
			}
			else if (selectedIndex >= 0 && selectedIndex < slotButtons.Count)
			{
				slotButtons[selectedIndex].PressButton();
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
			Deactivate();
			mainMenuController?.OnBackToMainMenu();
		}
	}

	private void Select(int index)
	{
		int num = selectedIndex;
		for (int i = 0; i < slotButtons.Count; i++)
		{
			slotButtons[i].SetSelected(i == index);
		}
		backButton?.SetSelected(index == -1);
		selectedIndex = index;
		if (selectedIndex != num)
		{
			SFXManager.Instance?.Play2D("UI_ButtonHover");
		}
	}

	private void DeselectAll()
	{
		foreach (SaveSlotButton slotButton in slotButtons)
		{
			slotButton.SetSelected(selected: false);
		}
		backButton?.SetSelected(selected: false);
	}

	private void RefreshAll()
	{
		foreach (SaveSlotButton slotButton in slotButtons)
		{
			slotButton.RefreshDisplay();
		}
	}
}
