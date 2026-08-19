using System.Collections;
using UnityEngine;
using ColorCrash.UI;
using ColorCrash.Effects;

namespace ColorCrash.Units
{
    public abstract class UnitBase : MonoBehaviour, IDamageable
    {
        public enum State
        {
            SearchTarget,
            MoveToTarget,
            Attack,
            Knockback
        }

        protected const float ATTACK_ANIM_DELAY = 0.3f;

        [Header("Unit Database Settings")]
        [SerializeField] protected string unitId;
                
        [Header("Team Material Settings")]
        [SerializeField] protected Material blueTeamMaterial;
        [SerializeField] protected Material redTeamMaterial;

        protected Renderer[] cachedRenderers;

        [Header("UI Reference")]
        [SerializeField] protected HealthBarUI healthBarUI;

        [Header("UI Bottom Mark")]
        public MeshRenderer BottomMarkRenderer;

        [Header("Base Targeting Settings")]
        public LayerMask enemyUnitMask;
        public LayerMask enemyBuildingMask;

        public string UnitId => unitId;

        [HideInInspector] public TeamColor teamColor = TeamColor.Blue;
        [HideInInspector] public TeamColor enemyColor;
        [HideInInspector] public int currentTileIndex = -1;

        [HideInInspector] public float maxHp = 100f;
        protected float currentHp;
        [HideInInspector] public float defense = 0f;
        [HideInInspector] public float moveSpeed = 10f;
        [HideInInspector] public float meleeAttackDamage = 10f;
        [HideInInspector] public float meleeAttackRange = 2f;
        [HideInInspector] public float meleeAttackCooldown = 1f;
        [HideInInspector] public float rangeAttackDamage = 10f;
        [HideInInspector] public float rangeAttackRange = 10f;
        [HideInInspector] public float rangeAttackCooldown = 1f;
        [HideInInspector] public float searchRadius = 20f;
        [HideInInspector] public float separationRadius = 1.0f;
        [HideInInspector] public float separationForce = 100.0f; // 겹친 유닛 밀어내는 힘 (Steering Force)
        [HideInInspector] public float tileCaptureDelay = 0.3f;

        [Header("Tile Capture Settings")]
        [SerializeField] protected int requiredTileHits = 1;
        protected int currentTileHitCount = 0;

        public float TileCaptureDelay => tileCaptureDelay;
        public int RequiredTileHits => requiredTileHits;

        protected State currentState;
        protected Vector3 targetPosition;
        protected Transform targetTransform;
        protected IDamageable targetDamageable;
        protected bool isTargetTile = false;
        protected bool isInitialized = false;

        // 조향 이동 벡터 (Steering Movement Vector)
        protected Vector3 desiredMoveDirection = Vector3.zero;

        // 장애물 우회 길찾기 (Pathfinder Waypoints) 필드
        protected System.Collections.Generic.List<Vector3> currentPath = new System.Collections.Generic.List<Vector3>();
        protected int currentPathIndex = 0;
        protected float pathRecalculateTimer = 0f;
        protected const float PATH_RECALCULATE_INTERVAL = 0.5f;

        // GC 방지를 위한 캐싱
        protected Collider[] hitColliders = new Collider[10];
        protected WaitForSeconds searchTick;
        protected WaitForSeconds attackAnimDelayTick;
        protected WaitForSeconds attackCooldownTick;
        protected WaitForSeconds rangeAttackCooldownTick;
        protected Material bottomMarkMaterialInstance;
        
        protected static readonly int IsAttackingHash = Animator.StringToHash("isAttacking");

        protected Animator mainAnimator;
        protected UnitDataShatter dataShatter;

        #region Animation Control
        /// <summary>
        /// 유닛의 공격 애니메이션 파라미터(isAttacking)를 설정합니다.
        /// </summary>
        public void SetAttackAnimation(bool isAttacking)
        {
            if (mainAnimator != null)
            {
                mainAnimator.SetBool(IsAttackingHash, isAttacking);
                if (isAttacking)
                {
                    mainAnimator.Play(0, 0, 0f);
                }
            }
        }

        public void PlayAttackAnimation() => SetAttackAnimation(true);
        public void StopAttackAnimation() => SetAttackAnimation(false);
        #endregion

        #region FSM State Management
        public void ChangeState(State newState)
        {
            if (currentState == newState) return;

            OnStateExit(currentState);
            currentState = newState;
            OnStateEnter(currentState);
        }

