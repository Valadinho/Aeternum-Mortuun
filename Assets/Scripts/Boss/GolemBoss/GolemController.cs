using UnityEngine;

public class GolemController : MonoBehaviour, IEnemyDataProvider, IMeleeHost
{
    [Header("References")]
    [SerializeField] private Transform player;

    [Header("Stats")]
    [SerializeField] private float detectionRadius = 6f;
    [SerializeField] private float maxSpeed = 2.2f;
    [SerializeField] private float acceleration = 10f;

    [Header("Melee")]
    [SerializeField] private float meleeAttackRange = 1.4f;
    [SerializeField] private float meleeCooldown = 0.9f;
    private float nextMeleeAllowedTime = 0f;

    [Header("Attack script (del prefab)")]
    [SerializeField] private EnemyAttack attack; // asignalé el EnemyAttack del golem

    [Header("Laser Special")]
    [SerializeField] private float laserInitialDelay = 15f;
    [SerializeField] private float laserCooldown = 12f;
    [SerializeField] public float laserRecoverTime = 0f;

    [Header("Laser Prefab")]
    [SerializeField] public GolemBeam laserPrefab;          // arrastrá el prefab acá desde el inspector
    public GolemBeam LaserPrefab => laserPrefab;

    [Header("Laser Tuning")]
    [SerializeField] private float beamDuration = 3f;      // duración visual del rayo
    [SerializeField] private float beamDamage = 1f;      // daño por tick/impacto
    [SerializeField] private float beamMaxRange = 12f;     // alcance máx.
    [SerializeField] private float beamThickness = 0.6f;    // grosor visual/colisión
    [SerializeField] private float beamKnockback = 6f;      // 0 para desactivar empuje
    [SerializeField] private LayerMask beamPlayerMask;       // capa Player
    [SerializeField] private LayerMask beamObstacleMask;     // muros

    [Header("Heavy Attack")]
    [SerializeField] private float heavyRange = 2.6f;         // cuándo puede usarlo
    [SerializeField] private float heavyAirTime = 0.60f;       // tiempo de salto
    [SerializeField] private float heavyArcHeight = 1.5f;      // altura del arco
    [SerializeField] private float heavyDamageRadius = 1.4f;   // AOE al caer
    [SerializeField] private float heavyDamage = 2f;           // daño
    [SerializeField] private float heavyStunTime = 4f;         // stun tras caer
    [SerializeField] private float heavyCooldownMin = 5f;
    [SerializeField] private float heavyCooldownMax = 15f;
    [SerializeField] private LayerMask heavyPlayerMask;

    [Header("Heavy Landing Target")]
    [SerializeField] private GameObject heavyLandingTargetPrefab;

    [Header("Heavy Ranges (advanced)")]
    [SerializeField] private float heavyDecisionRange = 6f; // hasta dónde la IA considera usar Heavy
    [SerializeField] private float heavyJumpMaxRange = 8f; // hasta dónde PUEDE saltar físicamente

    [Header("Heavy Tuning")]
    [SerializeField] private float heavyLeadTime = 0.25f;   // cuánto “adelantas” al jugador (s)
    [SerializeField] private float heavyAirTimeMin = 0.55f; // tiempo de salto para distancias cortas
    [SerializeField] private float heavyAirTimeMax = 0.90f; // tiempo de salto para distancias largas

    [Header("Heavy Prediction")]
    [SerializeField, Range(0f, 1.5f)] private float _heavyLeadTime = 0.35f;   // cuánto predecimos
    [SerializeField, Range(0f, 1f)] private float _heavyPredictBlend = 0.5f;  // 0=sin predicción, 1=solo predicción

    public float HeavyLeadTime => _heavyLeadTime;
    public float HeavyPredictBlend => _heavyPredictBlend;
    public float HeavyAirMin => heavyAirTimeMin;
    public float HeavyAirMax => heavyAirTimeMax;

    // Debounce de transición
    private int lastTransitionFrame = -1;
    private EnemyInputs lastTransitionInput;

    // Lock de Heavy (pendiente/activo)
    private bool heavyEngaged = false;
    public bool IsHeavyEngaged() => heavyEngaged;
    public void SetHeavyEngaged(bool v) => heavyEngaged = v;

