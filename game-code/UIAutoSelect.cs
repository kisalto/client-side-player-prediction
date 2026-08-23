using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

public class UIAutoSelect : MonoBehaviour
{
	[Header("Selection Settings")]
	[SerializeField]
	private Selectable firstSelectedElement;

	[SerializeField]
	private bool selectOnEnable = true;

	[SerializeField]
	private float delayBeforeSelect;

	[Header("Re-selection Behavior")]
	[SerializeField]
	private bool reselectOnInputLost = true;

	[SerializeField]
	private bool reselectOnAnyInput;

	[Header("Canvas Group Integration")]
	[SerializeField]
	private CanvasGroup targetCanvasGroup;

	[SerializeField]
	private bool waitForCanvasGroupFade;

	[SerializeField]
	private float canvasGroupThreshold = 0.9f;

	[Header("Debug")]
	[SerializeField]
	private bool showDebugLogs;

	private EventSystem eventSystem;

	private Coroutine autoSelectCoroutine;

	private bool hasSelectedOnce;

	private bool anyInputThisFrame;

	private void Awake()
	{
		eventSystem = EventSystem.current;
		if (eventSystem == null)
		{
			Debug.LogError("[UIAutoSelect] No EventSystem found in scene! UI navigation requires an EventSystem.");
		}
		if (targetCanvasGroup == null)
		{
			targetCanvasGroup = GetComponent<CanvasGroup>();
		}
	}

	private void OnEnable()
	{
		hasSelectedOnce = false;
		if (selectOnEnable && firstSelectedElement != null)
		{
			if (autoSelectCoroutine != null)
			{
				StopCoroutine(autoSelectCoroutine);
			}
			autoSelectCoroutine = StartCoroutine(SelectElementAfterDelay());
		}
		InputSystem.onAnyButtonPress.CallOnce(delegate
		{
			anyInputThisFrame = true;
		});
	}

	private void OnDisable()
	{
		if (autoSelectCoroutine != null)
		{
			StopCoroutine(autoSelectCoroutine);
			autoSelectCoroutine = null;
		}
		if (eventSystem != null)
		{
			eventSystem.SetSelectedGameObject(null);
		}
		hasSelectedOnce = false;
	}

	private IEnumerator SelectElementAfterDelay()
	{
		if (delayBeforeSelect > 0f)
		{
			float elapsed = 0f;
			while (elapsed < delayBeforeSelect)
			{
				elapsed += Time.unscaledDeltaTime;
				yield return null;
			}
		}
		if (waitForCanvasGroupFade && targetCanvasGroup != null)
		{
			while (targetCanvasGroup.alpha < canvasGroupThreshold)
			{
				yield return null;
			}
		}
		SelectFirstElement();
		autoSelectCoroutine = null;
	}

	public void SelectFirstElement()
	{
		if (firstSelectedElement == null)
		{
			if (showDebugLogs)
			{
				Debug.LogWarning("[UIAutoSelect] First selected element is null!");
			}
			return;
		}
		if (eventSystem == null)
		{
			if (showDebugLogs)
			{
				Debug.LogWarning("[UIAutoSelect] EventSystem is null!");
			}
			return;
		}
		if (!firstSelectedElement.IsInteractable())
		{
			if (showDebugLogs)
			{
				Debug.LogWarning("[UIAutoSelect] " + firstSelectedElement.name + " is not interactable!");
			}
			return;
		}
		eventSystem.SetSelectedGameObject(null);
		firstSelectedElement.Select();
		hasSelectedOnce = true;
		if (showDebugLogs)
		{
			Debug.Log("[UIAutoSelect] Selected " + firstSelectedElement.name);
		}
	}

	private void Update()
	{
		if (!reselectOnInputLost || !hasSelectedOnce || eventSystem == null)
		{
			return;
		}
		bool flag = CheckForAnyInput();
		if (eventSystem.currentSelectedGameObject == null)
		{
			if (reselectOnAnyInput & flag)
			{
				SelectFirstElement();
			}
			else if (!reselectOnAnyInput)
			{
				SelectFirstElement();
			}
		}
		anyInputThisFrame = false;
	}

	private bool CheckForAnyInput()
	{
		if (anyInputThisFrame)
		{
			return true;
		}
		if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
		{
			return true;
		}
		if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame || Mouse.current.middleButton.wasPressedThisFrame))
		{
			return true;
		}
		if (Gamepad.current != null)
		{
			foreach (InputControl allControl in Gamepad.current.allControls)
			{
				if (allControl is ButtonControl { wasPressedThisFrame: not false })
				{
					return true;
				}
			}
		}
		return false;
	}

	public void SetFirstSelectedElement(Selectable newElement)
	{
		firstSelectedElement = newElement;
	}

	public void SelectElementImmediate()
	{
		if (autoSelectCoroutine != null)
		{
			StopCoroutine(autoSelectCoroutine);
			autoSelectCoroutine = null;
		}
		SelectFirstElement();
	}
}
