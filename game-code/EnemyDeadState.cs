using UnityEngine;

public class EnemyDeadState : IEnemyState
{
	private readonly EnemyController enemy;

	public EnemyAnimationState AnimationState => EnemyAnimationState.Death;

	public EnemyDeadState(EnemyController enemyController)
	{
		enemy = enemyController;
	}

	public void OnEnter()
	{
		enemy.Movement.Stop();
		Rigidbody2D component = enemy.GetComponent<Rigidbody2D>();
		if (component != null)
		{
			component.linearVelocity = Vector2.zero;
			component.simulated = false;
		}
		Collider2D component2 = enemy.GetComponent<Collider2D>();
		if (component2 != null)
		{
			component2.enabled = false;
		}
		Canvas[] componentsInChildren = enemy.GetComponentsInChildren<Canvas>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].enabled = false;
		}
	}

	public void OnUpdate()
	{
	}

	public void OnFixedUpdate()
	{
	}

	public void OnExit()
	{
	}
}
