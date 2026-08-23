public class PlayerBlockState : PlayerStateBase
{
	private PlayerAnimationController animController;

	private int currentBlockType;

	private bool blockFlag;

	public override PlayerAnimationState AnimationState => PlayerAnimationState.Block;

	public PlayerBlockState(PlayerStateMachine player)
		: base(player)
	{
	}

	public override void OnEnter()
	{
		if (animController == null)
		{
			animController = player.GetComponent<PlayerAnimationController>();
		}
		blockFlag = false;
		UpdateBlockMode();
		SFXManager.Instance?.Play("P_Block", player.Movement.transform.position);
		currentBlockType = DetermineBlockType();
		animController.SetBlockType(currentBlockType);
		animController.SetState(6);
		animController.SetBlocking(isBlocking: false);
		if (player.Input.HasBufferedParry)
		{
			player.Input.ConsumeParryBuffer();
			if (player.Input.StickJumpHeld)
			{
				player.ChangeState<PlayerParryUpState>();
			}
			else
			{
				player.ChangeState<PlayerParryState>();
			}
		}
	}

	public override void OnUpdate()
	{
		if (!blockFlag)
		{
			animController.SetBlocking(isBlocking: false);
			blockFlag = true;
		}
		else
		{
			animController.SetBlocking(isBlocking: true);
		}
		UpdateBlockMode();
		UpdateBlockType();
		CheckTransitions();
	}

	public override void OnFixedUpdate()
	{
		player.Movement.Move(player.Input.MoveInput.x * player.Data.blockMovementMultiplier);
		player.Movement.ApplyGravity();
	}

	public override void OnExit()
	{
		player.Combat.StopBlock();
	}

	private void CheckTransitions()
	{
		if (player.Input.AttackPressed)
		{
			if (player.Input.StickJumpHeld)
			{
				player.ChangeState<PlayerParryUpState>();
			}
			else
			{
				player.ChangeState<PlayerParryState>();
			}
		}
		else if (!player.Input.BlockHeld)
		{
			if (IsMovementInputActive())
			{
				player.ChangeState<PlayerMoveState>();
			}
			else
			{
				player.ChangeState<PlayerIdleState>();
			}
		}
	}

	private void UpdateBlockMode()
	{
		if (player.Input.StickJumpHeld)
		{
			player.Combat.StartBlockUp();
		}
		else
		{
			player.Combat.StartBlock();
		}
	}

	private void UpdateBlockType()
	{
		int num = DetermineBlockType();
		if (num != currentBlockType)
		{
			currentBlockType = num;
			animController.SetBlockType(currentBlockType);
		}
	}

	private int DetermineBlockType()
	{
		bool flag = IsMovementInputActive();
		bool stickJumpHeld = player.Input.StickJumpHeld;
		if (stickJumpHeld & flag)
		{
			return 3;
		}
		if (stickJumpHeld)
		{
			return 2;
		}
		if (flag)
		{
			return 1;
		}
		return 0;
	}
}
