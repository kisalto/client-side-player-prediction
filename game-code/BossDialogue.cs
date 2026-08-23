using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class BossDialogue : MonoBehaviour
{
	[SerializeField]
	private TextMeshProUGUI textComponent;

	[SerializeField]
	private string[] dialogueLines;

	[SerializeField]
	private float textSpeed = 0.05f;

	private BossTrigger bossTrigger;

	private Action onDialogueComplete;

	private int currentLineIndex;

	private bool isTyping;

	private bool isIntroDialogue;

	private Coroutine typingCoroutine;

	private PlayerInputReader playerInputReader;

	private Rigidbody2D playerRigidbody;

	private PlayerAnimationController playerAnimController;

	private PlayerSFXHandler playerSFXHandler;

	private RigidbodyConstraints2D originalConstraints;

	public static bool IsDialogueActive { get; private set; }

	public void Initialize(BossTrigger trigger)
	{
		bossTrigger = trigger;
		isIntroDialogue = true;
		IsDialogueActive = true;
		LockPlayer();
		StartDialogue();
	}

	public void Initialize(Action onComplete)
	{
		onDialogueComplete = onComplete;
		isIntroDialogue = false;
		IsDialogueActive = true;
		LockPlayer();
		StartDialogue();
	}

	private void LockPlayer()
	{
		GameObject gameObject = GameObject.FindGameObjectWithTag("Player");
		if (!(gameObject == null))
		{
			playerInputReader = gameObject.GetComponent<PlayerInputReader>();
			if (playerInputReader != null)
			{
				playerInputReader.enabled = false;
			}
			playerRigidbody = gameObject.GetComponent<Rigidbody2D>();
			if (playerRigidbody != null)
			{
				originalConstraints = playerRigidbody.constraints;
				playerRigidbody.linearVelocity = new Vector2(0f, playerRigidbody.linearVelocity.y);
				playerRigidbody.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
			}
			playerSFXHandler = gameObject.GetComponent<PlayerSFXHandler>();
			if (playerSFXHandler != null)
			{
				playerSFXHandler.enabled = false;
			}
			PlayerStateMachine component = gameObject.GetComponent<PlayerStateMachine>();
			if (component != null)
			{
				component.ChangeState<PlayerIdleState>();
			}
			playerAnimController = gameObject.GetComponent<PlayerAnimationController>();
			if (playerAnimController != null)
			{
				playerAnimController.SetState(0);
				playerAnimController.enabled = false;
			}
		}
	}

	private void UnlockPlayer()
	{
		if (playerInputReader != null)
		{
			playerInputReader.enabled = true;
		}
		if (playerRigidbody != null)
		{
			playerRigidbody.constraints = originalConstraints;
		}
		if (playerSFXHandler != null)
		{
			playerSFXHandler.enabled = true;
		}
		if (playerAnimController != null)
		{
			playerAnimController.enabled = true;
		}
	}

	private void StartDialogue()
	{
		if (dialogueLines.Length != 0)
		{
			ShowNextLine();
		}
	}

	private void Update()
	{
		if ((Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) && (Keyboard.current == null || !Keyboard.current.spaceKey.wasPressedThisFrame) && (Gamepad.current == null || !Gamepad.current.buttonSouth.wasPressedThisFrame))
		{
			return;
		}
		if (isTyping)
		{
			if (typingCoroutine != null)
			{
				StopCoroutine(typingCoroutine);
			}
			textComponent.text = dialogueLines[currentLineIndex];
			isTyping = false;
		}
		else
		{
			AdvanceDialogue();
		}
	}

	private void AdvanceDialogue()
	{
		currentLineIndex++;
		SFXManager.Instance?.Play2D("UI_DialogueAdvance");
		if (currentLineIndex < dialogueLines.Length)
		{
			ShowNextLine();
		}
		else
		{
			EndDialogue();
		}
	}

	private void ShowNextLine()
	{
		if (typingCoroutine != null)
		{
			StopCoroutine(typingCoroutine);
		}
		if (currentLineIndex == dialogueLines.Length - 1 && isIntroDialogue)
		{
			SFXManager.Instance?.Play2D("B_EvilLaugh");
		}
		typingCoroutine = StartCoroutine(TypeLine(dialogueLines[currentLineIndex]));
	}

	private IEnumerator TypeLine(string line)
	{
		isTyping = true;
		textComponent.text = "";
		for (int i = 0; i < line.Length; i++)
		{
			char c = line[i];
			textComponent.text += c;
			if (!char.IsWhiteSpace(c))
			{
				SFXManager.Instance?.PlayExclusive2D("UI_DialogueTick");
			}
			yield return new WaitForSeconds(textSpeed);
		}
		isTyping = false;
	}

	private void EndDialogue()
	{
		IsDialogueActive = false;
		UnlockPlayer();
		bossTrigger?.StartBoss();
		onDialogueComplete?.Invoke();
		UnityEngine.Object.Destroy(base.gameObject);
	}

	private void OnDestroy()
	{
		IsDialogueActive = false;
	}
}
