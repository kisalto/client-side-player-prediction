using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputDeviceTutorialToggle : MonoBehaviour
{
	[Header("Tutorial Boards")]
	[SerializeField]
	private GameObject controllerBoard;

	[SerializeField]
	private GameObject keyboardMouseBoard;

	private PlayerInput playerInput;

	private void OnEnable()
	{
		InputSystem.onActionChange += OnActionChange;
		InputSystem.onDeviceChange += OnDeviceChange;
	}

	private void OnDisable()
	{
		InputSystem.onActionChange -= OnActionChange;
		InputSystem.onDeviceChange -= OnDeviceChange;
		if (playerInput != null)
		{
			playerInput.onControlsChanged -= OnControlsChanged;
		}
	}

	private void Start()
	{
		TrySubscribeToPlayerInput();
		Refresh();
	}

	private void TrySubscribeToPlayerInput()
	{
		if (!(playerInput != null))
		{
			playerInput = UnityEngine.Object.FindAnyObjectByType<PlayerInput>();
			if (playerInput != null)
			{
				playerInput.onControlsChanged += OnControlsChanged;
			}
		}
	}

	private void OnControlsChanged(PlayerInput input)
	{
		Refresh();
	}

	private void OnDeviceChange(InputDevice device, InputDeviceChange change)
	{
		if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed || change == InputDeviceChange.Enabled || change == InputDeviceChange.Disabled)
		{
			Refresh();
		}
	}

	private void OnActionChange(object obj, InputActionChange change)
	{
		if (change == InputActionChange.ActionPerformed)
		{
			TrySubscribeToPlayerInput();
			Refresh();
		}
	}

	private void Refresh()
	{
		bool flag = IsControllerActive();
		if (controllerBoard != null)
		{
			controllerBoard.SetActive(flag);
		}
		if (keyboardMouseBoard != null)
		{
			keyboardMouseBoard.SetActive(!flag);
		}
	}

	private bool IsControllerActive()
	{
		if (playerInput != null)
		{
			string currentControlScheme = playerInput.currentControlScheme;
			if (!string.IsNullOrEmpty(currentControlScheme))
			{
				return !currentControlScheme.Equals("Keyboard&Mouse", StringComparison.OrdinalIgnoreCase);
			}
		}
		InputDevice inputDevice = ((InputSystem.devices.Count > 0) ? GetLastUsedDevice() : null);
		if (inputDevice != null)
		{
			return inputDevice is Gamepad;
		}
		if (Gamepad.current != null)
		{
			return Keyboard.current == null;
		}
		return false;
	}

	private InputDevice GetLastUsedDevice()
	{
		double num = double.MinValue;
		InputDevice result = null;
		foreach (InputDevice device in InputSystem.devices)
		{
			if (device.lastUpdateTime > num)
			{
				num = device.lastUpdateTime;
				result = device;
			}
		}
		return result;
	}
}
