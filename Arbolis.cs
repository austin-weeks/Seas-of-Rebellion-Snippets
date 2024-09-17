using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UI;

public class Arbol : MonoBehaviour, IBoss
{
	public event Action OnBossStarted;
	public event Action OnBossEncounteredEnded;
	public event Action OnBossKilled;
	private string bossName = "Árbolis";
	private string bossTitle = "Wrath of Nature Incarnate";

	[Header("Attacks Settings")]
	[SerializeField] private ArbolBossAttack basicAttack;
	[SerializeField] private ArbolBossAttack vineTrapAttack;
	[SerializeField] private ArbolBossAttack powerCrushAttack;
	[SerializeField] private ArbolBossAttack goopAttack;
	private List<ArbolBossAttack> specialAttacks;
	
	[Header("References")]
	[SerializeField] private Animator animator;
	[SerializeField] private AudioSource arbolAudioSource;
	[SerializeField] private Transform throwPoint;
	[SerializeField] private Transform visualTransform;
	[SerializeField] private BossEntrance bossEntrance;
	[SerializeField] private AudioClip bossMusic;
	[SerializeField] private GameObject barricade;
	[SerializeField] private float attackDistance;
	[SerializeField] private Transform rewardPlinth;
	[SerializeField] private GameObject congratsMessageUI;

	[Header("Throw Attack")]
	[SerializeField] private SoundEffect throwSound;
	[SerializeField] private GameObject woodProjectilePrefab;
	[SerializeField] private GameObject smallLogsPrefab;
	[SerializeField] private LobLandingPoint lobLandingPoint;
	
	[Header("Vine Attack")]
	[SerializeField] private SoundEffect vineSound;
	[SerializeField] private VineTrap vineTrap;
	
	[Header("Crush Attack")]
	[SerializeField] private SoundEffect crushSound;
	[SerializeField] private SoundEffect splashSound;
	[SerializeField] private Transform splashPoint1;
	[SerializeField] private ParticleSystem splashParticles1;
	[SerializeField] private Transform splashPoint2;
	[SerializeField] private ParticleSystem splashParticles2;

	[Header("Minion Spawns")]
	[SerializeField] private SoundEffect minionSpawnRoarSound;
	[SerializeField] private GameObject shieldVisual;
	[SerializeField] private MMF_Player riseFeedback;
	[SerializeField] private MMF_Player lowerFeedback;
	[SerializeField] private List<Transform> spawnPoints;
	[SerializeField] private List<GameObject> firstMinionWave;
	[SerializeField] private List<GameObject> secondMinionWave;
	//[SerializeField] private List<GameObject> thirdMinionWave;
	
	[Header("Goop Attack")]
	[SerializeField] private SoundEffect goopGruntSound;
	[SerializeField] private int goops = 3;
	[SerializeField] private Transform goopThrowPoint;
	[SerializeField] private GameObject goopProjectile;
	[SerializeField] private List<ThickBubbles> thickBubbles;
	
	[Header("Death")]
	[SerializeField] private SoundEffect deathRoar;
	[SerializeField] private SoundEffect deathThud;
	[SerializeField] private ParticleSystem deathSplash;
	private EnemyHealth enemyHealth;
	private BuffDebuffs buffDebuffs;
	private ArbolBossAttack queuedAttack;
	private BossHealthEvents bossHealthEvents;
	private CapsuleCollider damageCollider;
	private float rechargeTimer = -2f;
	private float rechargeTimerMax;
	private const string deathAnim = "Die";
	private const string swipeAnim = "Swipe";
	private const string throwAnim = "Throw";
	private const string vineAnim = "VineTrap";
	private const string crushAnim = "Crush";
	private const string goopAnim = "Goop";
	private const string minionSpawnAnim = "Minions";
	private enum BossAttackState
	{
		Ready,
		Attacking,
		Recharging,
	}
	private BossAttackState attackState = BossAttackState.Recharging;

	private void Awake()
	{
		IBoss.Instance = this;
		
		specialAttacks = new() { vineTrapAttack, powerCrushAttack, goopAttack };
		
		shieldVisual.SetActive(false);
		
		buffDebuffs = GetComponent<BuffDebuffs>();
		bossHealthEvents = GetComponent<BossHealthEvents>();
		damageCollider = GetComponent<CapsuleCollider>();
		damageCollider.enabled = false;
		enemyHealth = GetComponent<EnemyHealth>();
		enemyHealth.DontDestroyOnDeath();
	}
	private void OnDestroy() {
		IBoss.Instance = null;
	}

