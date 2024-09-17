using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Player : MonoBehaviour
{
	public static Player Instance
	{
		get
		{
			if (_instance != null) return _instance;
			else 
			{
				_instance = FindFirstObjectByType<Player>();
				return _instance;
			}
		}
	}
	private void OnDestroy() => _instance = null;
	private static Player _instance;
	
	private Rigidbody rigidBody;
	private BuffDebuffs buffDebuffs;
	private CapsuleCollider capsuleCollider;
	private PlayerShipStats playerShipStats;
	[SerializeField] private Transform playerVisualParent;
	[field:SerializeField] public SpawnPoof Poof {get; private set;}
	[Header("Player Ship Prefabs")]
	[SerializeField] GameObject sloopPrefab;
	[SerializeField] GameObject schoonerPrefab;
	[SerializeField] GameObject galleonPrefab;

	private float moveSpeed;
	private float rotationSpeed;

	private float stamina;
	private float staminaMax = 6f;
	readonly float sprintSpeedBoost = 1.8f;
	readonly float sprintDrainRate = 2;
	readonly float aimDrainRate = 1f;

	private void Awake()
	{
		rigidBody = GetComponent<Rigidbody>();
		buffDebuffs = GetComponent<BuffDebuffs>();
		capsuleCollider = GetComponent<CapsuleCollider>();
		playerShipStats = GetComponent<PlayerShipStats>();
	}

	private void Start()
	{		
		playerShipStats.OnStatsChanged += SetData;
		GameManager.Instance.OnLevelCleared += () => invincibleDash = false;
		SetData();
	}

	private void FixedUpdate()
	{
		if (resetPosition)
		{
			rigidBody.velocity = Vector3.zero;
			transform.position = respawnPosition;
			transform.forward = respawnForwardTransform;
			resetPosition = false;
		}
		
		if (immobilized) return;

		CheckIfAiming();
		HandleStamina();

		switch (GameInput.Instance.GetMovementMode())
		{
			case GameInput.MovementMode.MouseDirectional:
			case GameInput.MovementMode.ControllerDirectional:
				DirectionalMovement();
				break;
			case GameInput.MovementMode.KeyboardSteer:
			case GameInput.MovementMode.ControllerSteer:
				SteeringMovement();
				break;
		}
	}

	private float currentMoveSpeed;
	private bool isSprinting;
	private void HandleStamina()
	{
		if (GameInput.Instance.PlayerSprintingAndMoving() && stamina > 0f)
		{
			currentMoveSpeed = moveSpeed * sprintSpeedBoost * buffDebuffs.moveSpeedModifier;
			
			float staminaDrain = buffDebuffs.staminaDrainModifier;
			if (invincibleDash) staminaDrain += InvincibleDashRelic.staminaDrainMod;
			stamina -= sprintDrainRate * staminaDrain * Time.deltaTime;
			isSprinting = true;
			return;
		}
		else isSprinting = false;
		
		if (GameInput.Instance.PlayerAiming() && stamina > 0f)
		{
			float aimingSpeedModifier = 0.5f;
			currentMoveSpeed = moveSpeed * aimingSpeedModifier * buffDebuffs.moveSpeedModifier;

			stamina -= aimDrainRate * Time.deltaTime;
			return;
		}

		currentMoveSpeed = moveSpeed * buffDebuffs.moveSpeedModifier;
		
		//player not sprinting and not hold sprint
		if (StaminaActions()) { return; }
		//stamina regeneration
		stamina += Time.deltaTime;
		if (stamina > staminaMax)
		{
			stamina = staminaMax;
		}
	}

	private void DirectionalMovement()
	{
		Vector3 moveDir = GameInput.Instance.GetMovementDirection();

		if (!GameInput.Instance.ShouldMoveForward()) { return; }

		HandleRotation(moveDir);

		//applying force to move player
		float movementDistance = currentMoveSpeed * Time.deltaTime;
		rigidBody.velocity += transform.forward * movementDistance;
	}

	private void SteeringMovement()
	{
		Vector3 moveDir;
		Vector3 inputVector = GameInput.Instance.GetMovementDirection();
		if (inputVector == Vector3.left)
		{
			moveDir = Vector3.Cross(transform.forward, Vector3.up);
		}
		else if (inputVector == Vector3.right)
		{
			moveDir = Vector3.Cross(transform.forward, Vector3.down);
		}
		else { moveDir = transform.forward; }

		if (!GameInput.Instance.ShouldMoveForward()) { return; }

		HandleRotation(moveDir);

		//applying force to move player
		float movementDistance = currentMoveSpeed * Time.deltaTime;
		rigidBody.velocity += transform.forward * movementDistance;

	}
	private void HandleRotation(Vector3 moveDir)
	{
		transform.forward = Vector3.RotateTowards(transform.forward, moveDir, rotationSpeed * buffDebuffs.rotationSpeedModifier * Time.deltaTime, 5f);
		//transform.forward = Vector3.Slerp(transform.forward, moveDir, rotationSpeed * Time.deltaTime);
		transform.localEulerAngles = new Vector3 (0f, transform.localEulerAngles.y, 0f);
	}
	
	//Make sure this doesn't break anything
	public event Action OnAimStarted;
	public event Action OnAimEnded;
	bool aimedLastFixedUpdate;
	public bool CheckIfAiming()
	{
		if (PauseMenuUI.Instance.IsPaused) return false;
		
		if (GameInput.Instance.PlayerAiming() && stamina > 0f)
		{
			if (!aimedLastFixedUpdate) OnAimStarted?.Invoke();
			aimedLastFixedUpdate = true;
			
			Time.timeScale = 0.82f;
			return true;
		}
		else if (Time.timeScale != 0)
		{
			if (aimedLastFixedUpdate) OnAimEnded?.Invoke();
			aimedLastFixedUpdate = false;
			
			Time.timeScale = 1f;
			return false;
		}
		else return false;
	}

	//Setting Functions
	ShipType currentShipType;
	bool firstLoad = true;
	private void SetData()
	{
		//Always set stats as hull/sail upgrades change stats even if shiptype stays the same.
		SetStats();
		
		if (!firstLoad)
		{
			if (currentShipType == PlayerShipStats.Instance.CurrentShip) return;
		}
		else firstLoad = false;

		currentShipType = PlayerShipStats.Instance.CurrentShip;
		SetVisual();
		SetCollider();
	}

	private void SetStats()
	{
		moveSpeed = playerShipStats.MoveSpeed;
		rotationSpeed = playerShipStats.RotationSpeed;
		staminaMax = playerShipStats.StaminaMax;
		stamina = staminaMax;
	}

	private void SetVisual()
	{
		var shipPrefab = currentShipType switch
		{
			ShipType.Sloop => sloopPrefab,
			ShipType.Schooner => schoonerPrefab,
			ShipType.Galleon => galleonPrefab,
			_ => sloopPrefab,
		};

		for (int i = 0; i < playerVisualParent.childCount; i++)
		{
			Destroy(playerVisualParent.GetChild(i).gameObject);
		}
		Instantiate(shipPrefab, playerVisualParent);
	}

	private void SetCollider()
	{
		switch (currentShipType)
		{
			case ShipType.Sloop:
				SetColliderMethod(new(0f, 1f, 0f), 2.6f, 12.9f);
				break;
			case ShipType.Schooner:
				SetColliderMethod(new(0f, 1.5f, 1.5f), 3.2f, 23f);
				break;
			case ShipType.Galleon:
				SetColliderMethod(new(0f, 2f, 1.5f), 4f, 27f);
				break;
		}
	}
	private void SetColliderMethod(Vector3 pos, float radius, float height)
	{
		capsuleCollider.center = pos;
		capsuleCollider.radius = radius;
		capsuleCollider.height = height;
	}

	public bool IsSprinting()
	{
		return isSprinting;
	}

	public float GetStamina()
	{
		return stamina / staminaMax;
	}

	private bool resetPosition;
	Vector3 respawnPosition;
	Vector3 respawnForwardTransform;
	public void ResetPosition(Vector3 newPosition, Vector3 newForwardTransform)
	{
		respawnForwardTransform = newForwardTransform;
		respawnPosition = newPosition;
		resetPosition = true;
	}
	private bool immobilized;
	public void ImmobilizePlayer(float immobilizedTime)
	{
		StopAllCoroutines();
		StartCoroutine(HandleImmobilization(immobilizedTime));
	}
	public void CancelImmobilization()
	{
		StopAllCoroutines();
		immobilized = false;
	}
	private IEnumerator HandleImmobilization(float immobilizedTime)
	{
		immobilized = true;
		//rigidBody.velocity = Vector3.zero;
		yield return new WaitForSeconds(immobilizedTime);
		immobilized = false;
	}
	bool invincibleDash;
	public void EnableInvincibleDash()
	{
		invincibleDash = true;
	}
	
	/// <summary>
	/// Returns true if the the player is holding boost or aiming. Indicated attemp to use stamina, not that stamina is being used.
	/// </summary>
	/// <returns></returns>
	public bool StaminaActions()
	{
		return GameInput.Instance.PlayerHoldingSprint() || GameInput.Instance.PlayerAiming();
	}
	
	/// <summary>
	/// Return true if stamina is currently being drained.
	/// </summary>
	/// <returns></returns>
	public bool UsingStamina()
	{
		return (GameInput.Instance.PlayerHoldingSprint() && GameInput.Instance.ShouldMoveForward()) || GameInput.Instance.PlayerAiming();
	}
}