    private bool seededFollowTimers = false;
    private float nextHeavyReadyTime = float.PositiveInfinity;
    private GolemHeavyAttackState _heavyRef;
    public float HeavyRange => heavyJumpMaxRange;

    public bool IsHeavying() => fsm.GetCurrentState() is GolemHeavyAttackState;
    public bool IsPlayerInHeavyRange()
    {
        if (!player) return false;
        return Vector2.Distance(transform.position, player.position) <= heavyDecisionRange;
    }
    public bool CanUseHeavy() => Time.time >= nextHeavyReadyTime;
    public void MarkHeavyUsed() =>
        nextHeavyReadyTime = Time.time + Random.Range(heavyCooldownMin, heavyCooldownMax);

    // getters usados por el estado
    public float HeavyAirTime => heavyAirTime;
    public float HeavyArcHeight => heavyArcHeight;
    public float HeavyDamageRadius => heavyDamageRadius;
    public float HeavyDamage => heavyDamage;
    public float HeavyStunTime => heavyStunTime;
    public LayerMask HeavyPlayerMask => heavyPlayerMask;
    public GameObject HeavyLandingTargetPrefab => heavyLandingTargetPrefab;

    // registro + eventos de animación (poné estos métodos en el controller)
    public void RegisterHeavyState(GolemHeavyAttackState s) => _heavyRef = s;
    public void OnHeavyJumpStart() => _heavyRef?.OnJumpStart();   // evento en clip
    public void OnHeavyImpact() => _heavyRef?.OnImpact();      // evento en clip
    public void OnHeavyFinished() => _heavyRef?.OnFinished();    // evento en clip


    private float nextLaserReadyTime;

    // gating
    public bool CanUseLaser() => Time.time >= nextLaserReadyTime;
    public void MarkLaserUsed() => nextLaserReadyTime = Time.time + laserCooldown;
    public bool IsPlayerOutsideMelee() => !IsPlayerInMeleeRange();

    // referencia al estado (para reenviar Animation Events)
    private GolemLaserState _laserRef;
    public void RegisterLaserState(GolemLaserState s) => _laserRef = s;
    public bool IsLasering() => fsm.GetCurrentState() is GolemLaserState;

    // Eventos llamados por la animación (los pondrás en el clip)
    public void OnLaserChargeEnd() => _laserRef?.OnChargeEnd();
    public void OnLaserFinished() => _laserRef?.OnLaserFinished();

    // Ref del estado Melee para reenviar eventos de animación
    private MeleeAttackState _meleeRef;
    public void RegisterMeleeState(MeleeAttackState s) => _meleeRef = s;

    // Animator Events (clip Melee)
    public void OnMeleeHit() => _meleeRef?.OnMeleeHit();
    public void OnMeleeFinished()
    {
        Debug.Log("CTRL (Golem) OnMeleeFinished");
        _meleeRef?.OnMeleeFinished();
    }

    // Helpers de estado
    public bool IsMeleeing() => fsm.GetCurrentState() is MeleeAttackState;
    public bool CanMeleeAttack() => Time.time >= nextMeleeAllowedTime;
    public void MarkMeleeUsed() => nextMeleeAllowedTime = Time.time + meleeCooldown;
    public bool IsPlayerInMeleeRange()
    {
        if (!player) return false;
        return Vector2.Distance(transform.position, player.position) <= meleeAttackRange;
    }

    public bool IsFollowing() => fsm.GetCurrentState() is EnemyFollowState;

    // FSM & comps
    private FSM<EnemyInputs> fsm;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private EnemyHealth health;
    private Rigidbody2D rb;

    private EnemyDeathState _deathRef;
    public void RegisterDeathState(EnemyDeathState s) => _deathRef = s;
    public void OnDeathAnimFinished() => _deathRef?.OnDeathAnimFinished();

