using System;
using System.Collections.Generic;

public class EnemyStateFactory
{
	private readonly EnemyController controller;

	private readonly EnemyConfig config;

	private readonly EnemyDetection detection;

	private readonly EnemyCombat combat;

	private readonly EnemyIdleState idleState;

	private readonly EnemyPatrolState patrolState;

	private readonly EnemyChaseState chaseState;

	private readonly EnemyAttackState attackState;

	private readonly EnemyReturnState returnState;

	private readonly EnemyDeadState deadState;

	private readonly Dictionary<Type, IEnemyState> customStates = new Dictionary<Type, IEnemyState>();

	public EnemyIdleState Idle => idleState;

	public EnemyPatrolState Patrol => patrolState;

	public EnemyChaseState Chase => chaseState;

	public EnemyAttackState Attack => attackState;

	public EnemyReturnState Return => returnState;

	public EnemyDeadState Dead => deadState;

	public EnemyStateFactory(EnemyController controller, EnemyConfig config, EnemyDetection detection, EnemyCombat combat)
	{
		this.controller = controller;
		this.config = config;
		this.detection = detection;
		this.combat = combat;
		idleState = new EnemyIdleState(controller, config, detection, this);
		patrolState = new EnemyPatrolState(controller, config, detection, this);
		chaseState = new EnemyChaseState(controller, config, detection, combat, this);
		attackState = new EnemyAttackState(controller, config, detection, combat, this);
		returnState = new EnemyReturnState(controller, config, detection, this);
		deadState = new EnemyDeadState(controller);
	}

	public void RegisterCustomState<T>(T state) where T : IEnemyState
	{
		customStates[typeof(T)] = state;
	}

	public T GetCustomState<T>() where T : IEnemyState
	{
		if (customStates.TryGetValue(typeof(T), out var value))
		{
			return (T)value;
		}
		return default(T);
	}
}