	private bool encounterStarted;
	private void Start()
	{	
		foreach (ThickBubbles bubbles in thickBubbles)
		{
			bubbles.HideBubbles();
		}
		
		enemyHealth.OnEnemyDied += () =>
		{
			dead = true;
			enemyHealth.ToggleInvulnerability(true);
			StopAllCoroutines();
			StartCoroutine(Die());
		};

		//LevelManager.Instance.IncreaseEnemyCount();

		bossEntrance.OnBossRoomEntered += () =>
		{
			OnBossStarted?.Invoke();
			damageCollider.enabled = true;
			encounterStarted = true;
			MusicManager.Instance.PlaySpecificTrack(bossMusic);
		};

		bossHealthEvents.On66Health += () =>
		{
			goops++;
			
			queuedAttack = new()
			{
				attackType = ArbolBossAttack.AttackType.MinionSpawn,
				enemySpawns = firstMinionWave,
				rechargeTimer = 2f,
			};
		};
		bossHealthEvents.On33Health += () =>
		{
			goops++;
			
			queuedAttack = new()
			{
				attackType = ArbolBossAttack.AttackType.MinionSpawn,
				enemySpawns = secondMinionWave,
				rechargeTimer = 2f,
			};
		};
		// bossHealthEvents.On75Health += () => QueueMinions(1);
		// bossHealthEvents.On50Health += () => QueueMinions(2);
		// bossHealthEvents.On25Health += () => QueueMinions(3);
	}

	private bool dead;
	private void Update()
	{
		if (!encounterStarted) return;
		if (dead) return;
		
		HandleRotation();

		switch (attackState)
		{
			case BossAttackState.Ready:
				ChooseNextAttack();
				break;
			case BossAttackState.Attacking:
				break;
			case BossAttackState.Recharging:
				rechargeTimer += Time.deltaTime;

				if (rechargeTimer >= rechargeTimerMax)
				{
					attackState = BossAttackState.Ready;
					rechargeTimer = 0f;
				}
				break;
		}
	}

	private ArbolBossAttack lastAttack;
	private int basicAttacksPerformed;
	private int basicAttacksBetweenSpecials = 2;
	private void ChooseNextAttack()
	{
		if (queuedAttack != null)
		{
			print("Using queued Attack!");
			Attack(queuedAttack);
			queuedAttack = null;
			return;
		}
		
		if (basicAttacksPerformed < basicAttacksBetweenSpecials)
		{
			basicAttacksPerformed++;
			
			Attack(basicAttack);
			return;
		}
		else basicAttacksPerformed = 0;

		ArbolBossAttack specialAttack = null;
		bool attackChosen = false;
		while (!attackChosen)
		{
			ArbolBossAttack newAttack;
			int i = UnityEngine.Random.Range(0, specialAttacks.Count);
			newAttack = specialAttacks[i];

			float roll = UnityEngine.Random.Range(0f, 1f);
			if (roll < newAttack.attackChance)
			{
				attackChosen = true;
				specialAttack = newAttack;
				lastAttack = specialAttack;
			}
		}
		Attack(specialAttack);
	}


	//This function receives an attack and starts the appropriate coroutine.
	private void Attack(ArbolBossAttack attack)
	{
		attackState = BossAttackState.Attacking;
		rechargeTimerMax = attack.rechargeTimer;
		
		switch (attack.attackType)
		{
			case ArbolBossAttack.AttackType.BasicAttack:
				//Check distance to player
				if (Vector3.Distance(transform.position, Player.Instance.transform.position) < attackDistance * 1.05f)
				{
					StartCoroutine(SwipeAttack(attack));
				}
				else StartCoroutine(ThrowAttack(attack));
				break;
				
			case ArbolBossAttack.AttackType.VineTrap:
				StartCoroutine(VineAttack());
				break;
				
			case ArbolBossAttack.AttackType.PowerCrush:
				StartCoroutine(PowerCrush(attack));
				break;
				
			case ArbolBossAttack.AttackType.MinionSpawn:
				StartCoroutine(MinionSpawn(attack));
				break;
				
			case ArbolBossAttack.AttackType.Goop:
				StartCoroutine(GoopAttack(attack));
				break;
		}
	}

	private void HandleRotation()
	{
		Vector3 towardsPlayer = Player.Instance.transform.position - transform.position;

		visualTransform.forward = -towardsPlayer;
	}

