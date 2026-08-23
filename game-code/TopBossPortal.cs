using System.Collections;
using UnityEngine;

public class TopBossPortal : MonoBehaviour
{
	[Header("Drop-in Settings")]
	[SerializeField]
	private float dropFromHeight = 3f;

	[SerializeField]
	private float dropDuration = 0.4f;

	[Header("Bullet Spawn Offset")]
	[Tooltip("Offsets where bullets spawn relative to the portal's position. Does not affect the portal GameObject's position in any way.")]
	[SerializeField]
	private Vector2 spawnOffset = Vector2.zero;

	[Header("Open Animation")]
	[SerializeField]
	private float openAnimationFallbackDuration = 1.5f;

	[SerializeField]
	private float delayBeforeShooting;

	[Header("Barrage Settings")]
	[SerializeField]
	private float barrageDuration = 2.5f;

	[SerializeField]
	private float minTimeBetweenBullets = 0.1f;

	[SerializeField]
	private float maxTimeBetweenBullets = 0.25f;

	[SerializeField]
	private float portalWidth = 2f;

	[Header("Pool Settings")]
	[SerializeField]
	private string bulletPoolTag = "TopPortalBullet";

	[Header("Close Animation")]
	[SerializeField]
	private float closeAnimationFallbackDuration = 1f;

	[Header("VFX")]
	[Tooltip("Pool tag for the per-bullet splash particle.")]
	[SerializeField]
	private string bulletSplashPoolTag = "PortalBulletSplash";

	private static readonly Quaternion FLIPPED_ROTATION = Quaternion.Euler(0f, 0f, 180f);

	private const int ANIM_STATE_IDLE = 0;

	private const int ANIM_STATE_OPEN = 1;

	private const int ANIM_STATE_CLOSE = 2;

	private bool _openAnimationComplete;

	private bool _closeAnimationComplete;

	private Color _splashColor = Color.white;

	private Animator animator;

	private SpriteRenderer spriteRenderer;

	private void Awake()
	{
		animator = GetComponent<Animator>();
		spriteRenderer = GetComponent<SpriteRenderer>();
	}

	public void Initialize()
	{
		_openAnimationComplete = false;
		SFXManager.Instance?.Play("B_Portal", base.transform.position);
		animator?.SetInteger("State", 1);
		StartCoroutine(PortalSequence());
	}

	public void Initialize(Color tint)
	{
		if (spriteRenderer != null)
		{
			spriteRenderer.color = tint;
		}
		Initialize();
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
		Vector3 startPosition = finalPosition + Vector3.up * dropFromHeight;
		base.transform.position = startPosition;
		float elapsed = 0f;
		while (elapsed < dropDuration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.SmoothStep(0f, 1f, elapsed / dropDuration);
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
		if (delayBeforeShooting > 0f)
		{
			yield return new WaitForSeconds(delayBeforeShooting);
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
			Debug.LogWarning("[TopBossPortal] No bullet pool tag assigned.");
			yield break;
		}
		Vector3 bulletOrigin = base.transform.position + new Vector3(spawnOffset.x, spawnOffset.y, 0f);
		int soundHandle = SFXManager.Instance?.PlayLooped("B_PortalFire", base.transform.position) ?? (-1);
		float elapsed = 0f;
		while (elapsed < barrageDuration)
		{
			float x = bulletOrigin.x + Random.Range((0f - portalWidth) * 0.5f, portalWidth * 0.5f);
			Vector3 position = new Vector3(x, bulletOrigin.y, 0f);
			ObjectPool.Instance.SpawnFromPool(bulletPoolTag, position, Quaternion.identity)?.GetComponent<TopPortalBullet>()?.Initialize(base.transform);
			SpawnBulletSplash(position);
			float num = Random.Range(minTimeBetweenBullets, maxTimeBetweenBullets);
			elapsed += num;
			yield return new WaitForSeconds(num);
		}
		if (soundHandle >= 0)
		{
			SFXManager.Instance?.Stop(soundHandle);
		}
	}

	private void SpawnBulletSplash(Vector3 position)
	{
		if (string.IsNullOrEmpty(bulletSplashPoolTag))
		{
			return;
		}
		GameObject gameObject = ObjectPool.Instance.SpawnFromPool(bulletSplashPoolTag, position, FLIPPED_ROTATION);
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
