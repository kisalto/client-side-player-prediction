using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerMovementController : MonoBehaviour
{
	private PlayerData data;

	[Header("Ladder Detection")]
	[SerializeField]
	private float ladderDetectionRadius = 0.5f;

	[SerializeField]
	private LayerMask ladderLayer;

	private PlayerStateMachine playerStateMachine;

	private Rigidbody2D rb;

	private BoxCollider2D col;

	private float coyoteTimeCounter;

	private float originalGravityScale;

	public bool IsGrounded { get; private set; }

	public bool IsFacingRight { get; private set; } = true;

	public bool IsOnLadder { get; private set; }

	public bool LadderCollided { get; private set; }

	public Vector2 Velocity => rb.linearVelocity;

	public bool CanCoyoteJump => coyoteTimeCounter > 0f;

	public bool IsClimbing
	{
		get
		{
			if (IsOnLadder)
			{
				return rb.gravityScale == 0f;
			}
			return false;
		}
	}

	public bool WasLaunchedExternally { get; private set; }

	private void Awake()
	{
		rb = GetComponent<Rigidbody2D>();
		col = GetComponent<BoxCollider2D>();
		playerStateMachine = GetComponent<PlayerStateMachine>();
		originalGravityScale = rb.gravityScale;
	}

	public void Init(PlayerData playerData)
	{
		data = playerData;
	}

	private void FixedUpdate()
	{
		CheckGroundStatus();
		CheckLadderStatus();
		UpdateCoyoteTime();
	}

	public void Move(float horizontalInput)
	{
		float num = horizontalInput * data.moveSpeed;
		float x = rb.linearVelocity.x;
		float num2 = ((!IsGrounded) ? ((Mathf.Abs(num) > 0.01f) ? data.airAcceleration : data.airDeceleration) : ((Mathf.Abs(num) > 0.01f) ? data.groundAcceleration : data.groundAcceleration));
		float x2 = Mathf.MoveTowards(x, num, num2 * Time.fixedDeltaTime);
		rb.linearVelocity = new Vector2(x2, rb.linearVelocity.y);
		if (horizontalInput != 0f)
		{
			Flip(horizontalInput > 0f);
		}
	}

	public void StopMovement()
	{
		if (IsGrounded)
		{
			rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
		}
	}

	public void StartClimbing()
	{
		IsOnLadder = true;
		rb.gravityScale = 0f;
	}

	public void Climb(float verticalInput, float horizontalInput)
	{
		rb.gravityScale = 0f;
		Vector2 linearVelocity = rb.linearVelocity;
		linearVelocity.y = verticalInput * data.climbSpeed;
		linearVelocity.x = horizontalInput * data.climbSpeed;
		rb.linearVelocity = linearVelocity;
	}

	public void StopClimbing(bool preserveVerticalVelocity = false)
	{
		IsOnLadder = false;
		rb.gravityScale = originalGravityScale;
		if (preserveVerticalVelocity)
		{
			rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
		}
		else
		{
			rb.linearVelocity = Vector2.zero;
		}
	}

	public void StickJump()
	{
		if (!LadderCollided)
		{
			rb.linearVelocity = new Vector2(rb.linearVelocity.x, data.jumpForce);
			coyoteTimeCounter = 0f;
		}
	}

	public void Jump()
	{
		rb.linearVelocity = new Vector2(rb.linearVelocity.x, data.jumpForce);
		coyoteTimeCounter = 0f;
	}

	public void ExternalLaunch(float verticalForce)
	{
		rb.linearVelocity = new Vector2(rb.linearVelocity.x, verticalForce);
		coyoteTimeCounter = 0f;
		WasLaunchedExternally = true;
	}

	public void ClearExternalLaunch()
	{
		WasLaunchedExternally = false;
	}

	public void CutJump()
	{
		if (rb.linearVelocity.y > 0f)
		{
			rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
		}
	}

	public void Dash(float direction)
	{
		rb.linearVelocity = new Vector2(direction * data.dashSpeed, 0f);
	}

	public void ApplyGravity()
	{
		Vector2 linearVelocity = rb.linearVelocity;
		if (linearVelocity.y < 0f)
		{
			linearVelocity += Vector2.up * Physics2D.gravity.y * (data.fallMultiplier - 1f) * Time.fixedDeltaTime;
			linearVelocity.y = Mathf.Max(linearVelocity.y, data.maxFallSpeed);
		}
		else if (rb.linearVelocity.y > 0f)
		{
			linearVelocity += Vector2.up * Physics2D.gravity.y * (data.gravity - 1f) * Time.fixedDeltaTime;
		}
		rb.linearVelocity = linearVelocity;
	}

	public void ApplyLowJumpGravity()
	{
		Vector2 linearVelocity = rb.linearVelocity;
		if (linearVelocity.y > 0f)
		{
			linearVelocity += Vector2.up * Physics2D.gravity.y * (data.lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
		}
		linearVelocity.y = Mathf.Max(linearVelocity.y, data.maxFallSpeed);
		rb.linearVelocity = linearVelocity;
	}

	public void SetVelocity(Vector2 velocity)
	{
		rb.linearVelocity = velocity;
	}

	private void CheckGroundStatus()
	{
		Vector2 point = (Vector2)base.transform.position + col.offset + Vector2.down * (col.size.y * 0.5f);
		Vector2 size = new Vector2(col.size.x * 0.9f, data.groundCheckDistance);
		IsGrounded = Physics2D.OverlapBox(point, size, 0f, data.groundLayer);
	}

	private void CheckLadderStatus()
	{
		Collider2D collider2D = Physics2D.OverlapCircle(base.transform.position, ladderDetectionRadius, ladderLayer);
		LadderCollided = collider2D != null;
		if (IsClimbing && !LadderCollided)
		{
			IsOnLadder = false;
		}
	}

	private void UpdateCoyoteTime()
	{
		if (IsGrounded)
		{
			coyoteTimeCounter = data.coyoteTime;
		}
		else
		{
			coyoteTimeCounter -= Time.deltaTime;
		}
	}

	public void Flip(bool facingRight)
	{
		if (IsFacingRight != facingRight)
		{
			IsFacingRight = facingRight;
			Vector3 localScale = base.transform.localScale;
			localScale.x *= -1f;
			base.transform.localScale = localScale;
		}
	}

	private void OnDrawGizmosSelected()
	{
		if (!col)
		{
			col = GetComponent<BoxCollider2D>();
		}
		if ((bool)data)
		{
			Vector2 vector = (Vector2)base.transform.position + col.offset + Vector2.down * (col.size.y * 0.5f);
			Vector2 vector2 = new Vector2(col.size.x * 0.9f, data.groundCheckDistance);
			Gizmos.color = (IsGrounded ? Color.green : Color.red);
			Gizmos.DrawWireCube(vector, vector2);
			Gizmos.color = (IsOnLadder ? Color.cyan : Color.yellow);
			Gizmos.DrawWireSphere(base.transform.position, ladderDetectionRadius);
		}
	}
}
