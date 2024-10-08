using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

public class Arbol : MonoBehaviour, IBoss
{
	public event Action OnBossStarted;
	public event Action OnBossEncounteredEnded;
	public event Action OnBossKilled;
	const string bossName = "Árbolis";
	const string bossTitle = "Wrath of Nature Incarnate";

	[Header("Attacks Settings")]
	[SerializeField] private ArbolBossAttack basicAttack;
	[SerializeField] private ArbolBossAttack vineTrapAttack;
	[SerializeField] private ArbolBossAttack powerCrushAttack;
	[SerializeField] private ArbolBossAttack goopAttack;
	private List<ArbolBossAttack> specialAttacks;
	
	[Header("Component References")]
	[SerializeField] private Animator animator;
	[SerializeField] private AudioSource arbolAudioSource;
	[SerializeField] private Transform throwPoint;
	[SerializeField] private Transform visualTransform;
	[SerializeField] private BossEntrance bossEntrance;
	[SerializeField] private AudioClip bossMusic;
	[SerializeField] private GameObject barricade;
	[SerializeField] private float attackDistance;
	[SerializeField] private Transform rewardPlinth;
	[SerializeField] private SoundEffect basicAttackSound;

	[Header("Throw Attack")]
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
	
	[Header("Goop Attack")]
	[SerializeField] private SoundEffect goopGruntSound;
	[SerializeField] private Transform goopThrowPoint;
	[SerializeField] private GameObject goopProjectile;
	[SerializeField] private List<ThickBubbles> thickBubbles;
	
	[Header("Death Sequence")]
	[SerializeField] private SoundEffect deathRoar;
	[SerializeField] private SoundEffect deathThud;
	[SerializeField] private ParticleSystem deathSplash;

	private EnemyHealth enemyHealth;
	private ArbolBossAttack queuedAttack;
	private BossHealthEvents bossHealthEvents;
	private CapsuleCollider hitbox;
	private float rechargeTimer = -2f;
	private float attackCooldownTime;
	const string deathAnim = "Die";
	const string swipeAnim = "Swipe";
	const string throwAnim = "Throw";
	const string vineAnim = "VineTrap";
	const string crushAnim = "Crush";
	const string goopAnim = "Goop";
	const string minionSpawnAnim = "Minions";
	private enum AttackState
	{
		Ready,
		Attacking,
		Recharging,
	}

	//Boss State Variables
	private AttackState attackState = AttackState.Recharging;
	private bool encounterStarted;
	private bool dead;
	private int goopsPerAttack = 3;

	private void Awake() {
		IBoss.Instance = this;
		
		specialAttacks = new() { vineTrapAttack, powerCrushAttack, goopAttack };

		shieldVisual.SetActive(false);
		
		bossHealthEvents = GetComponent<BossHealthEvents>();
		hitbox = GetComponent<CapsuleCollider>();
		hitbox.enabled = false;
		enemyHealth = GetComponent<EnemyHealth>();
		enemyHealth.DontDestroyOnDeath();
	}

	private void OnDestroy() => IBoss.Instance = null;

	private void Start() {	
		foreach (ThickBubbles bubbles in thickBubbles) bubbles.HideBubbles();
		
		enemyHealth.OnEnemyDied += () => {
			dead = true;
			enemyHealth.ToggleInvulnerability(true);
			StopAllCoroutines();
			StartCoroutine(Die());
		};

		bossEntrance.OnBossRoomEntered += () => {
			OnBossStarted?.Invoke();
			hitbox.enabled = true;
			encounterStarted = true;
			MusicManager.Instance.PlaySpecificTrack(bossMusic);
		};

		bossHealthEvents.On66Health += () => {
			goopsPerAttack++;
			queuedAttack = new() {
				attackType = ArbolBossAttack.AttackType.MinionSpawn,
				enemySpawns = firstMinionWave,
				rechargeTimer = 2f,
			};
		};
		bossHealthEvents.On33Health += () => {
			goopsPerAttack++;
			queuedAttack = new() {
				attackType = ArbolBossAttack.AttackType.MinionSpawn,
				enemySpawns = secondMinionWave,
				rechargeTimer = 2f,
			};
		};
	}

