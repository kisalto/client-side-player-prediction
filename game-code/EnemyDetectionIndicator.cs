using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EnemyDetectionIndicator : MonoBehaviour
{
	private enum IndicatorState
	{
		Hidden,
		Detection,
		Combat,
		Lost
	}

	[Header("References")]
	[SerializeField]
	private Image indicatorImage;

	[SerializeField]
	private EnemyDetection enemyDetection;

	[SerializeField]
	private EnemyController enemyController;

	[Header("Sprites")]
	[SerializeField]
	private Sprite exclamationSprite;

	[SerializeField]
	private Sprite questionSprite;

	[Header("Settings")]
	[SerializeField]
	private Vector3 offset = new Vector3(0f, 1.5f, 0f);

	[SerializeField]
	private float initialShowDuration = 1.5f;

	[SerializeField]
	private bool showDuringCombat = true;

	[SerializeField]
	private float lostPlayerDuration = 2f;

	[SerializeField]
	private bool mirrorOffsetWithEnemy = true;

	[Header("Trash Can Settings")]
	[SerializeField]
	private bool hideWhenTrashCanIsHiding = true;

	[Header("Animation")]
	[SerializeField]
	private bool useAnimation = true;

	[SerializeField]
	private float popInDuration = 0.3f;

	[SerializeField]
	private float popInScale = 1.2f;

	[SerializeField]
	private float slideUpDistance = 1f;

	[SerializeField]
	private float fadeOutDuration = 0.5f;

	[Header("Colors")]
	[SerializeField]
	private Color detectionColor = Color.yellow;

	[SerializeField]
	private Color combatColor = Color.red;

	[SerializeField]
	private Color lostColor = new Color(0.7f, 0.7f, 0.7f, 1f);

	[SerializeField]
	private bool changeColorInCombat = true;

	[Header("Debug")]
	[SerializeField]
	private bool showDebugLogs;

	private IndicatorState currentState;

	private IndicatorState previousState;

	private bool wasDetectingLastFrame;

	private Coroutine hideCoroutine;

	private Coroutine animationCoroutine;

	private Transform enemyTransform;

	private EnemyMovement enemyMovement;

	private TrashCanBehavior trashCanBehavior;

	private Canvas canvas;

	private CanvasGroup canvasGroup;

	private Vector3 targetPosition;

	private Vector3 startSlidePosition;

	private void Awake()
	{
		enemyTransform = GetComponentInParent<EnemyController>()?.transform;
		if (enemyTransform == null)
		{
			Debug.LogError("[EnemyDetectionIndicator] Must be a child of an enemy with EnemyController!");
			base.enabled = false;
			return;
		}
		canvas = GetComponentInParent<Canvas>();
		enemyMovement = enemyTransform.GetComponent<EnemyMovement>();
		trashCanBehavior = enemyTransform.GetComponent<TrashCanBehavior>();
		if (indicatorImage != null)
		{
			canvasGroup = indicatorImage.GetComponent<CanvasGroup>();
			if (canvasGroup == null)
			{
				canvasGroup = indicatorImage.gameObject.AddComponent<CanvasGroup>();
				if (showDebugLogs)
				{
					Debug.Log("[EnemyDetectionIndicator] Added CanvasGroup to indicator image");
				}
			}
			if (exclamationSprite == null)
			{
				exclamationSprite = indicatorImage.sprite;
			}
			if (enemyDetection == null)
			{
				enemyDetection = enemyTransform.GetComponent<EnemyDetection>();
			}
			if (enemyController == null)
			{
				enemyController = enemyTransform.GetComponent<EnemyController>();
			}
			if (enemyDetection == null || enemyController == null)
			{
				Debug.LogError($"[EnemyDetectionIndicator] Missing references! Detection: {enemyDetection != null}, Controller: {enemyController != null}");
				base.enabled = false;
				return;
			}
			Hide();
			if (showDebugLogs)
			{
				Debug.Log("[EnemyDetectionIndicator] Initialized successfully!");
			}
		}
		else
		{
			Debug.LogError("[EnemyDetectionIndicator] Indicator Image is not assigned!");
			base.enabled = false;
		}
	}

	private void Update()
	{
		UpdateTargetPosition();
		CheckStatus();
		EnsureSpriteNotMirrored();
	}

	private void UpdateTargetPosition()
	{
		if (!(enemyTransform == null))
		{
			Vector3 vector = offset;
			if (mirrorOffsetWithEnemy && enemyMovement != null && !enemyMovement.IsFacingRight)
			{
				vector.x = 0f - offset.x;
			}
			targetPosition = enemyTransform.position + vector;
		}
	}

	private void EnsureSpriteNotMirrored()
	{
		if (!(indicatorImage == null))
		{
			Vector3 localScale = indicatorImage.transform.localScale;
			if (enemyMovement != null && !enemyMovement.IsFacingRight)
			{
				localScale.x = 0f - Mathf.Abs(localScale.x);
			}
			else
			{
				localScale.x = Mathf.Abs(localScale.x);
			}
			indicatorImage.transform.localScale = localScale;
		}
	}

	private void CheckStatus()
	{
		if (enemyDetection == null || enemyController == null)
		{
			return;
		}
		if (hideWhenTrashCanIsHiding && IsTrashCanHiding())
		{
			if (currentState != IndicatorState.Hidden)
			{
				if (showDebugLogs)
				{
					Debug.Log("[EnemyDetectionIndicator] Trash can is hiding, hiding indicator.");
				}
				Hide();
			}
			return;
		}
		bool hasDetectedPlayer = enemyDetection.HasDetectedPlayer;
		bool flag = IsEnemyInCombat();
		if (hasDetectedPlayer && currentState == IndicatorState.Hidden)
		{
			if (showDebugLogs)
			{
				Debug.Log("[EnemyDetectionIndicator] Player detected!");
			}
			ShowDetection();
		}
		else if (flag && showDuringCombat && currentState != IndicatorState.Combat)
		{
			if (showDebugLogs)
			{
				Debug.Log("[EnemyDetectionIndicator] Enemy in combat!");
			}
			ShowCombat();
		}
		else if (!hasDetectedPlayer && wasDetectingLastFrame && currentState != IndicatorState.Hidden)
		{
			if (showDebugLogs)
			{
				Debug.Log("[EnemyDetectionIndicator] Lost player!");
			}
			ShowLost();
		}
		else if (!hasDetectedPlayer && !flag && currentState != IndicatorState.Lost && currentState != IndicatorState.Hidden)
		{
			if (showDebugLogs)
			{
				Debug.Log("[EnemyDetectionIndicator] No longer in combat, hiding.");
			}
			HideWithAnimation();
		}
		wasDetectingLastFrame = hasDetectedPlayer;
	}

	private bool IsTrashCanHiding()
	{
		if (trashCanBehavior == null)
		{
			return false;
		}
		EnemyAnimationState currentAnimationState = enemyController.GetCurrentAnimationState();
		if (currentAnimationState != EnemyAnimationState.Hide && currentAnimationState != EnemyAnimationState.HideIdle)
		{
			return currentAnimationState == EnemyAnimationState.HideScared;
		}
		return true;
	}

	private bool IsEnemyInCombat()
	{
		if (enemyController == null)
		{
			return false;
		}
		EnemyAnimationState currentAnimationState = enemyController.GetCurrentAnimationState();
		if (currentAnimationState != EnemyAnimationState.Attack && currentAnimationState != EnemyAnimationState.Chase)
		{
			return currentAnimationState == EnemyAnimationState.Reveal;
		}
		return true;
	}

	private void ShowDetection()
	{
		previousState = currentState;
		currentState = IndicatorState.Detection;
		if (indicatorImage != null)
		{
			if (exclamationSprite != null)
			{
				indicatorImage.sprite = exclamationSprite;
			}
			if (changeColorInCombat)
			{
				indicatorImage.color = detectionColor;
			}
		}
		if (enemyTransform != null)
		{
			SFXManager.Instance?.Play("E_Alert", enemyTransform.position);
		}
		Show(initialShowDuration, slideUp: true);
	}

	private void ShowCombat()
	{
		previousState = currentState;
		currentState = IndicatorState.Combat;
		if (indicatorImage != null)
		{
			if (exclamationSprite != null)
			{
				indicatorImage.sprite = exclamationSprite;
			}
			if (changeColorInCombat)
			{
				indicatorImage.color = combatColor;
			}
		}
		Show(0f, slideUp: true);
	}

	private void ShowLost()
	{
		previousState = currentState;
		currentState = IndicatorState.Lost;
		if (indicatorImage != null)
		{
			if (questionSprite != null)
			{
				indicatorImage.sprite = questionSprite;
			}
			if (changeColorInCombat)
			{
				indicatorImage.color = lostColor;
			}
		}
		if (hideCoroutine != null)
		{
			StopCoroutine(hideCoroutine);
		}
		hideCoroutine = StartCoroutine(HideAfterDelay(lostPlayerDuration));
	}

	private void Show(float duration, bool slideUp)
	{
		if (!(indicatorImage == null) && !(canvasGroup == null))
		{
			if (hideCoroutine != null)
			{
				StopCoroutine(hideCoroutine);
				hideCoroutine = null;
			}
			if (animationCoroutine != null)
			{
				StopCoroutine(animationCoroutine);
			}
			indicatorImage.gameObject.SetActive(value: true);
			if (useAnimation)
			{
				animationCoroutine = StartCoroutine(SlideUpAndPopAnimation());
			}
			else
			{
				canvasGroup.alpha = 1f;
				indicatorImage.transform.localScale = Vector3.one;
				indicatorImage.transform.position = targetPosition;
			}
			if (duration > 0f)
			{
				hideCoroutine = StartCoroutine(HideAfterDelay(duration));
			}
		}
	}

	private void Hide()
	{
		if (!(canvasGroup == null) && !(indicatorImage == null))
		{
			currentState = IndicatorState.Hidden;
			if (hideCoroutine != null)
			{
				StopCoroutine(hideCoroutine);
				hideCoroutine = null;
			}
			if (animationCoroutine != null)
			{
				StopCoroutine(animationCoroutine);
				animationCoroutine = null;
			}
			canvasGroup.alpha = 0f;
			indicatorImage.gameObject.SetActive(value: false);
		}
	}

	private void HideWithAnimation()
	{
		if (!(canvasGroup == null) && !(indicatorImage == null))
		{
			if (hideCoroutine != null)
			{
				StopCoroutine(hideCoroutine);
				hideCoroutine = null;
			}
			if (animationCoroutine != null)
			{
				StopCoroutine(animationCoroutine);
			}
			animationCoroutine = StartCoroutine(FadeDownAnimation());
		}
	}

	private IEnumerator SlideUpAndPopAnimation()
	{
		if (!(canvasGroup == null) && !(indicatorImage == null))
		{
			startSlidePosition = enemyTransform.position + new Vector3(0f, slideUpDistance * 0.1f, 0f);
			if (mirrorOffsetWithEnemy && enemyMovement != null && !enemyMovement.IsFacingRight)
			{
				startSlidePosition.x = enemyTransform.position.x - offset.x;
			}
			else if (mirrorOffsetWithEnemy && enemyMovement != null)
			{
				startSlidePosition.x = enemyTransform.position.x + offset.x;
			}
			canvasGroup.alpha = 0f;
			indicatorImage.transform.localScale = Vector3.zero;
			indicatorImage.transform.position = startSlidePosition;
			float elapsed = 0f;
			float totalDuration = popInDuration;
			while (elapsed < totalDuration)
			{
				elapsed += Time.deltaTime;
				float num = elapsed / totalDuration;
				float t = EaseOutCubic(num);
				indicatorImage.transform.position = Vector3.Lerp(startSlidePosition, targetPosition, t);
				float t2 = EaseOutBack(num);
				float num2 = Mathf.LerpUnclamped(0f, popInScale, t2);
				float x = ((enemyMovement != null && !enemyMovement.IsFacingRight) ? (0f - Mathf.Abs(num2)) : Mathf.Abs(num2));
				indicatorImage.transform.localScale = new Vector3(x, num2, num2);
				canvasGroup.alpha = num;
				yield return null;
			}
			elapsed = 0f;
			float bounceBackDuration = popInDuration * 0.5f;
			while (elapsed < bounceBackDuration)
			{
				elapsed += Time.deltaTime;
				float t3 = elapsed / bounceBackDuration;
				float num3 = Mathf.Lerp(popInScale, 1f, t3);
				float x2 = ((enemyMovement != null && !enemyMovement.IsFacingRight) ? (0f - Mathf.Abs(num3)) : Mathf.Abs(num3));
				indicatorImage.transform.localScale = new Vector3(x2, num3, num3);
				yield return null;
			}
			indicatorImage.transform.localScale = Vector3.one;
			indicatorImage.transform.position = targetPosition;
			canvasGroup.alpha = 1f;
			animationCoroutine = null;
		}
	}

	private IEnumerator FadeDownAnimation()
	{
		if (!(canvasGroup == null) && !(indicatorImage == null))
		{
			float startAlpha = canvasGroup.alpha;
			Vector3 startScale = indicatorImage.transform.localScale;
			Vector3 startPos = indicatorImage.transform.position;
			Vector3 endPos = enemyTransform.position + new Vector3(0f, slideUpDistance * 0.1f, 0f);
			if (mirrorOffsetWithEnemy && enemyMovement != null && !enemyMovement.IsFacingRight)
			{
				endPos.x = enemyTransform.position.x - offset.x;
			}
			else if (mirrorOffsetWithEnemy && enemyMovement != null)
			{
				endPos.x = enemyTransform.position.x + offset.x;
			}
			float elapsed = 0f;
			while (elapsed < fadeOutDuration)
			{
				elapsed += Time.deltaTime;
				float t = elapsed / fadeOutDuration;
				float t2 = EaseInCubic(t);
				canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t2);
				float num = Mathf.Lerp(startScale.y, 0f, t2);
				float x = ((enemyMovement != null && !enemyMovement.IsFacingRight) ? (0f - Mathf.Abs(num)) : Mathf.Abs(num));
				indicatorImage.transform.localScale = new Vector3(x, num, num);
				indicatorImage.transform.position = Vector3.Lerp(startPos, endPos, t2);
				yield return null;
			}
			Hide();
			animationCoroutine = null;
		}
	}

	private IEnumerator HideAfterDelay(float delay)
	{
		yield return new WaitForSeconds(delay);
		HideWithAnimation();
		hideCoroutine = null;
	}

	private float EaseOutBack(float t)
	{
		float num = 1.70158f;
		float num2 = num + 1f;
		return 1f + num2 * Mathf.Pow(t - 1f, 3f) + num * Mathf.Pow(t - 1f, 2f);
	}

	private float EaseOutCubic(float t)
	{
		return 1f - Mathf.Pow(1f - t, 3f);
	}

	private float EaseInCubic(float t)
	{
		return t * t * t;
	}
}
