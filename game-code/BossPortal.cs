using System.Collections;
using UnityEngine;

public class BossPortal : MonoBehaviour
{
	[Header("Rise Settings")]
	[SerializeField]
	private float riseFromDepth = 3f;

	[SerializeField]
	private float riseDuration = 0.4f;

	[Header("Bullet Spawn Offset")]
	[Tooltip("Offsets where bullets spawn relative to the portal's position. Does not affect the portal GameObject's position in any way.")]
	[SerializeField]
	private Vector2 spawnOffset = Vector2.zero;

	[Header("Open Animation")]
	[Tooltip("Fallback: barrage starts after this many seconds if OnOpenAnimationComplete is never received.")]
	[SerializeField]
	private float openAnimationFallbackDuration = 1.5f;

	[Tooltip("Delay after the open animation finishes before the portal starts shooting (single barrage).")]
	[SerializeField]
	private float delayBeforeShooting;

	[Tooltip("Delay after the open animation finishes before the portal starts shooting (multi-wave barrage).")]
	[SerializeField]
	private float multiWaveDelayBeforeShooting;

	[Header("Barrage Settings")]
	[SerializeField]
	private float barrageDuration = 2.5f;

	[SerializeField]
	private float minTimeBetweenBullets = 0.05f;

	[SerializeField]
	private float maxTimeBetweenBullets = 0.18f;

	[SerializeField]
	private float portalWidth = 2f;

	[Header("Pool Settings")]
	[SerializeField]
	private string bulletPoolTag = "PortalBullet";

	[Header("Close Animation")]
	[Tooltip("Fallback: portal destroys itself after this many seconds if OnCloseAnimationComplete is never received.")]
	[SerializeField]
	private float closeAnimationFallbackDuration = 1f;

	[Header("VFX")]
	[Tooltip("Pool tag for the per-bullet splash particle.")]
	[SerializeField]
	private string bulletSplashPoolTag = "PortalBulletSplash";

	private const int ANIM_STATE_IDLE = 0;

	private const int ANIM_STATE_OPEN = 1;

	private const int ANIM_STATE_CLOSE = 2;

	private bool _openAnimationComplete;

	private bool _closeAnimationComplete;

	private bool _isMultiWave;

	private Color _splashColor = Color.white;

	private Animator animator;

	private SpriteRenderer spriteRenderer;

	private void Awake()
	{
		animator = GetComponent<Animator>();
		spriteRenderer = GetComponent<SpriteRenderer>();
	}

	public void Initialize(bool isMultiWave = false)
	{
		_isMultiWave = isMultiWave;
		_openAnimationComplete = false;
		SFXManager.Instance?.Play("B_Portal", base.transform.position);
		animator?.SetInteger("State", 1);
		StartCoroutine(PortalSequence());
	}

	public void Initialize(bool isMultiWave, Color tint)
	{
		if (spriteRenderer != null)
		{
			spriteRenderer.color = tint;
		}
		Initialize(isMultiWave);
		_splashColor = tint;
	}

	public void OnOpenAnimationComplete()
	{
		_openAnimationComplete = true;
	}

	public void OnCloseAnimationComplete()
	{
		_closeAnimationComplete = true;
	}

	private IEnumerator PortalSequence()
	{
		Vector3 finalPosition = base.transform.position;
		Vector3 startPosition = finalPosition + Vector3.down * riseFromDepth;
		base.transform.position = startPosition;
		float elapsed = 0f;
		while (elapsed < riseDuration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.SmoothStep(0f, 1f, elapsed / riseDuration);
			base.transform.position = Vector3.Lerp(startPosition, finalPosition, t);
			yield return null;
		}
		base.transform.position = finalPosition;
		float openFallback = 0f;
		yield return new WaitUntil(delegate
		{
			openFallback += Time.deltaTime;
			return _openAnimationComplete || openFallback >= openAnimationFallbackDuration;
		});
		animator?.SetInteger("State", 0);
		float num = (_isMultiWave ? multiWaveDelayBeforeShooting : delayBeforeShooting);
		if (num > 0f)
		{
			yield return new WaitForSeconds(num);
		}
		yield return StartCoroutine(FireBarrage());
		_closeAnimationComplete = false;
		animator?.SetInteger("State", 2);
		float closeFallback = 0f;
		yield return new WaitUntil(delegate
		{
			closeFallback += Time.deltaTime;
			return _closeAnimationComplete || closeFallback >= closeAnimationFallbackDuration;
		});
		Object.Destroy(base.gameObject);
	}

	private IEnumerator FireBarrage()
	{
		if (string.IsNullOrEmpty(bulletPoolTag))
		{
			Debug.LogWarning("[BossPortal] No bullet pool tag assigned.");
			yield break;
		}
		Vector3 bulletOrigin = base.transform.position + new Vector3(spawnOffset.x, spawnOffset.y, 0f);
		int soundHandle = SFXManager.Instance?.PlayLooped("B_PortalFire", base.transform.position) ?? (-1);
		float elapsed = 0f;
		while (elapsed < barrageDuration)
		{
			float x = bulletOrigin.x + Random.Range((0f - portalWidth) * 0.5f, portalWidth * 0.5f);
			Vector3 position = new Vector3(x, bulletOrigin.y, 0f);
			ObjectPool.Instance.SpawnFromPool(bulletPoolTag, position, Quaternion.identity)?.GetComponent<PortalBullet>()?.Initialize(Vector2.up);
			SpawnBulletSplash(position, Quaternion.identity);
			float num = Random.Range(minTimeBetweenBullets, maxTimeBetweenBullets);
			elapsed += num;
			yield return new WaitForSeconds(num);
		}
		if (soundHandle >= 0)
		{
			SFXManager.Instance?.Stop(soundHandle);
		}
	}

	private void SpawnBulletSplash(Vector3 position, Quaternion rotation)
	{
		if (string.IsNullOrEmpty(bulletSplashPoolTag))
		{
			return;
		}
		GameObject gameObject = ObjectPool.Instance.SpawnFromPool(bulletSplashPoolTag, position, rotation);
		if (!(gameObject == null))
		{
			ParticleSystem component = gameObject.GetComponent<ParticleSystem>();
			if (component != null)
			{
				ParticleSystem.MainModule main = component.main;
				main.startColor = _splashColor;
			}
		}
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.magenta;
		Gizmos.DrawWireCube(base.transform.position, new Vector3(portalWidth, 0.2f, 0f));
		if (spawnOffset != Vector2.zero)
		{
			Vector3 vector = base.transform.position + new Vector3(spawnOffset.x, spawnOffset.y, 0f);
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireCube(vector, new Vector3(portalWidth, 0.2f, 0f));
			Gizmos.color = Color.green;
			Gizmos.DrawLine(base.transform.position, vector);
		}
	}
}