	private IEnumerator SwipeAttack(ArbolBossAttack attack)
	{
		animator.SetTrigger(swipeAnim);

		float animWaitTime = 0.85f;
		yield return new WaitForSeconds(animWaitTime);

		Sound.PlaySound(throwSound, arbolAudioSource);
		
		if (Vector3.Distance(transform.position, Player.Instance.transform.position) < attackDistance)
		{
			PlayerHealth.Instance.Damage(attack.attackDamage, false, transform);
		}

		float remainingAnimTime = 1.5f;
		yield return new WaitForSeconds(remainingAnimTime);

		attackState = BossAttackState.Recharging;
	}
	
	private IEnumerator ThrowAttack(ArbolBossAttack attack)
	{
		animator.SetTrigger(throwAnim);

		float animWaitTime = 1.15f;
		yield return new WaitForSeconds(animWaitTime);

		//Spawning Log
		Sound.PlaySound(throwSound, arbolAudioSource);
		
		Vector3 playerPos = Player.Instance.transform.position;
		lobLandingPoint.SetPosition(new(playerPos.x, 0f, playerPos.z));

		LobProjectile lob = Instantiate(woodProjectilePrefab, transform.position, Quaternion.identity).GetComponent<LobProjectile>();
		float travelTime = 1.9f;
		float maxHeight = 90f;
		lob.SetUp(attack.attackDamage, lobLandingPoint, transform, travelTime, maxHeight);
		lob.TreeThrowSetup(throwPoint.position, smallLogsPrefab, true);

		//Attack Ending
		float remainingAnimTime = 1.4f;
		yield return new WaitForSeconds(remainingAnimTime);

		attackState = BossAttackState.Recharging;
	}
	
	private IEnumerator VineAttack(int requiredEscapeActions = 10, bool partOfCrushAttack = false)
	{	
		animator.SetTrigger(vineAnim);
		
		float animWaitTime = 1.3f;
		yield return new WaitForSeconds(animWaitTime);
		
		Sound.PlaySound(vineSound, arbolAudioSource);
		
		float trapTime = 7f;
		vineTrap.TrapPlayer(trapTime, requiredEscapeActions);
		
		float remainingAnimTime = 1.53f;
		yield return new WaitForSeconds(remainingAnimTime);
		
		if (!partOfCrushAttack) attackState = BossAttackState.Recharging;
	}
	private IEnumerator PowerCrush(ArbolBossAttack attack)
	{
		//VINE PORTION
		StartCoroutine(VineAttack(14, true));
		yield return new WaitForSeconds(2.83f);
		vineTrap.PullInPlayer(transform);
		
		//CRUSH PORTION
		animator.SetTrigger(crushAnim);
		float crushAnimWaitTime = 2.5f;
		yield return new WaitForSeconds(crushAnimWaitTime);
		
		Sound.PlaySound(crushSound, arbolAudioSource);
		Sound.PlaySound(splashSound, arbolAudioSource);
		splashParticles1.transform.position = new(splashPoint1.position.x, 0f, splashPoint1.position.z);
		splashParticles1.Play();
		splashParticles2.transform.position = new(splashPoint2.position.x, 0f, splashPoint2.position.z);
		splashParticles2.Play();
		
		if (Vector3.Distance(transform.position, Player.Instance.transform.position) < attackDistance)
		{
			PlayerHealth.Instance.Damage(attack.attackDamage, true, transform);
		}
		
		float remainingCrushAnimTime = 2f;
		yield return new WaitForSeconds(remainingCrushAnimTime);

		attackState = BossAttackState.Recharging;
	}
	private IEnumerator GoopAttack(ArbolBossAttack attack)
	{
		animator.SetTrigger(goopAnim);

		float animWaitTime = 0.83f;
		yield return new WaitForSeconds(animWaitTime);
		
		Sound.PlaySound(goopGruntSound, arbolAudioSource);
		
		//Do the attack
		List<ThickBubbles> viableThickBubbles = new();
		foreach (ThickBubbles bubbles in thickBubbles)
		{
			viableThickBubbles.Add(bubbles);
		}
		
		float travelTime = 2f;
		float maxHeight = 80f;
		for (int i = 0; i < goops; i++)
		{			
			int index = UnityEngine.Random.Range(0, viableThickBubbles.Count);
			ThickBubbles bubbles = viableThickBubbles[index];
			viableThickBubbles.Remove(bubbles);

			Vector3 throwPoint = new(goopThrowPoint.position.x, 0f, goopThrowPoint.position.z);
			LobProjectile lob = Instantiate(goopProjectile, throwPoint, Quaternion.identity).GetComponent<LobProjectile>();
			//enemySounds.PlayShootSounds();
			lob.SetUp(attack.attackDamage, null, transform, travelTime, maxHeight);
			lob.GoopSetup(bubbles);

			//Delay between goops
			yield return new WaitForSeconds(0.1f);	
		}
		
		yield return new WaitForSeconds(1f);

		attackState = BossAttackState.Recharging;
	}
	private List<EnemyHealth> activeMinions = new();
	private IEnumerator MinionSpawn(ArbolBossAttack attack)
	{
		print("Minion Spawn Started!");
		//minionGraphicFeedback.PlayFeedbacks();
		
		animator.SetTrigger(minionSpawnAnim);
		StartCoroutine(HandleShield());
		
		float animWaitTime = 0.5f;
		yield return new WaitForSeconds(animWaitTime);
		
		Sound.PlaySound(minionSpawnRoarSound, arbolAudioSource);

		List<Transform> viableSpawnPoints = new();
		foreach (Transform transform in spawnPoints)
		{
			viableSpawnPoints.Add(transform);
		}

		for (int i = 0; i < attack.enemySpawns.Count; i++)
		{
			int index = UnityEngine.Random.Range(0, viableSpawnPoints.Count);
			Transform spawnPoint = viableSpawnPoints[index];
			viableSpawnPoints.Remove(spawnPoint);
			
			EnemyHealth minion = Instantiate(attack.enemySpawns[i], spawnPoint.position, Quaternion.identity).GetComponent<EnemyHealth>();
			minion.GetComponent<EnemyMovement>().detectDistance = 1000f;
			
			activeMinions.Add(minion);
			minion.OnDied += () => activeMinions.Remove(minion);
			
			//EnemySOHolder minion = Instantiate(minionPrefab, spawnPoint.position, Quaternion.identity).GetComponent<EnemySOHolder>();
			// minion.SkipSetup();
			// minion.enemySO = correctMinionLevelSO;
			//minion.InvokeOnEnemySOChanged();

			yield return new WaitForSeconds(0.5f);
		}

		print("Minion Spawn Ended!");
		attackState = BossAttackState.Recharging;
	}
	private IEnumerator HandleShield()
	{
		damageCollider.enabled = false;
		shieldVisual.SetActive(true);
		
		//Resetting Root Positions
		//Transform visualsTransform = shieldVisual.transform.GetChild(0);
		//visualsTransform.localPosition = new(0f, -8.53f, 0f);
		
		riseFeedback.PlayFeedbacks();
		
		yield return new WaitForSeconds(3f);
		
		while (activeMinions.Count > 0)
		{
			yield return null;
		}
		
		lowerFeedback.PlayFeedbacks();
		yield return new WaitForSeconds(1f);
		
		shieldVisual.SetActive(false);
		damageCollider.enabled = true;
	}
	