        protected virtual void OnStateEnter(State state)
        {
            switch (state)
            {
                case State.Attack:
                    SetAttackAnimation(true);
                    break;
            }
        }

        protected virtual void OnStateExit(State state)
        {
            switch (state)
            {
                case State.Attack:
                    SetAttackAnimation(false);
                    break;
            }
        }

        /// <summary>
        /// FBX Animation Event(OnHit)에 의해 타격 프레임에서 호출되는 가상 메소드
        /// </summary>
        public virtual void OnHit()
        {
        }
        #endregion

                
        public TeamColor TeamColor => teamColor;
        public bool IsDead => currentHp <= 0f;
        public float MaxHp => maxHp;
        public float CurrentHp => currentHp;
        public event System.Action<float, float> OnHealthChanged;

        protected virtual void Awake()
        {
            searchTick = new WaitForSeconds(0.2f);
            attackAnimDelayTick = new WaitForSeconds(ATTACK_ANIM_DELAY);
            attackCooldownTick = new WaitForSeconds(Mathf.Max(0f, meleeAttackCooldown - ATTACK_ANIM_DELAY));
            rangeAttackCooldownTick = new WaitForSeconds(Mathf.Max(0f, rangeAttackCooldown - ATTACK_ANIM_DELAY));

            mainAnimator = GetComponentInChildren<Animator>();
            dataShatter = GetComponent<UnitDataShatter>();
            cachedRenderers = GetComponentsInChildren<Renderer>(true);

            if (mainAnimator != null && !mainAnimator.TryGetComponent<UnitAnimationRelay>(out _))
            {
                mainAnimator.gameObject.AddComponent<UnitAnimationRelay>();
            }
        }

        public virtual void Initialize(TeamColor team)
        {
            teamColor = team;
            enemyColor = (teamColor == TeamColor.Blue) ? TeamColor.Red : TeamColor.Blue;

            if (UnitManager.Instance != null && UnitManager.Instance.UnitDatabase != null)
            {
                if (UnitManager.Instance.UnitDatabase.TryGetRecord(unitId, out var record))
                {
                    maxHp = record.MaxHp;
                    defense = record.Defense;
                    meleeAttackDamage = record.MeleeAttackDamage;
                    rangeAttackDamage = record.RangeAttackDamage;
                    moveSpeed = record.MoveSpeed;
                    meleeAttackRange = record.MeleeAttackRange;
                    meleeAttackCooldown = record.MeleeAttackCooldown;
                    rangeAttackRange = record.RangeAttackRange;
                    rangeAttackCooldown = record.RangeAttackCooldown;
                    searchRadius = record.SearchRadius;
                    separationRadius = record.SeparationRadius;
                    tileCaptureDelay = record.TileCaptureDelay;
                    requiredTileHits = record.RequiredTileHits;
                }
                else
                {
                    Debug.LogWarning($"[UnitBase] UnitDatabase에서 ID '{unitId}'를 찾을 수 없습니다. (오브젝트: {gameObject.name})");
                }
            }

            currentHp = maxHp;
            OnHealthChanged?.Invoke(currentHp, maxHp);
            
            if (healthBarUI != null)
            {
                healthBarUI.Setup(this, transform);
            }
            
            // 캐싱된 YieldInstruction 업데이트
            attackCooldownTick = new WaitForSeconds(Mathf.Max(0f, meleeAttackCooldown - ATTACK_ANIM_DELAY));
            rangeAttackCooldownTick = new WaitForSeconds(Mathf.Max(0f, rangeAttackCooldown - ATTACK_ANIM_DELAY));
            
            isInitialized = true;

            // 이미 활성화된 상태에서 호출되었다면 로직 바로 시작
            if (gameObject.activeInHierarchy)
            {
                ApplyTeamMaterial();
                ApplyBottomMarkColor();
                StartCoroutine(FSM_Loop());
            }
        }

        protected virtual void OnEnable()
        {
            currentState = State.SearchTarget;
            targetTransform = null;
            targetDamageable = null;
            isTargetTile = false;
            ResetMovementState();
            StopAttackAnimation();

            if (mainAnimator != null)
            {
                mainAnimator.enabled = true;
            }

            if (!isInitialized) return;
            
            ApplyTeamMaterial();
            ApplyBottomMarkColor();
            StartCoroutine(FSM_Loop());
        }

