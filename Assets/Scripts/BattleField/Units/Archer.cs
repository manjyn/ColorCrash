using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ColorCrash.Effects;
using ColorCrash.Projectiles;

namespace ColorCrash.Units
{
    public class Archer : RangedUnit
    {
        [Header("Archer Effects")]
        public Transform meleeAttackPoint;

        private bool isMeleeMode = false;
        private float aggroCheckTimer = 0f;
        private const float AGGRO_CHECK_INTERVAL = 0.2f;
        
        private ArcherProjectile cachedProjectilePrefab;

        protected override void Awake()
        {
            base.Awake();
            if (projectilePrefab != null)
            {
                cachedProjectilePrefab = projectilePrefab.GetComponent<ArcherProjectile>();
            }
        }

        protected override IEnumerator SearchTargetRoutine()
        {
            if (!FindTargetForArcher())
            {
                yield return searchTick;
            }
            else
            {
                ChangeState(State.MoveToTarget);
                yield return null;
            }
        }

        protected override IEnumerator MoveToTargetRoutine()
        {
            aggroCheckTimer += Time.deltaTime;
            if (aggroCheckTimer >= AGGRO_CHECK_INTERVAL)
            {
                aggroCheckTimer = 0f;
                
                // 타겟이 죽었거나 사라졌는지 등 상태 점검, 더 좋은 타겟으로 갱신
                FindTargetForArcher(); 
                if (targetTransform == null && !isTargetTile)
                {
                    ChangeState(State.SearchTarget);
                    yield return null;
                    yield break;
                }
            }

            Vector3 currentPos = transform.position;
            Vector3 flatTarget;

            if (isTargetTile)
            {
                flatTarget = new Vector3(targetPosition.x, currentPos.y, targetPosition.z);
            }
            else
            {
                if (targetTransform == null || !targetTransform.gameObject.activeInHierarchy || (targetDamageable != null && targetDamageable.IsDead))
                {
                    ChangeState(State.SearchTarget);
                    yield return null;
                    yield break;
                }
                flatTarget = new Vector3(targetTransform.position.x, currentPos.y, targetTransform.position.z);
            }

            float sqrDist;
            float targetRadiusOffset = 0f;

            if (isTargetTile)
            {
                // 1. 타일 타겟: 사각 경계 Clamping
                float halfTile = GridManager.tileSize * 0.5f;
                Vector3 closestPointOnTile = new Vector3(
                    Mathf.Clamp(currentPos.x, flatTarget.x - halfTile, flatTarget.x + halfTile),
                    currentPos.y,
                    Mathf.Clamp(currentPos.z, flatTarget.z - halfTile, flatTarget.z + halfTile)
                );
                sqrDist = (closestPointOnTile - currentPos).sqrMagnitude;
            }
            else if (targetDamageable is TowerBase targetTower)
            {
                // 2. 건물 타겟: 건물 사각 테두리 표면 Clamping (O(1))
                float halfSize = GridManager.tileSize * 0.5f;
                Vector3 closestPointOnTower = new Vector3(
                    Mathf.Clamp(currentPos.x, flatTarget.x - halfSize, flatTarget.x + halfSize),
                    currentPos.y,
                    Mathf.Clamp(currentPos.z, flatTarget.z - halfSize, flatTarget.z + halfSize)
                );
                sqrDist = (closestPointOnTower - currentPos).sqrMagnitude;
            }
            else
            {
                // 3. 유닛 타겟: 유닛 3D 부피 반경(separationRadius) 오프셋 보정
                sqrDist = (flatTarget - currentPos).sqrMagnitude;
                if (targetDamageable is UnitBase targetUnit)
                {
                    targetRadiusOffset = targetUnit.separationRadius;
                }
            }

            // 모드에 따라 활성화된 사거리 적용
            float currentActiveRange = isMeleeMode ? meleeAttackRange : rangeAttackRange;

            // O(1) 초고속 실효 사거리 및 멈춤 버퍼 연산 (제곱 거리 기반)
            float effectiveRange = currentActiveRange + targetRadiusOffset;
            float sqrEffectiveRange = effectiveRange * effectiveRange;
            float stopRange = effectiveRange * 0.85f;
            float sqrStopRange = stopRange * stopRange;

            if (sqrDist <= sqrEffectiveRange)
            {
                ChangeState(State.Attack);
                SetDesiredMoveDirection(Vector3.zero); // 타겟 표면 진입 완료 시 전진 힘 즉시 차단
            }
            else
            {
                // 이동 목표 지점 선정 (직선 가능 시 flatTarget, 장애물 가로막힘 시 A* 우회 웨이포인트)
                Vector3 moveDestination = flatTarget;

                pathRecalculateTimer += Time.deltaTime;
                bool pathBlocked = IsPathBlocked(currentPos, flatTarget);

                if (pathBlocked)
                {
                    // 정면에 장애물이 막혀 있는 경우: A* Pathfinder 우회 경로 사용
                    if (currentPath == null || currentPath.Count == 0 || pathRecalculateTimer >= PATH_RECALCULATE_INTERVAL)
                    {
                        pathRecalculateTimer = 0f;
                        UpdatePath(flatTarget);
                    }

                    if (currentPath != null && currentPath.Count > 0 && currentPathIndex < currentPath.Count)
                    {
                        Vector3 currentWaypoint = currentPath[currentPathIndex];
                        Vector3 flatWaypoint = new Vector3(currentWaypoint.x, currentPos.y, currentWaypoint.z);

                        float sqrDistToWaypoint = (flatWaypoint - currentPos).sqrMagnitude;
                        if (sqrDistToWaypoint < 1.0f)
                        {
                            currentPathIndex++;
                            if (currentPathIndex < currentPath.Count)
                            {
                                flatWaypoint = new Vector3(currentPath[currentPathIndex].x, currentPos.y, currentPath[currentPathIndex].z);
                            }
                        }

                        moveDestination = flatWaypoint;
                    }
                }
                else
                {
                    // 직선 시야 확보 시 우회 경로 초기화
                    if (currentPath != null && currentPath.Count > 0)
                    {
                        currentPath.Clear();
                        currentPathIndex = 0;
                    }
                }

                // 이동 방향 바라보기 및 통합 이동 벡터 설정 (멈춤 버퍼 존 적용)
                if (moveDestination != currentPos)
                {
                    Vector3 moveDir = moveDestination - currentPos;
                    moveDir.y = 0;
                    if (moveDir != Vector3.zero)
                    {
                        transform.rotation = Quaternion.LookRotation(moveDir);

                        // [O(1) 멈춤 버퍼] 사거리 85% 이내 진입 시 중심점 파고드는 전진력 0으로 설정
                        if (sqrDist <= sqrStopRange)
                        {
                            SetDesiredMoveDirection(Vector3.zero);
                        }
                        else
                        {
                            SetDesiredMoveDirection(moveDir);
                        }
                    }
                    else
                    {
                        SetDesiredMoveDirection(Vector3.zero);
                    }
                }
                else
                {
                    SetDesiredMoveDirection(Vector3.zero);
                }
            }
            yield return null;
        }

