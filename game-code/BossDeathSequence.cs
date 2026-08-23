using System;
using System.Collections;
using UnityEngine;

public class BossDeathSequence : MonoBehaviour
{
	[Header("Explosions")]
	[SerializeField]
	private string explosionEffectName = "Explosion";

	[SerializeField]
	private float deathExplosionInterval = 0.5f;

	[SerializeField]
	private int deathExplosionCount = 10;

	[SerializeField]
	private float deathExplosionRandomOffset = 1.5f;

	[Header("Camera")]
	[SerializeField]
	private float deathCameraHoldDuration = 2f;

	[SerializeField]
	private float deathShakeAmplitude = 0.15f;

	[SerializeField]
	private float deathShakeFrequency = 1f;

	[SerializeField]
	private float finalDeathShakeAmplitude = 0.5f;

	[SerializeField]
	private float finalDeathShakeDuration = 0.6f;

	[Header("Gate")]
	[SerializeField]
	private BossGateTrigger bossGateTrigger;

	private IVFXService vFXService;

	private void Awake()
	{
		vFXService = ServiceLocator.VFX ?? VFXManager.Instance;
	}

	public Coroutine Run(Animator animator, int deadAnimState, Action<float> onExplosion, Action onComplete)
	{
		return StartCoroutine(DeathSequenceRoutine(animator, deadAnimState, onExplosion, onComplete));
	}

	private IEnumerator DeathSequenceRoutine(Animator animator, int deadAnimState, Action<float> onExplosion, Action onComplete)
	{
		for (int i = 0; i < deathExplosionCount; i++)
		{
			bool num = i == deathExplosionCount - 1;
			Vector2 vector = (num ? Vector2.zero : new Vector2(UnityEngine.Random.Range(0f - deathExplosionRandomOffset, deathExplosionRandomOffset), UnityEngine.Random.Range(0f - deathExplosionRandomOffset, deathExplosionRandomOffset)));
			Vector2 vector2 = (Vector2)base.transform.position + vector;
			vFXService?.PlayEffect(explosionEffectName, vector2);
			vFXService?.PlayEffect("ArmourBreak", (Vector2)base.transform.position);
			SFXManager.Instance?.Play("ITEM_RocketExplosion", vector2);
			SFXManager.Instance?.Play("B_ArmourBreak", (Vector2)base.transform.position);
			if (num)
			{
				CameraShakeManager.Instance?.ShakeCamera(finalDeathShakeAmplitude, deathShakeFrequency, finalDeathShakeDuration);
			}
			else
			{
				CameraShakeManager.Instance?.ShakeCamera(deathShakeAmplitude, deathShakeFrequency, deathExplosionInterval);
			}
			onExplosion?.Invoke(deathExplosionInterval);
			if (!num)
			{
				yield return new WaitForSeconds(deathExplosionInterval);
			}
		}
		animator?.SetInteger("State", deadAnimState);
		yield return new WaitForSeconds(deathCameraHoldDuration);
		if ((bool)bossGateTrigger)
		{
			bossGateTrigger.ReverseMovement();
		}
		onComplete?.Invoke();
	}
}