        public void SetDesiredMoveDirection(Vector3 dir)
        {
            desiredMoveDirection = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.zero;
        }

        public void ResetMovementState()
        {
            desiredMoveDirection = Vector3.zero;
            if (currentPath != null)
            {
                currentPath.Clear();
            }
            currentPathIndex = 0;
            pathRecalculateTimer = 0f;
        }

        public virtual void TakeDamage(float amount)
        {
            if (IsDead) return;

            float actualDamage = Mathf.Max(1f, amount - defense);
            currentHp -= actualDamage;

            if (currentHp <= 0f)
            {
                currentHp = 0f;
            }

            OnHealthChanged?.Invoke(currentHp, maxHp);

            if (currentHp <= 0f)
            {
                OnDeath();
            }
        }

        protected virtual void OnDeath()
        {
            // 모든 동작 및 애니메이션 정지
            StopAllCoroutines();
            ResetMovementState();
            StopAttackAnimation();

            if (mainAnimator != null)
            {
                mainAnimator.enabled = false;
            }

            // 파편화 연출 컴포넌트가 있다면 실행하고, 연출 종료 후 Die
            if (dataShatter != null)
            {
                dataShatter.ExecuteShatter(Die);
            }
            else
            {
                // 파편화 연출이 없으면 즉시 Die
                Die();
            }
        }

        protected virtual void Die()
        {
            Color effectColor = (teamColor == TeamColor.Blue) ? Color.blue : Color.red;
            if (EffectManager.Instance != null)
            {
                EffectManager.Instance.PlayEffect(EffectType.UnitDestroy, transform.position + Vector3.up * 10f, effectColor);
            }

            if (UnitManager.Instance != null)
            {
                UnitManager.Instance.DespawnUnit(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        protected void ApplyBottomMarkColor()
        {
            if (bottomMarkMaterialInstance == null)
            {
                if (BottomMarkRenderer != null)
                {
                    foreach (var mat in BottomMarkRenderer.materials)
                    {
                        if (mat.name.Contains("unit_bottom_mark"))
                        {
                            bottomMarkMaterialInstance = mat;
                            break;
                        }
                    }
                }
            }

            if (bottomMarkMaterialInstance != null)
            {
                Color colorToApply = (teamColor == TeamColor.Blue) ? Color.blue : Color.red;
                if (bottomMarkMaterialInstance.HasProperty("_BaseColor"))
                {
                    bottomMarkMaterialInstance.SetColor("_BaseColor", colorToApply);
                }
                else if (bottomMarkMaterialInstance.HasProperty("_Color"))
                {
                    bottomMarkMaterialInstance.SetColor("_Color", colorToApply);
                }
            }
        }

        /// <summary>
        /// 팀(TeamColor)에 맞게 캐싱된 자식 렌더러 파츠들의 sharedMaterial을 일괄 교체합니다. (GC Alloc 0)
        /// </summary>
        protected void ApplyTeamMaterial()
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0) return;

            Material targetMaterial = (teamColor == TeamColor.Blue) ? blueTeamMaterial : redTeamMaterial;
            if (targetMaterial == null) return;

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                Renderer rend = cachedRenderers[i];
                if (rend != null && rend != BottomMarkRenderer)
                {
                    rend.sharedMaterial = targetMaterial;
                }
            }
        }

        public virtual void ApplyKnockback(Vector3 direction, float force, float duration)
        {
            if (IsDead) return;
            
            StopAllCoroutines();
            ResetMovementState();
            ChangeState(State.Knockback);

            Vector3 knockbackVector = direction.normalized * force;
            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos + knockbackVector;

            // 넉백 궤적에 장애물 타일이 포함되어 있는지 사전 검사 및 TargetPos 클램핑
            if (GridManager.Instance != null)
            {
                int steps = 10;
                for (int i = 1; i <= steps; i++)
                {
                    Vector3 samplePos = Vector3.Lerp(startPos, targetPos, (float)i / steps);
                    int sampleIndex = GridManager.Instance.WorldToIndex(samplePos);
                    if (sampleIndex != -1 && GridManager.Instance.tileStates[sampleIndex] == TeamColor.Obstacle)
                    {
                        // 장애물 진입 직전 샘플 위치로 targetPos 제한
                        targetPos = Vector3.Lerp(startPos, targetPos, (float)(i - 1) / steps);
                        break;
                    }
                }
            }

            StartCoroutine(KnockbackRoutine(targetPos, duration));
        }

