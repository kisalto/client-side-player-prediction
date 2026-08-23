using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LevelCompleteNavigationController : MonoBehaviour
{
	[Header("Navigation Settings")]
	[SerializeField]
	private float inputCooldown = 0.2f;

	[SerializeField]
	private float diagonalDeadzone = 0.5f;

	[SerializeField]
	private float cardinalDeadzone = 0.3f;

	[SerializeField]
	private int defaultIndex;

	private readonly List<LevelCompleteMenuButton> buttons = new List<LevelCompleteMenuButton>();

	private int selectedIndex;

	private float lastNavigateTime;

	private bool isInitialized;

	private bool inputEnabled;

	private bool inputConsumed;

	private void OnEnable()
	{
		if (isInitialized && buttons.Count > 0)
		{
			Select(Mathf.Clamp(defaultIndex, 0, buttons.Count - 1));
		}
	}

	private void Update()
	{
		if (inputEnabled && !inputConsumed && buttons.Count != 0)
		{
			HandleNavigationInput();
			HandleSubmitInput();
		}
	}

	public void EnableInput()
	{
		inputEnabled = true;
	}

	public void RegisterButton(LevelCompleteMenuButton button)
	{
		if (!buttons.Contains(button))
		{
			buttons.Add(button);
		}
		buttons.Sort((LevelCompleteMenuButton a, LevelCompleteMenuButton b) => a.NavigationOrder.CompareTo(b.NavigationOrder));
		if (!isInitialized && buttons.Count > 0)
		{
			selectedIndex = Mathf.Clamp(defaultIndex, 0, buttons.Count - 1);
			Select(selectedIndex);
			isInitialized = true;
		}
	}

	public void UnregisterButton(LevelCompleteMenuButton button)
	{
		buttons.Remove(button);
	}

	public void SelectButton(LevelCompleteMenuButton button)
	{
		int num = buttons.IndexOf(button);
		if (num >= 0)
		{
			Select(num);
		}
	}

	private void HandleNavigationInput()
	{
		if (Time.unscaledTime - lastNavigateTime < inputCooldown)
		{
			return;
		}
		NavigationDirection? navigationDirection = ReadDirection();
		if (navigationDirection.HasValue)
		{
			LevelCompleteMenuButton neighbour = buttons[selectedIndex].GetNeighbour(navigationDirection.Value);
			if (!(neighbour == null))
			{
				SelectButton(neighbour);
				lastNavigateTime = Time.unscaledTime;
			}
		}
	}

	private NavigationDirection? ReadDirection()
	{
		Vector2 vector = Vector2.zero;
		if (Gamepad.current != null)
		{
			Vector2 vector2 = Gamepad.current.leftStick.ReadValue();
			Vector2 vector3 = Gamepad.current.dpad.ReadValue();
			vector = ((vector2.magnitude > vector3.magnitude) ? vector2 : vector3);
		}
		if (vector.magnitude < cardinalDeadzone && Keyboard.current != null)
		{
			float num = 0f;
			float num2 = 0f;
			if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
			{
				num++;
			}
			if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
			{
				num--;
			}
			if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
			{
				num2++;
			}
			if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
			{
				num2--;
			}
			vector = new Vector2(num, num2);
		}
		if (vector.magnitude < cardinalDeadzone)
		{
			return null;
		}
		bool num3 = Mathf.Abs(vector.x) >= diagonalDeadzone;
		bool flag = Mathf.Abs(vector.y) >= diagonalDeadzone;
		if (num3 & flag)
		{
			if (vector.x > 0f && vector.y > 0f)
			{
				return NavigationDirection.UpRight;
			}
			if (vector.x < 0f && vector.y > 0f)
			{
				return NavigationDirection.UpLeft;
			}
			if (vector.x > 0f && vector.y < 0f)
			{
				return NavigationDirection.DownRight;
			}
			if (vector.x < 0f && vector.y < 0f)
			{
				return NavigationDirection.DownLeft;
			}
		}
		if (Mathf.Abs(vector.x) > Mathf.Abs(vector.y))
		{
			return (vector.x > 0f) ? NavigationDirection.Right : NavigationDirection.Left;
		}
		return (!(vector.y > 0f)) ? NavigationDirection.Down : NavigationDirection.Up;
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
		if (flag && selectedIndex >= 0 && selectedIndex < buttons.Count)
		{
			inputConsumed = true;
			buttons[selectedIndex].Press();
		}
	}

	private void Select(int index)
	{
		int num = selectedIndex;
		for (int i = 0; i < buttons.Count; i++)
		{
			buttons[i].SetActive(i == index);
		}
		selectedIndex = index;
		if (selectedIndex != num)
		{
			SFXManager.Instance?.Play2D("UI_ButtonHover");
		}
	}
}
