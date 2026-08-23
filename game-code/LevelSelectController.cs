using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LevelSelectController : MonoBehaviour
{
	private const float CardinalDeadzone = 0.3f;

	private const float DiagonalDeadzone = 0.5f;

	private const float ArrivalThreshold = 0.02f;

	private static readonly int AnimatorState = Animator.StringToHash("State");

	private const int StateIdle = 0;

	private const int StateWalk = 1;

	private static readonly LevelSelectDirection[] AllDirections = (LevelSelectDirection[])Enum.GetValues(typeof(LevelSelectDirection));

	public static int JustCompletedLevelIndex = -1;

	[Header("Starting Point")]
	[Tooltip("The level node the player marker starts on when arriving from the main menu.")]
	[SerializeField]
	private LevelPointButton startingPoint;

	[Header("Player Marker")]
	[SerializeField]
	private Transform playerMarker;

	[SerializeField]
	private float markerMoveSpeed = 3f;

	[SerializeField]
	private float markerYOffset = 0.9f;

	[Header("Input Settings")]
	[SerializeField]
	private float inputCooldown = 0.2f;

	[Header("All Level Nodes")]
	[Tooltip("Drag every LevelPointButton in this chapter here in sequential order.")]
	[SerializeField]
	private LevelPointButton[] allNodes;

	private LevelPointButton currentPoint;

	private LevelPointButton lastArrivedPoint;

	private readonly Queue<LevelPointButton> waypointQueue = new Queue<LevelPointButton>();

	private Animator markerAnimator;

	private SpriteRenderer markerRenderer;

	private float lastNavigateTime;

	private bool isMoving;

	private LevelPointButton lastHoveredPoint;

	private bool isInputLocked;

	private void Start()
	{
		if (startingPoint == null)
		{
			Debug.LogError("[LEVEL SELECT] No starting point assigned on LevelSelectController.");
			return;
		}
		if (playerMarker != null)
		{
			markerAnimator = playerMarker.GetComponent<Animator>();
			markerRenderer = playerMarker.GetComponent<SpriteRenderer>();
		}
		int justCompletedLevelIndex = JustCompletedLevelIndex;
		JustCompletedLevelIndex = -1;
		Debug.Log($"[LEVEL SELECT] Start -- justCompleted={justCompletedLevelIndex}, " + $"hasSaveSlot={SaveSlotManager.Instance != null && SaveSlotManager.Instance.GetCurrentSlot() != null}");
		ApplyInstantUnlocks(justCompletedLevelIndex);
		if (justCompletedLevelIndex >= 0)
		{
			StartCoroutine(WaitForFadeInThenReveal(justCompletedLevelIndex));
		}
	}

	private void Update()
	{
		if (!(currentPoint == null))
		{
			if (!isInputLocked)
			{
				HandleNavigationInput();
				HandleSubmitInput();
				HandleMouseHover();
				HandleMouseClick();
			}
			MoveMarkerToTarget();
			UpdateAnimation();
		}
	}

	private void ApplyInstantUnlocks(int justCompleted)
	{
		bool flag = SaveSlotManager.Instance != null && SaveSlotManager.Instance.GetCurrentSlot() != null;
		object arg = flag;
		LevelPointButton[] array = allNodes;
		Debug.Log($"[LEVEL SELECT] ApplyInstantUnlocks -- hasSaveData={arg}, allNodes={((array != null) ? array.Length : 0)}");
		LevelPointButton[] array2;
		if (allNodes != null)
		{
			array2 = allNodes;
			foreach (LevelPointButton levelPointButton in array2)
			{
				if (levelPointButton == null || levelPointButton.IsUnlocked || !flag || !ShouldUnlock(levelPointButton))
				{
					continue;
				}
				if (justCompleted >= 0 && levelPointButton.Type == LevelPointButton.PointType.Level && levelPointButton.PointIndex == justCompleted + 1)
				{
					LevelSaveData levelSaveData = SaveSlotManager.Instance?.GetLevelProgress(levelPointButton.PointIndex);
					if (levelSaveData == null || !levelSaveData.isCompleted)
					{
						continue;
					}
				}
				Debug.Log($"[LEVEL SELECT] Node {levelPointButton.name} (pointIndex={levelPointButton.PointIndex}, type={levelPointButton.Type}) unlocking instantly.");
				levelPointButton.UnlockInstant();
			}
		}
		PlaceMarker(justCompleted);
		if (!flag || allNodes == null)
		{
			return;
		}
		array2 = allNodes;
		foreach (LevelPointButton levelPointButton2 in array2)
		{
			if (levelPointButton2 != null && levelPointButton2.IsUnlocked)
			{
				levelPointButton2.RefreshCoinDisplay();
			}
		}
	}

	private IEnumerator WaitForFadeInThenReveal(int justCompleted)
	{
		if (ScreenFader.Instance != null)
		{
			yield return new WaitWhile(() => ScreenFader.Instance.IsFading);
		}
		LevelPointButton levelPointButton = FindNodeToReveal(justCompleted);
		Debug.Log("[LEVEL SELECT] WaitForFadeInThenReveal -- nodeToReveal=" + ((levelPointButton != null) ? levelPointButton.name : "null"));
		if (!(levelPointButton == null))
		{
			isInputLocked = true;
			bool done = false;
			levelPointButton.OnRevealComplete += delegate
			{
				done = true;
			};
			levelPointButton.UnlockWithAnimation();
			yield return new WaitUntil(() => done);
			isInputLocked = false;
		}
	}

	private LevelPointButton FindNodeToReveal(int justCompleted)
	{
		if (allNodes == null)
		{
			return null;
		}
		LevelPointButton[] array = allNodes;
		foreach (LevelPointButton levelPointButton in array)
		{
			if (!(levelPointButton == null) && !levelPointButton.IsUnlocked && levelPointButton.Type == LevelPointButton.PointType.Level && levelPointButton.PointIndex == justCompleted + 1 && ShouldUnlock(levelPointButton))
			{
				LevelSaveData levelSaveData = SaveSlotManager.Instance?.GetLevelProgress(levelPointButton.PointIndex);
				if (levelSaveData == null || !levelSaveData.isCompleted)
				{
					return levelPointButton;
				}
			}
		}
		return null;
	}

	private bool ShouldUnlock(LevelPointButton node)
	{
		int num = node.PointIndex - 1;
		if (num < 0)
		{
			return true;
		}
		if (SaveSlotManager.Instance == null)
		{
			return false;
		}
		LevelSaveData levelProgress = SaveSlotManager.Instance.GetLevelProgress(num);
		Debug.Log($"[LEVEL SELECT] ShouldUnlock {node.name}: prerequisiteIndex={num}, " + $"found={levelProgress != null}, isCompleted={levelProgress?.isCompleted}");
		return levelProgress?.isCompleted ?? false;
	}

	private void PlaceMarker(int justCompleted)
	{
		LevelPointButton levelPointButton = startingPoint;
		if (justCompleted >= 0 && allNodes != null)
		{
			LevelPointButton[] array = allNodes;
			foreach (LevelPointButton levelPointButton2 in array)
			{
				if (levelPointButton2 != null && levelPointButton2.Type == LevelPointButton.PointType.Level && levelPointButton2.PointIndex == justCompleted)
				{
					levelPointButton = levelPointButton2;
					break;
				}
			}
		}
		else if (allNodes != null && SaveSlotManager.Instance != null && SaveSlotManager.Instance.GetCurrentSlot() != null)
		{
			int num = -1;
			LevelPointButton levelPointButton3 = null;
			LevelPointButton[] array = allNodes;
			foreach (LevelPointButton levelPointButton4 in array)
			{
				if (!(levelPointButton4 == null) && levelPointButton4.Type == LevelPointButton.PointType.Level)
				{
					LevelSaveData levelProgress = SaveSlotManager.Instance.GetLevelProgress(levelPointButton4.PointIndex);
					if (levelProgress != null && levelProgress.isCompleted && levelPointButton4.PointIndex > num)
					{
						num = levelPointButton4.PointIndex;
						levelPointButton3 = levelPointButton4;
					}
				}
			}
			if (levelPointButton3 != null)
			{
				levelPointButton = levelPointButton3;
			}
		}
		Debug.Log($"[LEVEL SELECT] PlaceMarker -- justCompleted={justCompleted}, target={levelPointButton?.name}");
		currentPoint = levelPointButton;
		lastArrivedPoint = levelPointButton;
		currentPoint.SetSelected(selected: true);
		currentPoint.SetMarkerPresent(present: true);
		if (playerMarker != null)
		{
			playerMarker.position = GetMarkerPositionFor(levelPointButton);
		}
	}

	private void HandleNavigationInput()
	{
		if (Time.time - lastNavigateTime < inputCooldown)
		{
			return;
		}
		LevelSelectDirection? levelSelectDirection = ReadDirection();
		if (levelSelectDirection.HasValue)
		{
			LevelPointButton neighbour = currentPoint.GetNeighbour(levelSelectDirection.Value);
			if (!(neighbour == null))
			{
				NavigateTo(neighbour);
				lastNavigateTime = Time.time;
			}
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
		if (flag && !isMoving && lastArrivedPoint == currentPoint)
		{
			currentPoint.PressButton();
		}
	}

	private void HandleMouseHover()
	{
		if (Mouse.current == null || (ScreenFader.Instance != null && ScreenFader.Instance.IsFading))
		{
			return;
		}
		Collider2D collider2D = Physics2D.OverlapPoint(Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()));
		LevelPointButton levelPointButton = ((collider2D != null) ? collider2D.GetComponent<LevelPointButton>() : null);
		if (!(levelPointButton == lastHoveredPoint))
		{
			lastHoveredPoint = levelPointButton;
			if (!(levelPointButton == null) && !(levelPointButton == currentPoint))
			{
				NavigateTo(levelPointButton);
			}
		}
	}

	private void HandleMouseClick()
	{
		if (Mouse.current == null || (ScreenFader.Instance != null && ScreenFader.Instance.IsFading) || !Mouse.current.leftButton.wasPressedThisFrame)
		{
			return;
		}
		Collider2D collider2D = Physics2D.OverlapPoint(Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()));
		if (!(collider2D == null))
		{
			LevelPointButton component = collider2D.GetComponent<LevelPointButton>();
			if (component != null && component == currentPoint && !isMoving && lastArrivedPoint == currentPoint)
			{
				currentPoint.PressButton();
			}
		}
	}

	private LevelSelectDirection? ReadDirection()
	{
		Vector2 vector = Vector2.zero;
		if (Gamepad.current != null)
		{
			Vector2 vector2 = Gamepad.current.leftStick.ReadValue();
			Vector2 vector3 = Gamepad.current.dpad.ReadValue();
			vector = ((vector2.magnitude > vector3.magnitude) ? vector2 : vector3);
		}
		if (vector.magnitude < 0.3f && Keyboard.current != null)
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
		if (vector.magnitude < 0.3f)
		{
			return null;
		}
		bool num3 = Mathf.Abs(vector.x) >= 0.5f;
		bool flag = Mathf.Abs(vector.y) >= 0.5f;
		if (num3 & flag)
		{
			if (vector.x > 0f && vector.y > 0f)
			{
				return LevelSelectDirection.UpRight;
			}
			if (vector.x < 0f && vector.y > 0f)
			{
				return LevelSelectDirection.UpLeft;
			}
			if (vector.x > 0f && vector.y < 0f)
			{
				return LevelSelectDirection.DownRight;
			}
			if (vector.x < 0f && vector.y < 0f)
			{
				return LevelSelectDirection.DownLeft;
			}
		}
		if (Mathf.Abs(vector.x) > Mathf.Abs(vector.y))
		{
			return (vector.x > 0f) ? LevelSelectDirection.Right : LevelSelectDirection.Left;
		}
		return (!(vector.y > 0f)) ? LevelSelectDirection.Down : LevelSelectDirection.Up;
	}

	private void NavigateTo(LevelPointButton target)
	{
		if (target == currentPoint)
		{
			return;
		}
		List<LevelPointButton> list = FindPath(lastArrivedPoint, target);
		if (list == null || list.Count == 0)
		{
			return;
		}
		if (lastArrivedPoint != null)
		{
			lastArrivedPoint.SetMarkerPresent(present: false);
		}
		currentPoint.SetSelected(selected: false);
		currentPoint = target;
		currentPoint.SetSelected(selected: true);
		SFXManager.Instance?.Play2D("UI_ButtonHover");
		waypointQueue.Clear();
		foreach (LevelPointButton item in list)
		{
			waypointQueue.Enqueue(item);
		}
		isMoving = true;
		UpdateMarkerFlip(waypointQueue.Peek());
	}

	private void MoveMarkerToTarget()
	{
		if (playerMarker == null || waypointQueue.Count == 0)
		{
			isMoving = false;
			return;
		}
		LevelPointButton point = waypointQueue.Peek();
		Vector3 markerPositionFor = GetMarkerPositionFor(point);
		playerMarker.position = Vector3.MoveTowards(playerMarker.position, markerPositionFor, markerMoveSpeed * Time.deltaTime);
		if (Vector3.Distance(playerMarker.position, markerPositionFor) < 0.02f)
		{
			playerMarker.position = markerPositionFor;
			waypointQueue.Dequeue();
			lastArrivedPoint = point;
			if (waypointQueue.Count > 0)
			{
				UpdateMarkerFlip(waypointQueue.Peek());
				return;
			}
			isMoving = false;
			lastArrivedPoint.SetMarkerPresent(present: true);
		}
	}

	private List<LevelPointButton> FindPath(LevelPointButton from, LevelPointButton to)
	{
		if (from == to)
		{
			return new List<LevelPointButton>();
		}
		HashSet<LevelPointButton> hashSet = new HashSet<LevelPointButton> { from };
		Queue<LevelPointButton> queue = new Queue<LevelPointButton>();
		Dictionary<LevelPointButton, LevelPointButton> dictionary = new Dictionary<LevelPointButton, LevelPointButton> { [from] = null };
		queue.Enqueue(from);
		while (queue.Count > 0)
		{
			LevelPointButton levelPointButton = queue.Dequeue();
			LevelSelectDirection[] allDirections = AllDirections;
			foreach (LevelSelectDirection direction in allDirections)
			{
				LevelPointButton neighbour = levelPointButton.GetNeighbour(direction);
				if (neighbour == null || hashSet.Contains(neighbour))
				{
					continue;
				}
				hashSet.Add(neighbour);
				dictionary[neighbour] = levelPointButton;
				queue.Enqueue(neighbour);
				if (neighbour == to)
				{
					List<LevelPointButton> list = new List<LevelPointButton>();
					LevelPointButton levelPointButton2 = to;
					while (levelPointButton2 != from)
					{
						list.Add(levelPointButton2);
						levelPointButton2 = dictionary[levelPointButton2];
					}
					list.Reverse();
					return list;
				}
			}
		}
		Debug.LogWarning("[LEVEL SELECT] No path found from " + from.name + " to " + to.name + ".");
		return null;
	}

	private void UpdateAnimation()
	{
		if (!(markerAnimator == null))
		{
			markerAnimator.SetInteger(AnimatorState, isMoving ? 1 : 0);
		}
	}

	private void UpdateMarkerFlip(LevelPointButton nextNode)
	{
		if (!(markerRenderer == null) && !(playerMarker == null))
		{
			Vector3 vector = GetMarkerPositionFor(nextNode) - playerMarker.position;
			if (Mathf.Abs(vector.x) > 0.01f)
			{
				markerRenderer.flipX = vector.x < 0f;
			}
		}
	}

	private Vector3 GetMarkerPositionFor(LevelPointButton point)
	{
		Vector3 position = point.transform.position;
		return new Vector3(position.x, position.y + markerYOffset, position.z);
	}
}