        public override void OnHit()
        {
            base.OnHit();

            if (isMeleeMode)
            {
                // [근접 모드]
                if (isTargetTile)
                {
                    if (GridManager.Instance != null)
                    {
                        int targetIdx = GridManager.Instance.WorldToIndex(targetPosition);
                        if (targetIdx != -1 && GridManager.Instance.tileStates[targetIdx] == TeamColor.Obstacle)
                        {
                            ChangeState(State.SearchTarget);
                            return;
                        }
                    }

                    currentTileHitCount++;

                    if (EffectManager.Instance != null)
                    {
                        Vector3 spawnPos = (meleeAttackPoint != null) ? meleeAttackPoint.position : transform.position;
                        Quaternion spawnRot = (meleeAttackPoint != null) ? meleeAttackPoint.rotation : transform.rotation;
                        EffectManager.Instance.PlayEffect(EffectType.MeleeSlash, spawnPos, spawnRot, Color.white);
                    }

                    if (currentTileHitCount >= requiredTileHits)
                    {
                        if (GridManager.Instance != null)
                        {
                            GridManager.Instance.PaintTile(targetPosition, teamColor);
                        }
                        currentTileHitCount = 0;
                        ChangeState(State.SearchTarget);
                    }
                }
                else
                {
                    if (targetTransform != null && targetDamageable != null && !targetDamageable.IsDead)
                    {
                        targetDamageable.TakeDamage(meleeAttackDamage);
                    }

                    if (EffectManager.Instance != null)
                    {
                        Vector3 spawnPos = (meleeAttackPoint != null) ? meleeAttackPoint.position : transform.position;
                        Quaternion spawnRot = (meleeAttackPoint != null) ? meleeAttackPoint.rotation : transform.rotation;
                        EffectManager.Instance.PlayEffect(EffectType.MeleeSlash, spawnPos, spawnRot, Color.white);
                    }
                }
            }
            else
            {
                // [원거리 모드]
                if (EffectManager.Instance != null && firePoint != null)
                {
                    Color effectColor = (teamColor == TeamColor.Blue) ? Color.blue : Color.red;
                    EffectManager.Instance.PlayEffect(EffectType.Muzzle, firePoint.position, effectColor);
                }

                if (projectilePrefab != null && firePoint != null)
                {
                    ArcherProjectile proj = null;
                    if (cachedProjectilePrefab != null && ProjectileManager.Instance != null)
                    {
                        proj = ProjectileManager.Instance.SpawnProjectile(cachedProjectilePrefab, firePoint.position) as ArcherProjectile;
                        if (proj != null)
                            proj.transform.rotation = firePoint.rotation;
                    }
                    else
                    {
                        GameObject projObj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
                        proj = projObj.GetComponent<ArcherProjectile>();
                    }

                    if (proj != null && targetTransform != null)
                        proj.Initialize(targetDamageable, targetTransform.position, rangeAttackDamage, teamColor);
                }
            }
        }