        protected virtual IEnumerator KnockbackRoutine(Vector3 targetPos, float duration)
        {
            float elapsed = 0f;
            Vector3 startPos = transform.position;

            while (elapsed < duration)
            {
                if (IsDead) yield break;
                
                transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
                ApplyObstacleRepulsion(); // 넉백 이동 중에도 실시간 장애물 반발력 튕겨내기 개입
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!IsDead)
            {
                transform.position = targetPos;
                ApplyObstacleRepulsion();
                ChangeState(State.SearchTarget);
                StartCoroutine(FSM_Loop());
            }
        }

        private IEnumerator FSM_Loop()
        {
            while (true)
            {
                switch (currentState)
                {
                    case State.SearchTarget:
                        yield return StartCoroutine(SearchTargetRoutine());
                        break;
                    case State.MoveToTarget:
                        yield return StartCoroutine(MoveToTargetRoutine());
                        break;
                    case State.Attack:
                        yield return StartCoroutine(AttackRoutine());
                        break;
                    case State.Knockback:
                        yield return null;
                        break;
                }
            }
        }

        protected virtual void Update()
        {
            if (IsDead) return;

            // 1. MoveToTarget 상태가 아닐 때는 목표 이동 방향 0으로 리셋
            if (currentState != State.MoveToTarget)
            {
                desiredMoveDirection = Vector3.zero;
            }

            Vector3 moveVelocity = desiredMoveDirection * moveSpeed;

            // 2. 주변 3x3 타일 유닛 밀어내기 벡터 (이동 중일 때는 50% 감쇄하여 이동성 보장)
            float separationWeight = (currentState == State.MoveToTarget) ? 0.5f : 1.0f;
            Vector3 separationVelocity = GetSeparationVector() * (separationForce * separationWeight);

            // 3. 장애물 반발 벡터
            Vector3 obstacleVelocity = GetObstacleRepulsionVector() * 15.0f;

            // 4. 최종 이동 벡터 합산
            Vector3 finalVelocity = moveVelocity + separationVelocity + obstacleVelocity;

            // [단점 보완 1] 속도 튀어남 방지 (최대 속도 Clamp: moveSpeed의 1.5배 또는 기본 15.0f)
            float maxAllowedSpeed = Mathf.Max(moveSpeed * 1.5f, 15.0f);
            finalVelocity = Vector3.ClampMagnitude(finalVelocity, maxAllowedSpeed);

            // [단점 보완 2] 떨림 방지 데드존 (미세 이동 무시)
            if (finalVelocity.sqrMagnitude < 0.001f)
            {
                finalVelocity = Vector3.zero;
            }

            // 5. [단점 보완 3] 벽 관통 방지 (장애물 타일 체크)
            Vector3 nextPos = transform.position + finalVelocity * Time.deltaTime;

            if (GridManager.Instance != null)
            {
                int nextIndex = GridManager.Instance.WorldToIndex(nextPos);
                if (nextIndex != -1 && GridManager.Instance.isObstacleTile != null && GridManager.Instance.isObstacleTile[nextIndex])
                {
                    // 밀어내기/반발로 장애물 타일을 뚫고 들어가지 않도록 기본 목표 이동 위치만 적용
                    nextPos = transform.position + moveVelocity * Time.deltaTime;
                }

                // 6. 단 1회 위치 적용 및 타일 업데이트
                transform.position = nextPos;

                int newTileIndex = GridManager.Instance.WorldToIndex(transform.position);
                if (newTileIndex != currentTileIndex)
                {
                    GridManager.Instance.UpdateUnitTile(this, currentTileIndex, newTileIndex);
                    currentTileIndex = newTileIndex;
                }
            }
            else
            {
                transform.position = nextPos;
            }
        }

