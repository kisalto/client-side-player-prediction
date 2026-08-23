using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MenuNavigationController : MonoBehaviour
{
	public enum NavigationMode
	{
		Horizontal,
		Vertical,
		Grid
	}

	[Header("Navigation Settings")]
	[SerializeField]
	private List<MenuButton> buttons = new List<MenuButton>();

	[SerializeField]
	private int defaultSelectedIndex;

	[SerializeField]
	private NavigationMode navigationMode;

	[Header("Input Settings")]
	[SerializeField]
	private float navigationCooldown = 0.2f;

	[SerializeField]
	private float mouseMovementThreshold = 0.1f;

	[SerializeField]
	private float inputDeadzone = 0.3f;

	[Header("Dependencies")]
	[SerializeField]
	private MainMenuController mainMenuController;

	private int currentSelectedIndex;

	private float lastNavigationTime;

	private Vector2 lastMousePosition;

	private bool isUsingMouse;

	private Vector2 lastGamepadInput = Vector2.zero;

	private bool wasInputBlocked;

	private bool IsInputBlocked
	{
		get
		{
			if (!SaveSlotMenuController.IsOpen && !OptionsMenuController.IsOpen)
			{
				if (mainMenuController != null)
				{
					return mainMenuController.IsMoving();
				}
				return false;
			}
			return true;
		}
	}

	public event Action<MenuButton> OnSelectionChanged;

	private void Start()
	{
		if (Mouse.current != null)
		{
			lastMousePosition = Mouse.current.position.ReadValue();
		}
		if (buttons.Count == 0)
		{
			FindAllButtons();
		}
		SortButtonsByPosition();
		currentSelectedIndex = Mathf.Clamp(defaultSelectedIndex, 0, buttons.Count - 1);
		if (buttons.Count > 0)
		{
			SelectButton(currentSelectedIndex);
		}
	}

	private void Update()
	{
		if (IsInputBlocked)
		{
			DeselectAllButtons();
			wasInputBlocked = true;
			return;
		}
		if (wasInputBlocked)
		{
			wasInputBlocked = false;
			isUsingMouse = false;
			if (buttons.Count > 0)
			{
				currentSelectedIndex = Mathf.Clamp(defaultSelectedIndex, 0, buttons.Count - 1);
				SelectButton(currentSelectedIndex);
			}
		}
		CheckInputMode();
		if (!isUsingMouse)
		{
			HandleGamepadInput();
			HandleKeyboardInput();
		}
		HandleSubmitInput();
	}

	private void CheckInputMode()
	{
		if (Mouse.current != null)
		{
			Vector2 a = Mouse.current.position.ReadValue();
			if (Vector2.Distance(a, lastMousePosition) > mouseMovementThreshold && !isUsingMouse)
			{
				isUsingMouse = true;
				DeselectAllButtons();
			}
			lastMousePosition = a;
		}
		bool flag = false;
		if (Gamepad.current != null)
		{
			Vector2 vector = Gamepad.current.leftStick.ReadValue();
			Vector2 vector2 = Gamepad.current.dpad.ReadValue();
			if (vector.magnitude > inputDeadzone || vector2.magnitude > inputDeadzone)
			{
				flag = true;
			}
			if (Gamepad.current.buttonSouth.wasPressedThisFrame)
			{
				flag = true;
			}
		}
		if (Keyboard.current != null && (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
		{
			flag = true;
		}
		if (flag && isUsingMouse)
		{
			isUsingMouse = false;
			SelectButton(currentSelectedIndex);
		}
	}

	private void HandleGamepadInput()
	{
		if (Gamepad.current == null || Time.time - lastNavigationTime < navigationCooldown)
		{
			return;
		}
		Vector2 vector = Gamepad.current.leftStick.ReadValue();
		Vector2 vector2 = Gamepad.current.dpad.ReadValue();
		Vector2 vector3 = ((vector.magnitude > vector2.magnitude) ? vector : vector2);
		if (vector3.magnitude > inputDeadzone && lastGamepadInput.magnitude <= inputDeadzone)
		{
			int num = currentSelectedIndex;
			switch (navigationMode)
			{
			case NavigationMode.Horizontal:
				if (vector3.x > inputDeadzone)
				{
					currentSelectedIndex++;
				}
				else if (vector3.x < 0f - inputDeadzone)
				{
					currentSelectedIndex--;
				}
				break;
			case NavigationMode.Vertical:
				if (vector3.y > inputDeadzone)
				{
					currentSelectedIndex--;
				}
				else if (vector3.y < 0f - inputDeadzone)
				{
					currentSelectedIndex++;
				}
				break;
			case NavigationMode.Grid:
				if (Mathf.Abs(vector3.x) > Mathf.Abs(vector3.y))
				{
					if (vector3.x > inputDeadzone)
					{
						currentSelectedIndex++;
					}
					else if (vector3.x < 0f - inputDeadzone)
					{
						currentSelectedIndex--;
					}
				}
				else if (vector3.y > inputDeadzone)
				{
					currentSelectedIndex--;
				}
				else if (vector3.y < 0f - inputDeadzone)
				{
					currentSelectedIndex++;
				}
				break;
			}
			currentSelectedIndex = (currentSelectedIndex + buttons.Count) % buttons.Count;
			if (currentSelectedIndex != num)
			{
				SelectButton(currentSelectedIndex);
				lastNavigationTime = Time.time;
			}
		}
		lastGamepadInput = vector3;
	}

	private void HandleKeyboardInput()
	{
		if (Keyboard.current == null || Time.time - lastNavigationTime < navigationCooldown)
		{
			return;
		}
		int num = currentSelectedIndex;
		bool flag = false;
		switch (navigationMode)
		{
		case NavigationMode.Horizontal:
			if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
			{
				currentSelectedIndex++;
				flag = true;
			}
			else if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
			{
				currentSelectedIndex--;
				flag = true;
			}
			break;
		case NavigationMode.Vertical:
			if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
			{
				currentSelectedIndex--;
				flag = true;
			}
			else if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
			{
				currentSelectedIndex++;
				flag = true;
			}
			break;
		case NavigationMode.Grid:
			if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
			{
				currentSelectedIndex++;
				flag = true;
			}
			else if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
			{
				currentSelectedIndex--;
				flag = true;
			}
			else if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
			{
				currentSelectedIndex--;
				flag = true;
			}
			else if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
			{
				currentSelectedIndex++;
				flag = true;
			}
			break;
		}
		if (flag)
		{
			currentSelectedIndex = (currentSelectedIndex + buttons.Count) % buttons.Count;
			if (currentSelectedIndex != num)
			{
				SelectButton(currentSelectedIndex);
				lastNavigationTime = Time.time;
			}
		}
	}

	private void HandleSubmitInput()
	{
		if (!isUsingMouse)
		{
			bool num = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
			bool flag = Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame);
			if ((num | flag) && currentSelectedIndex >= 0 && currentSelectedIndex < buttons.Count)
			{
				buttons[currentSelectedIndex].PressButton();
			}
		}
	}

	private void SelectButton(int index)
	{
		if (index >= 0 && index < buttons.Count)
		{
			for (int i = 0; i < buttons.Count; i++)
			{
				buttons[i].SetSelected(i == index);
			}
			currentSelectedIndex = index;
			OnSelectionChanged?.Invoke(buttons[currentSelectedIndex]);
		}
	}

	private void DeselectAllButtons()
	{
		foreach (MenuButton button in buttons)
		{
			button.SetSelected(selected: false);
		}
	}

	private void FindAllButtons()
	{
		buttons.Clear();
		MenuButton[] array = UnityEngine.Object.FindObjectsOfType<MenuButton>();
		foreach (MenuButton item in array)
		{
			buttons.Add(item);
		}
	}

	private void SortButtonsByPosition()
	{
		switch (navigationMode)
		{
		case NavigationMode.Horizontal:
			buttons.Sort((MenuButton a, MenuButton b) => a.transform.position.x.CompareTo(b.transform.position.x));
			break;
		case NavigationMode.Vertical:
			buttons.Sort((MenuButton a, MenuButton b) => b.transform.position.y.CompareTo(a.transform.position.y));
			break;
		case NavigationMode.Grid:
			buttons.Sort(delegate(MenuButton a, MenuButton b)
			{
				float num = b.transform.position.y - a.transform.position.y;
				if (Mathf.Abs(num) < 1f)
				{
					return a.transform.position.x.CompareTo(b.transform.position.x);
				}
				return (num > 0f) ? 1 : (-1);
			});
			break;
		}
	}

	public void RegisterButton(MenuButton button)
	{
		if (!buttons.Contains(button))
		{
			buttons.Add(button);
			SortButtonsByPosition();
		}
	}

	public void UnregisterButton(MenuButton button)
	{
		buttons.Remove(button);
	}

	public void SetSelectedButton(MenuButton button)
	{
		int num = buttons.IndexOf(button);
		if (num >= 0)
		{
			isUsingMouse = true;
			SelectButton(num);
		}
	}
}
