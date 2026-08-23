using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStateMachine : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private PlayerData data;

	private StateMachine stateMachine;

	private Dictionary<Type, IState> stateRegistry = new Dictionary<Type, IState>();

	public PlayerData Data => data;

	public PlayerMovementController Movement { get; private set; }

	public PlayerCombatController Combat { get; private set; }

	public PlayerHealthController Health { get; private set; }

	public PlayerInputReader Input { get; private set; }

	public PlayerDashController Dash { get; private set; }

	public PlayerVisualEffectsController VisualEffects { get; private set; }

	public Animator Animator { get; private set; }

	public event Action OnHurtEnd;

	private void Awake()
	{
		data = UnityEngine.Object.Instantiate(data);
		stateMachine = new StateMachine();
		CacheComponents();
		InitComponentsData();
		RegisterStates();
		SubscribeToEvents();
	}

	private void CacheComponents()
	{
		Movement = GetComponent<PlayerMovementController>();
		Combat = GetComponent<PlayerCombatController>();
		Health = GetComponent<PlayerHealthController>();
		Input = GetComponent<PlayerInputReader>();
		Dash = GetComponent<PlayerDashController>();
		Animator = GetComponentInChildren<Animator>();
		VisualEffects = GetComponent<PlayerVisualEffectsController>();
	}

	private void InitComponentsData()
	{
		Movement.Init(data);
		Combat.Init(data);
		Health.Init(data);
		Dash.Init(data);
		VisualEffects.Init(data);
	}

	private void RegisterStates()
	{
		RegisterState(new PlayerIdleState(this));
		RegisterState(new PlayerMoveState(this));
		RegisterState(new PlayerJumpState(this));
		RegisterState(new PlayerFallState(this));
		RegisterState(new PlayerDashState(this));
		RegisterState(new PlayerAttackState(this));
		RegisterState(new PlayerBlockState(this));
		RegisterState(new PlayerParryState(this));
		RegisterState(new PlayerParryUpState(this));
		RegisterState(new PlayerClimbState(this));
		RegisterState(new PlayerDeadState(this));
		RegisterState(new PlayerHurtState(this));
		RegisterState(new PlayerDeflectedState(this));
	}

	public void RegisterState(IState state)
	{
		Type type = state.GetType();
		if (!stateRegistry.ContainsKey(type))
		{
			stateRegistry.Add(type, state);
		}
	}

	public T GetState<T>() where T : class, IState
	{
		if (stateRegistry.TryGetValue(typeof(T), out var value))
		{
			return value as T;
		}
		return null;
	}

	public void ChangeState<T>() where T : class, IState
	{
		IState state = GetState<T>();
		if (state != null)
		{
			stateMachine.ChangeState(state);
		}
	}

	public void NotifyHurtEnd()
	{
		OnHurtEnd?.Invoke();
	}

	private void Start()
	{
		ChangeState<PlayerIdleState>();
	}

	private void Update()
	{
		Combat.UpdateTimers(Time.deltaTime);
		Health.UpdateTimers(Time.deltaTime);
		Dash.UpdateTimers(Time.deltaTime);
		stateMachine.Update();
	}

	private void FixedUpdate()
	{
		stateMachine.FixedUpdate();
	}

	private void SubscribeToEvents()
	{
		if ((bool)Health)
		{
			Health.OnHurt += HandleHurt;
		}
	}

	private void HandleHurt()
	{
		ChangeState<PlayerHurtState>();
	}

	public bool IsInState<T>() where T : IState
	{
		return stateMachine.GetCurrentState() is T;
	}

	public PlayerAnimationState GetCurrentAnimationState()
	{
		return stateMachine.GetCurrentState()?.AnimationState ?? PlayerAnimationState.Idle;
	}

	private void OnDestroy()
	{
		if ((bool)Health)
		{
			Health.OnHurt -= HandleHurt;
		}
	}
}