        /// <summary>
        /// 인접 3x3 타일(8방향 포함) 내의 유닛들과의 거리를 계산하여 서로를 밀쳐내는 분리(Separation) 벡터를 반환합니다.
        /// </summary>
        protected virtual Vector3 GetSeparationVector()
        {
            if (GridManager.Instance == null) return Vector3.zero;

            int myIndex = currentTileIndex;
            if (myIndex == -1)
            {
                myIndex = GridManager.Instance.WorldToIndex(transform.position);
                if (myIndex == -1) return Vector3.zero;
            }

            int width = GridManager.Instance.width;
            int height = GridManager.Instance.height;

            int cX = myIndex % width;
            int cZ = myIndex / width;

            Vector3 pushVector = Vector3.zero;
            float sqrSeparationRadius = separationRadius * separationRadius;
            Vector3 currentPos = transform.position;

            for (int z = cZ - 1; z <= cZ + 1; z++)
            {
                for (int x = cX - 1; x <= cX + 1; x++)
                {
                    if (x < 0 || x >= width || z < 0 || z >= height) continue;

                    int tileIdx = z * width + x;
                    var unitsInTile = GridManager.Instance.GetUnitsInTile(tileIdx);
                    if (unitsInTile == null || unitsInTile.Count == 0) continue;

                    for (int i = 0; i < unitsInTile.Count; i++)
                    {
                        UnitBase otherUnit = unitsInTile[i];
                        if (otherUnit == null || otherUnit == this || otherUnit.IsDead || !otherUnit.gameObject.activeInHierarchy) continue;

                        Vector3 direction = currentPos - otherUnit.transform.position;
                        direction.y = 0;

                        float sqrDist = direction.sqrMagnitude;

                        if (sqrDist == 0f)
                        {
                            direction = new Vector3(Random.Range(-0.1f, 0.1f), 0, Random.Range(-0.1f, 0.1f));
                            sqrDist = 0.0001f;
                        }

                        if (sqrDist < sqrSeparationRadius)
                        {
                            float dist = Mathf.Sqrt(sqrDist);
                            float pushWeight = 1.0f - (dist / separationRadius);
                            pushVector += (direction / dist) * pushWeight;
                        }
                    }
                }
            }

            return pushVector;
        }

        protected virtual void ApplySeparation()
        {
            // 하위 호환성을 위한 래퍼 (실제 연산은 Update 내 GetSeparationVector에서 수행)
        }

        /// <summary>
        /// 장애물 타일(Obstacle)과의 거리를 계산하여 유닛을 영역 밖으로 밀어내는 반발(Repulsion) 벡터를 반환합니다.
        /// </summary>
        protected virtual Vector3 GetObstacleRepulsionVector()
        {
            if (GridManager.Instance == null || GridManager.Instance.isNearObstacleTile == null) return Vector3.zero;

            int myIndex = currentTileIndex;
            if (myIndex == -1)
            {
                myIndex = GridManager.Instance.WorldToIndex(transform.position);
                if (myIndex == -1) return Vector3.zero;
            }

            if (!GridManager.Instance.isNearObstacleTile[myIndex]) return Vector3.zero;

            Vector3 currentPos = transform.position;

            // 예외 보정: 유닛 중심점이 이미 장애물 타일 내부에 완전히 파묻힌 경우 즉시 탈출
            if (GridManager.Instance.isObstacleTile != null && GridManager.Instance.isObstacleTile[myIndex])
            {
                Vector3 escapePos = GetNearestValidTilePosition(currentPos);
                transform.position = escapePos;
                return Vector3.zero;
            }

            int width = GridManager.Instance.width;
            int height = GridManager.Instance.height;
            float tileSize = GridManager.tileSize;

            int cX = myIndex % width;
            int cZ = myIndex / width;

            Vector3 pushVector = Vector3.zero;
            float repulsionRadius = tileSize * 0.75f;
            float sqrRepulsionRadius = repulsionRadius * repulsionRadius;

            for (int z = cZ - 1; z <= cZ + 1; z++)
            {
                for (int x = cX - 1; x <= cX + 1; x++)
                {
                    if (x < 0 || x >= width || z < 0 || z >= height) continue;

                    int checkIndex = z * width + x;
                    if (GridManager.Instance.isObstacleTile[checkIndex])
                    {
                        Vector3 obstaclePos = GridManager.Instance.IndexToWorld(checkIndex);
                        Vector3 direction = currentPos - obstaclePos;
                        direction.y = 0;

                        float sqrDist = direction.sqrMagnitude;
                        if (sqrDist < sqrRepulsionRadius)
                        {
                            float dist = Mathf.Sqrt(sqrDist);
                            if (dist < 0.001f)
                            {
                                direction = new Vector3(Random.Range(-0.1f, 0.1f), 0, Random.Range(-0.1f, 0.1f));
                                dist = 0.001f;
                            }

                            float pushWeight = 1.0f - (dist / repulsionRadius);
                            pushVector += (direction / dist) * pushWeight;
                        }
                    }
                }
            }

            return pushVector;
        }

