using UnityEngine;

public class AnimationEventForwarder : MonoBehaviour
{
	private PlayerCombatController combat;

	private PlayerVisualEffectsController visualEffects;

	private PlayerAnimationController animController;

	private void Awake()
	{
		combat = GetComponentInParent<PlayerCombatController>();
		visualEffects = GetComponentInParent<PlayerVisualEffectsController>();
		animController = GetComponentInParent<PlayerAnimationController>();
		if (combat == null)
		{
			Debug.LogError("[AnimationEventForwarder] PlayerCombatController not found in parent hierarchy.", this);
		}
		if (visualEffects == null)
		{
			Debug.LogError("[AnimationEventForwarder] PlayerVisualEffectsController not found in parent hierarchy.", this);
		}
		if (animController == null)
		{
			Debug.LogError("[AnimationEventForwarder] PlayerAnimationController not found in parent hierarchy.", this);
		}
	}

	public void OnAttackImpact()
	{
		combat?.OnAttackImpact();
	}

	public void OnDownAttackImpact()
	{
		combat?.OnDownAttackImpact();
	}

	public void OnDeflectedShake()
	{
		visualEffects?.OnDeflectedShake();
	}

	public void DisableAnimator()
	{
		animController?.DisableAnimator();
	}
}