	private void Update() {
		if (!encounterStarted || dead) return;
		
		RotateTowardsPlayer();

		if (attackState == AttackState.Ready) ChooseNextAttack();
		else if (attackState == AttackState.Recharging) {
			rechargeTimer += Time.deltaTime;
			if (rechargeTimer >= attackCooldownTime) {
				attackState = AttackState.Ready;
				rechargeTimer = 0f;
			}
		}
	}

	private int basicAttacksPerformed;
	const int basicAttacksBetweenSpecials = 2;
	private void ChooseNextAttack() {
		if (queuedAttack != null){
			Attack(queuedAttack);
			queuedAttack = null;
			return;
		}
		
		//Should next attack be a basic attack?
		if (basicAttacksPerformed < basicAttacksBetweenSpecials) {
			basicAttacksPerformed++;
			Attack(basicAttack);
			return;
		}

		//Choose and perform a special attack
		basicAttacksPerformed = 0;

		while (true) {
			var attack = specialAttacks.Random();
			if (Utils.DiceRollCheck(attack.attackChance)) {
				Attack(attack);
				break;
			}
		}
	}


	private void Attack(ArbolBossAttack attack) {
		attackState = AttackState.Attacking;
		attackCooldownTime = attack.rechargeTimer;
		
		switch (attack.attackType) {
			case ArbolBossAttack.AttackType.BasicAttack:
				//Swipe if player close, throw log if player far
				if (Vector3.Distance(transform.position, Player.Instance.transform.position) < attackDistance * 1.05f)
					StartCoroutine(SwipeAttack(attack));
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

	private void RotateTowardsPlayer() {
		Vector3 towardsPlayer = Player.Instance.transform.position - transform.position;
		visualTransform.forward = -towardsPlayer;
	}

	private IEnumerator SwipeAttack(ArbolBossAttack attack) {
		animator.SetTrigger(swipeAnim);
		yield return new WaitForSeconds(0.85f);

		Sound.PlaySound(basicAttackSound, arbolAudioSource);
		
		//Damage player if within attack range
		if (Vector3.Distance(transform.position, Player.Instance.transform.position) < attackDistance)
			PlayerHealth.Instance.Damage(attack.attackDamage, false, transform);

		yield return new WaitForSeconds(1.5f);
		attackState = AttackState.Recharging;
	}
	
	private IEnumerator ThrowAttack(ArbolBossAttack attack) {
		animator.SetTrigger(throwAnim);
		yield return new WaitForSeconds(1.15f);

		Sound.PlaySound(basicAttackSound, arbolAudioSource);
		
		var playerPos = Player.Instance.transform.position;
		lobLandingPoint.SetPosition(playerPos.Flatten());

		var log = Instantiate(woodProjectilePrefab, transform.position, Quaternion.identity).GetComponent<LobProjectile>();
		float travelTime = 1.9f, maxHeight = 90f;
		log.SetUp(attack.attackDamage, lobLandingPoint, transform, travelTime, maxHeight);
		log.TreeThrowSetup(throwPoint.position, smallLogsPrefab, true);

		//Attack Ending
		yield return new WaitForSeconds(1.4f);
		attackState = AttackState.Recharging;
	}
	
	private IEnumerator VineAttack(int requiredEscapeActions = 10, bool partOfCrushAttack = false) {
		animator.SetTrigger(vineAnim);
		yield return new WaitForSeconds(1.3f);
		
		Sound.PlaySound(vineSound, arbolAudioSource);
		
		float trapTime = 7f;
		vineTrap.TrapPlayer(trapTime, requiredEscapeActions);
		
		yield return new WaitForSeconds(1.53f);
		if (!partOfCrushAttack) attackState = AttackState.Recharging;
	}

	private IEnumerator PowerCrush(ArbolBossAttack attack) {
		//VINE PORTION
		StartCoroutine(VineAttack(14, true));
		yield return new WaitForSeconds(2.83f);
		vineTrap.PullInPlayer(transform);
		
		//CRUSH PORTION
		animator.SetTrigger(crushAnim);
		yield return new WaitForSeconds(2.5f);
		
		//Refactor PlaySound method to accepts ..SoundEffect[]?
		Sound.PlaySound(crushSound, arbolAudioSource);
		Sound.PlaySound(splashSound, arbolAudioSource);
		splashParticles1.transform.position = splashPoint1.position.Flatten();
		splashParticles2.transform.position = splashPoint2.position.Flatten();
		splashParticles1.Play();
		splashParticles2.Play();
		
		//Damage player if within attack range
		if (Vector3.Distance(transform.position, Player.Instance.transform.position) < attackDistance)
			PlayerHealth.Instance.Damage(attack.attackDamage, true, transform);
		
		yield return new WaitForSeconds(2f);
		attackState = AttackState.Recharging;
	}

	private IEnumerator GoopAttack(ArbolBossAttack attack) {
		animator.SetTrigger(goopAnim);
		yield return new WaitForSeconds(0.83f);
		
		Sound.PlaySound(goopGruntSound, arbolAudioSource);
		
		//Do the attack
		var viableBubbles = thickBubbles.NewCopy();
		float travelTime = 2f, maxHeight = 80f;
		for (int i = 0; i < goopsPerAttack; i++) {
			var bubbles = viableBubbles.Random();
			viableBubbles.Remove(bubbles);

			var goop = Instantiate(goopProjectile, goopThrowPoint.position.Flatten(), Quaternion.identity)
				.GetComponent<LobProjectile>();
			goop.SetUp(attack.attackDamage, null, transform, travelTime, maxHeight);
			goop.GoopSetup(bubbles);

			//Delay between goop throws
			yield return new WaitForSeconds(0.1f);	
		}

		yield return new WaitForSeconds(1f);
		attackState = AttackState.Recharging;
	}
	
	private int minionsActive = 0;
	private IEnumerator MinionSpawn(ArbolBossAttack attack) {
		StartCoroutine(HandleShield());
		animator.SetTrigger(minionSpawnAnim);
		yield return new WaitForSeconds(0.5f);
		
		Sound.PlaySound(minionSpawnRoarSound, arbolAudioSource);

		var viableSpawnPoints = spawnPoints.NewCopy();
		for (int i = 0; i < attack.enemySpawns.Count; i++) {
			var spawnPoint = viableSpawnPoints.Random();
			viableSpawnPoints.Remove(spawnPoint);
			
			var minion = Instantiate(attack.enemySpawns[i], spawnPoint.position, Quaternion.identity).GetComponent<EnemyHealth>();
			minion.GetComponent<EnemyMovement>().detectDistance = 1000f;
			minionsActive++;
			minion.OnDied += () => minionsActive--;

			//Delay between spawns
			yield return new WaitForSeconds(0.5f);
		}

		attackState = AttackState.Recharging;
	}

	private IEnumerator HandleShield() {
		hitbox.enabled = false;
		shieldVisual.SetActive(true);
		riseFeedback.PlayFeedbacks();
		
		yield return new WaitForSeconds(3f);
		
		while (minionsActive > 0) yield return null;
		
		lowerFeedback.PlayFeedbacks();
		yield return new WaitForSeconds(1f);
		
		shieldVisual.SetActive(false);
		hitbox.enabled = true;
	}
	
	private IEnumerator Die() {
		OnBossEncounteredEnded?.Invoke();

		//Face forward and start death animation
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
		
		Camera.main.GetComponent<TopDownCameraFollow>().ToggleBossCamera(false);
		
		var correctedPlinthPosition = new Vector3(transform.position.x + 1.7f, 0f, transform.position.z - 9.4f);
		rewardPlinth.position = correctedPlinthPosition;
		
		barricade.SetActive(false);
		Destroy(gameObject);
	}

	public string GetBossName() => bossName;
	public string GetBossTitle() => bossTitle;
	public Transform GetTransform() => transform;
}