        protected virtual void ApplyObstacleRepulsion()
        {
            // 하위 호환성을 위한 래퍼 (실제 연산은 Update 내 GetObstacleRepulsionVector에서 수행)
        }

        /// <summary>
        /// 유닛이 장애물 내부로 파묻혔을 때 가장 가까운 가용(통행 가능) 타일의 월드 좌표를 검색합니다.
        /// </summary>
        protected Vector3 GetNearestValidTilePosition(Vector3 currentPos)
        {
            if (GridManager.Instance == null) return currentPos;

            int index = GridManager.Instance.WorldToIndex(currentPos);
            if (index == -1) return currentPos;

            int width = GridManager.Instance.width;
            int height = GridManager.Instance.height;
            int cX = index % width;
            int cZ = index / width;

            float minSqrDist = float.MaxValue;
            Vector3 bestPos = currentPos;

            for (int r = 1; r <= 3; r++)
            {
                for (int z = cZ - r; z <= cZ + r; z++)
                {
                    for (int x = cX - r; x <= cX + r; x++)
                    {
                        if (x < 0 || x >= width || z < 0 || z >= height) continue;
                        int checkIndex = z * width + x;

                        bool isObstacle = (GridManager.Instance.isObstacleTile != null) 
                            ? GridManager.Instance.isObstacleTile[checkIndex] 
                            : (GridManager.Instance.tileStates[checkIndex] == TeamColor.Obstacle);

                        if (!isObstacle)
                        {
                            Vector3 tileWorldPos = GridManager.Instance.IndexToWorld(checkIndex);
                            float sqrDist = (tileWorldPos - currentPos).sqrMagnitude;
                            if (sqrDist < minSqrDist)
                            {
                                minSqrDist = sqrDist;
                                bestPos = tileWorldPos;
                            }
                        }
                    }
                }
                if (minSqrDist < float.MaxValue) break;
            }

            return bestPos;
        }

        /// <summary>
        /// 시작점에서 목표점까지의 직선 궤적 상에 장애물(Obstacle)이나 건물(Building)이 가로막혀 있는지 샘플링 검사합니다.
        /// </summary>
        protected bool IsPathBlocked(Vector3 startPos, Vector3 targetPos)
        {
            if (GridManager.Instance == null) return false;

            int startIdx = GridManager.Instance.WorldToIndex(startPos);
            int targetIdx = GridManager.Instance.WorldToIndex(targetPos);

            if (startIdx == -1 || targetIdx == -1) return false;
            if (startIdx == targetIdx) return false;

            float dist = (targetPos - startPos).magnitude;
            int steps = Mathf.Max(2, Mathf.CeilToInt(dist / (GridManager.tileSize * 0.5f)));

            for (int i = 1; i <= steps; i++)
            {
                Vector3 sample = Vector3.Lerp(startPos, targetPos, (float)i / steps);
                int sampleIdx = GridManager.Instance.WorldToIndex(sample);
                if (sampleIdx != -1)
                {
                    bool isObstacle = (GridManager.Instance.isObstacleTile != null) 
                        ? GridManager.Instance.isObstacleTile[sampleIdx] 
                        : (GridManager.Instance.tileStates[sampleIdx] == TeamColor.Obstacle);

                    if (isObstacle || GridManager.Instance.gridBuildings[sampleIdx] != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Pathfinder를 호출하여 장애물을 우회하는 A* 길찾기 웨이포인트(Path) 경로를 갱신합니다.
        /// </summary>
        protected void UpdatePath(Vector3 targetPos)
        {
            if (Pathfinder.Instance == null)
            {
                currentPath.Clear();
                currentPathIndex = 0;
                return;
            }

            currentPath = Pathfinder.Instance.FindPath(transform.position, targetPos);
            currentPathIndex = 0;

            if (currentPath != null && currentPath.Count > 1)
            {
                float sqrDistToFirst = (currentPath[0] - transform.position).sqrMagnitude;
                if (sqrDistToFirst < (GridManager.tileSize * 0.5f) * (GridManager.tileSize * 0.5f))
                {
                    currentPathIndex = 1;
                }
            }
        }

        // 하위 클래스에서 구체적으로 구현할 상태별 동작
        protected abstract IEnumerator SearchTargetRoutine();
        protected abstract IEnumerator MoveToTargetRoutine();
        protected abstract IEnumerator AttackRoutine();
    }
}