    private void Start()
    {
        EnemyManager.Instance.RegisterEnemy();


        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        health = GetComponent<EnemyHealth>();
        rb = GetComponent<Rigidbody2D>();

        if (health != null) health.OnDeath += () => Transition(EnemyInputs.Die);
        if (rb != null) rb.constraints |= RigidbodyConstraints2D.FreezeRotation;

        nextLaserReadyTime = Time.time + laserInitialDelay;

        // Estados base
        var idle = new EnemyIdleState(transform);
        var follow = new EnemyFollowState(transform, player, maxSpeed);
        var melee = new MeleeAttackState(this);
        var laser = new GolemLaserState(this);
        var heavy = new GolemHeavyAttackState(this);
        var death = new EnemyDeathState(this);

        fsm = new FSM<EnemyInputs>(idle);

        // Transiciones
        idle.AddTransition(EnemyInputs.SeePlayer, follow);
        idle.AddTransition(EnemyInputs.Die, death);

        follow.AddTransition(EnemyInputs.LostPlayer, idle);
        follow.AddTransition(EnemyInputs.MeleeAttack, melee);
        follow.AddTransition(EnemyInputs.Die, death);

        melee.AddTransition(EnemyInputs.SeePlayer, follow);

        follow.AddTransition(EnemyInputs.SpecialAttack, laser);
        laser.AddTransition(EnemyInputs.SeePlayer, follow);
        laser.AddTransition(EnemyInputs.Die, death);

        follow.AddTransition(EnemyInputs.HeavyAttack, heavy);
        heavy.AddTransition(EnemyInputs.SeePlayer, follow);
        //heavy.AddTransition(EnemyInputs.Die, death);

        if (animator != null)
        {
            animator.ResetTrigger("Heavy");
            animator.Play("Idle", 0, 0f); // o "Walking" si preferís
        }
    }

    private void FixedUpdate()
    {
        // Tick fijo del FSM (para que EnemyFollowState.FixedExecute corra a paso fijo)
        fsm.FixedUpdate();
    }

    private void Update()
    {
        // Mientras golpea, no meter inputs de ver/seguir
        if (IsMeleeing() || IsLasering() || IsHeavying())
        {

            fsm.Update();
            return;
        }

        fsm.Update();

        // Ver / perder jugador
        float dist = player ? Vector2.Distance(transform.position, player.position) : 999f;
        if (dist <= detectionRadius) Transition(EnemyInputs.SeePlayer);
        else Transition(EnemyInputs.LostPlayer);

        // Anim caminar
        animator.SetBool("isWalking", fsm.GetCurrentState() is EnemyFollowState);

        // Flip
        if (player)
        {
            Vector2 dir = player.position - transform.position;
            spriteRenderer.flipX = dir.x < 0;
        }

        if (IsFollowing() && !seededFollowTimers)
        {
            // Láser a los 5s desde que empezamos a seguir (respetando si ya había uno antes)
            nextLaserReadyTime = Mathf.Max(nextLaserReadyTime, Time.time + 5f);

            // Heavy a los 10s desde que empezamos a seguir
            nextHeavyReadyTime = Time.time + 10f;

            seededFollowTimers = true;
        }

        // Si dejamos de seguir, permitimos resembrar al volver a Follow
        if (!IsFollowing())
        {
            seededFollowTimers = false;
        }
    }


    public void Transition(EnemyInputs input) => fsm.Transition(input);

    // IEnemyDataProvider
    public Transform GetPlayer() => player;
    public float GetDetectionRadius() => detectionRadius;
    public float GetAttackDistance() => detectionRadius;
    public float GetDamage() => 0f;
    public float GetMaxSpeed() => maxSpeed;
    public float GetAcceleration() => acceleration;
    public float GetCurrentHealth() => health ? health.GetCurrentHealth() : 0f;

    // IMeleeHost (para MeleeAttackState)
    public Transform Transform => transform;
    public Animator Animator => animator;
    public Rigidbody2D Body => rb;
    public EnemyAttack Attack => attack;

    // Laser 
    public float BeamDuration => beamDuration;
    public float BeamDmg => beamDamage;
    public float BeamMaxRange => beamMaxRange;
    public float BeamThickness => beamThickness;
    public float BeamKnockback => beamKnockback;
    public LayerMask BeamPlayerMask => beamPlayerMask;
    public LayerMask BeamObstacleMask => beamObstacleMask;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeAttackRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, heavyRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, heavyDamageRadius);
    }
}