	private IEnumerator Die()
	{
		OnBossEncounteredEnded?.Invoke();
		//Camera.main.GetComponent<TopDownCameraFollow>().ForceCloseCamera();

		visualTransform.rotation = Quaternion.identity;
		animator.SetTrigger(deathAnim);
		
		Sound.PlaySound(deathRoar, arbolAudioSource);
		
		yield return new WaitForSeconds(4.38f);
		
		Sound.PlaySound(deathRoar, arbolAudioSource);
		
		yield return new WaitForSeconds(4.08f);
		
		Sound.PlaySound(deathThud, arbolAudioSource);
		deathSplash.Play();
		
		yield return new WaitForSeconds(2f);
		
		OnBossKilled?.Invoke();
		MusicManager.Instance.SwapToLevelClearedMusic(false);
		//playFalling Sound
		
		Camera.main.GetComponent<TopDownCameraFollow>().ToggleBossCamera(false);
		
		Vector3 correctedPlinthPosition = new(transform.position.x + 1.7f, 0f, transform.position.z - 9.4f);
		rewardPlinth.position = correctedPlinthPosition;
		
		barricade.SetActive(false);
		
		ShowCongratulations();
		Destroy(gameObject);
	}

	private void ShowCongratulations()
	{
		GameObject congratsMessage = Instantiate(congratsMessageUI, FindFirstObjectByType<PopupParent>().transform);
		congratsMessage.transform.GetComponentInChildren<Button>().onClick.AddListener(() =>
		{
			Destroy(congratsMessage);
		});
	}

	public string GetBossName()
	{
		return bossName;
	}
	public string GetBossTitle()
	{
		return bossTitle;
	}
	public Transform GetTransform()
	{
		return transform;
	}
}