        protected override IEnumerator AttackRoutine()
        {
            while (currentState == State.Attack)
            {
                PlayAttackAnimation();

                if (isMeleeMode)
                {
                    yield return attackCooldownTick;
                }
                else
                {
                    yield return rangeAttackCooldownTick;
                }

                if (currentState != State.Attack) yield break;

                if (!isTargetTile && (targetTransform == null || !targetTransform.gameObject.activeInHierarchy || targetDamageable == null || targetDamageable.IsDead))
                {
                    ChangeState(State.SearchTarget);
                    yield break;
                }

                if (isTargetTile && GridManager.Instance != null)
                {
                    int targetIdx = GridManager.Instance.WorldToIndex(targetPosition);
                    if (targetIdx != -1 && (GridManager.Instance.tileStates[targetIdx] == teamColor || GridManager.Instance.tileStates[targetIdx] == TeamColor.Obstacle))
                    {
                        ChangeState(State.SearchTarget);
                        yield break;
                    }
                }
            }
        }

        /// <summary>
        /// Archer 전용 개편된 타겟 탐색 로직
        /// 1순위: 가장 가까운 적 유닛 또는 적 건물 (무조건 원거리 사격 모드)
        /// 2순위: 전장에 적 유닛/건물이 없는 경우 가장 가까운 적/중립 타일 (근접 타일 점령 모드)
        /// </summary>
        protected bool FindTargetForArcher()
        {
            Vector3 currentPos = transform.position;

            float closestUnitDist = float.MaxValue;
            Transform closestUnitT = null;
            IDamageable closestUnitD = null;

            // 1. 적 유닛 탐색
            if (UnitManager.Instance != null)
            {
                List<UnitBase> enemyUnits = (enemyColor == TeamColor.Blue) ? UnitManager.Instance.activeBlueUnits : UnitManager.Instance.activeRedUnits;
                if (enemyUnits != null)
                {
                    for (int i = 0; i < enemyUnits.Count; i++)
                    {
                        var u = enemyUnits[i];
                        if (u == null || !u.gameObject.activeInHierarchy || u.IsDead) continue;
                        
                        float sqrDist = GetSqrDist(u.transform.position, currentPos);
                        if (sqrDist < closestUnitDist)
                        {
                            closestUnitDist = sqrDist;
                            closestUnitT = u.transform;
                            closestUnitD = u;
                        }
                    }
                }
            }

            // 2. 적 건물 탐색
            TowerBase[] buildings = null;
            if (GridManager.Instance != null && GridManager.Instance.gridBuildings != null)
            {
                buildings = GridManager.Instance.gridBuildings;
            }
            else
            {
                buildings = FindObjectsByType<TowerBase>(FindObjectsSortMode.None);
            }

            if (buildings != null)
            {
                HashSet<TowerBase> visited = new HashSet<TowerBase>();
                for (int i = 0; i < buildings.Length; i++)
                {
                    var b = buildings[i];
                    if (b == null || b.TeamColor != enemyColor || b.IsDead || visited.Contains(b)) continue;
                    visited.Add(b);

                    float sqrDist = GetSqrDist(b.transform.position, currentPos);
                    if (sqrDist < closestUnitDist)
                    {
                        closestUnitDist = sqrDist;
                        closestUnitT = b.transform;
                        closestUnitD = b;
                    }
                }
            }

            isTargetTile = false;
            targetTransform = null;
            targetDamageable = null;

            // 🌟 1순위: 전장에 적 유닛 또는 적 건물이 1개라도 존재하는 경우
            if (closestUnitT != null)
            {
                targetTransform = closestUnitT;
                targetDamageable = closestUnitD;
                isMeleeMode = false; // 적 공격 시 무조건 원거리 사격 모드
                return true;
            }

            // 🌟 2순위: 전장에 적 유닛 및 적 건물이 0개일 때만 점령 가능 타일 탐색
            if (GridManager.Instance != null)
            {
                float closestTileDist = float.MaxValue;
                Vector3? closestTilePos = null;

                List<int> enemyTileIndices = (enemyColor == TeamColor.Blue) ? GridManager.Instance.blueTileIndices : GridManager.Instance.redTileIndices;
                List<int> neutralTileIndices = GridManager.Instance.neutralTileIndices;

                for (int pass = 0; pass < 2; pass++)
                {
                    List<int> targetIndices = (pass == 0) ? enemyTileIndices : neutralTileIndices;
                    if (targetIndices == null) continue;

                    for (int i = 0; i < targetIndices.Count; i++)
                    {
                        int index = targetIndices[i];
                        if (GridManager.Instance.gridBuildings[index] != null || GridManager.Instance.tileStates[index] == TeamColor.Obstacle) continue;

                        Vector3 realPos = GridManager.Instance.IndexToWorld(index);
                        float sqrDist = GetSqrDist(realPos, currentPos);
                        if (sqrDist < closestTileDist)
                        {
                            closestTileDist = sqrDist;
                            closestTilePos = realPos;
                        }
                    }
                }

                if (closestTilePos.HasValue)
                {
                    targetPosition = closestTilePos.Value;
                    isTargetTile = true;
                    isMeleeMode = true; // 타일 점령 시에만 근접 모드 사용
                    return true;
                }
            }

            return false;
        }

        private float GetSqrDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return (dx * dx) + (dz * dz);
        }
    }
}
