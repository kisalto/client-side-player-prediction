using System;
using UnityEngine;

public class PlayerDashController : MonoBehaviour
{
	private PlayerData data;

	private float dashTimer;

	private float dashCooldownTimer;

	private AfterImageEffect afterImageEffect;

	public bool IsDashing { get; private set; }

	public bool CanDash
	{
		get
		{
			if (dashCooldownTimer <= 0f)
			{
				return !IsDashing;
			}
			return false;
		}
	}

	public event Action OnDashStart;

	public event Action OnDashEnd;

	private void Awake()
	{
		afterImageEffect = GetComponent<AfterImageEffect>();
	}

	public void Init(PlayerData playerData)
	{
		data = playerData;
	}

	public void UpdateTimers(float deltaTime)
	{
		if (IsDashing)
		{
			dashTimer -= deltaTime;
			if (dashTimer <= 0f)
			{
				EndDash();
			}
		}
		if (dashCooldownTimer > 0f)
		{
			dashCooldownTimer -= deltaTime;
		}
	}

	public void StartDash()
	{
		if (CanDash)
		{
			IsDashing = true;
			dashTimer = data.dashDuration;
			dashCooldownTimer = data.dashCooldown;
			if (afterImageEffect != null)
			{
				afterImageEffect.StartAfterImages();
			}
			OnDashStart?.Invoke();
		}
	}

	private void EndDash()
	{
		IsDashing = false;
		if (afterImageEffect != null)
		{
			afterImageEffect.StopAfterImages();
		}
		OnDashEnd?.Invoke();
	}

	public float GetDashCooldownProgress()
	{
		return 1f - dashCooldownTimer / data.dashCooldown;
	}
}
