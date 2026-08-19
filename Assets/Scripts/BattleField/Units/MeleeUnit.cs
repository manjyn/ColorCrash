using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ColorCrash.Effects;

namespace ColorCrash.Units
{
    public class MeleeUnit : UnitBase
    {
        [Header("Effect References")]
        [SerializeField] protected Transform attackPoint; // 공격 이펙트 발생 위치

        private float aggroCheckTimer = 0f;
        private const float AGGRO_CHECK_INTERVAL = 0.2f;

        protected override IEnumerator SearchTargetRoutine()
        {
            if (!FindTargetOptimized())
            {
                // 타겟이 없으면 일정 시간(Tick) 후 재탐색
                yield return searchTick;
            }
            else
            {
                // 타겟을 찾으면 즉시 이동 상태로 전환
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
                bool isTargetingUnit = (!isTargetTile && targetDamageable != null && targetDamageable is UnitBase);
                
                if (isTargetingUnit)
                {
                    if (targetTransform != null)
                    {
                        float diffX = targetTransform.position.x - transform.position.x;
                        float diffZ = targetTransform.position.z - transform.position.z;
                        float sqrDistToTarget = (diffX * diffX) + (diffZ * diffZ);
                        float maxSqrDist = (2f * GridManager.tileSize) * (2f * GridManager.tileSize);
                        if (sqrDistToTarget > maxSqrDist)
                        {
                            ChangeState(State.SearchTarget);
                            yield return null;
                            yield break;
                        }
                    }
                }
                else
                {
                    if (FindEnemyInGrid(out Transform enemyTransform, out IDamageable enemyDamageable))
                    {
                        ChangeState(State.SearchTarget);
                        yield return null;
                        yield break;
                    }
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
                // 유닛/건물 타겟이 죽거나 사라졌는지 체크
                if (targetTransform == null || !targetTransform.gameObject.activeInHierarchy || (targetDamageable != null && targetDamageable.IsDead))
                {
                    ChangeState(State.SearchTarget);
                    yield return null;
                    yield break; // 코루틴 즉시 종료
                }
                // 움직이는 타겟을 실시간으로 추적
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

            // O(1) 초고속 실효 사거리 및 멈춤 버퍼 연산 (제곱 거리 기반)
            float effectiveRange = meleeAttackRange + targetRadiusOffset;
            float sqrEffectiveRange = effectiveRange * effectiveRange;
            float stopRange = effectiveRange * 0.85f;
            float sqrStopRange = stopRange * stopRange;

            // 공격 범위 내에 들어왔는지 확인
            if (sqrDist <= sqrEffectiveRange)
            {
                ChangeState(State.Attack);
                SetDesiredMoveDirection(Vector3.zero); // 타겟 중심점 파고듦 즉시 차단
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

                        // [O(1) 멈춤 버퍼] 실효 사거리 85% 이내 진입 시 표면으로 파고드는 전진력 0으로 설정
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
            yield return null; // 이동 프레임 대기
        }

        public override void OnHit()
        {
            base.OnHit();

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
                    Vector3 spawnPos = (attackPoint != null) ? attackPoint.position : transform.position;
                    Quaternion spawnRot = (attackPoint != null) ? attackPoint.rotation : transform.rotation;
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
                    Vector3 spawnPos = (attackPoint != null) ? attackPoint.position : transform.position;
                    Quaternion spawnRot = (attackPoint != null) ? attackPoint.rotation : transform.rotation;
                    EffectManager.Instance.PlayEffect(EffectType.MeleeSlash, spawnPos, spawnRot, Color.white);
                }
            }
        }

        protected override IEnumerator AttackRoutine()
        {
            while (currentState == State.Attack)
            {
                PlayAttackAnimation();

                yield return attackCooldownTick;

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

        protected bool FindEnemyInGrid(out Transform closestTransform, out IDamageable closestDamageable)
        {
            closestTransform = null;
            closestDamageable = null;

            if (GridManager.Instance == null) return false;

            int tileIndex = currentTileIndex;
            if (tileIndex == -1)
            {
                tileIndex = GridManager.Instance.WorldToIndex(transform.position);
                if (tileIndex == -1) return false;
            }

            int width = GridManager.Instance.width;
            int height = GridManager.Instance.height;
            int cX = tileIndex % width;
            int cZ = tileIndex / width;
            
            float minSqrDist = float.MaxValue;
            Vector3 currentPos = transform.position;
            float maxSqrDist = (2f * GridManager.tileSize) * (2f * GridManager.tileSize);

            for (int z = cZ - 2; z <= cZ + 2; z++)
            {
                for (int x = cX - 2; x <= cX + 2; x++)
                {
                    if (x < 0 || x >= width || z < 0 || z >= height) continue;
                    
                    int checkIndex = z * width + x;
                    var unitsInTile = GridManager.Instance.gridUnits[checkIndex];
                    if (unitsInTile == null || unitsInTile.Count == 0) continue;
                    
                    for (int i = 0; i < unitsInTile.Count; i++)
                    {
                        UnitBase otherUnit = unitsInTile[i];
                        if (otherUnit == null || !otherUnit.gameObject.activeInHierarchy || otherUnit.IsDead || otherUnit == this) continue;
                        if (otherUnit.teamColor != enemyColor) continue;
                        
                        float diffX = otherUnit.transform.position.x - currentPos.x;
                        float diffZ = otherUnit.transform.position.z - currentPos.z;
                        float sqrDist = (diffX * diffX) + (diffZ * diffZ);
                        
                        if (sqrDist <= maxSqrDist && sqrDist < minSqrDist)
                        {
                            minSqrDist = sqrDist;
                            closestTransform = otherUnit.transform;
                            closestDamageable = otherUnit;
                        }
                    }
                }
            }

            return closestTransform != null;
        }

        /// <summary>
        /// 타겟 탐색 최적화 함수 (현재 호출 되는 함수) 
        /// </summary>
        protected virtual bool FindTargetOptimized()
        {
            Vector3 currentPos = transform.position;
            isTargetTile = false;
            targetTransform = null;
            targetDamageable = null;

            // 1단계: 반경 2타일 내 적 유닛 우선 탐색
            if (FindEnemyInGrid(out Transform enemyTransform, out IDamageable enemyDamageable))
            {
                targetTransform = enemyTransform;
                targetDamageable = enemyDamageable;
                isTargetTile = false;
                return true;
            }

            // 2단계: 적 건물 및 타일 전역 탐색
            float minSqrDist = float.MaxValue;
            Transform closestEnemyTransform = null;
            IDamageable closestEnemyDamageable = null;
            Vector3? closestTilePos = null;
            bool isTileTargetSelected = false;

            // 맵 전체 적 건물 탐색
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
                int buildingCount = buildings.Length;
                HashSet<TowerBase> visitedBuildings = new HashSet<TowerBase>();

                for (int i = 0; i < buildingCount; i++)
                {
                    TowerBase b = buildings[i];
                    if (b == null || b.TeamColor != enemyColor || b.IsDead || visitedBuildings.Contains(b)) 
                        continue;
                    visitedBuildings.Add(b);

                    float diffX = b.transform.position.x - currentPos.x;
                    float diffZ = b.transform.position.z - currentPos.z;
                    float sqrDist = (diffX * diffX) + (diffZ * diffZ);

                    if (sqrDist < minSqrDist)
                    {
                        minSqrDist = sqrDist;
                        closestEnemyTransform = b.transform;
                        closestEnemyDamageable = b;
                        isTileTargetSelected = false;
                    }
                }
            }

            // 맵 전체 점령 가능한 타일(적 타일 + 중립 타일) 탐색
            if (GridManager.Instance != null)
            {
                List<int> enemyTileIndices = (enemyColor == TeamColor.Blue) 
                    ? GridManager.Instance.blueTileIndices 
                    : GridManager.Instance.redTileIndices;
                List<int> neutralTileIndices = GridManager.Instance.neutralTileIndices;

                for (int pass = 0; pass < 2; pass++)
                {
                    List<int> targetIndices = (pass == 0) ? enemyTileIndices : neutralTileIndices;
                    if (targetIndices == null) continue;

                    int tileCount = targetIndices.Count;
                    for (int j = 0; j < tileCount; j++)
                    {
                        int index = targetIndices[j];

                        if (GridManager.Instance.gridBuildings[index] != null || GridManager.Instance.tileStates[index] == TeamColor.Obstacle)
                            continue;

                        Vector3 realTilePos = GridManager.Instance.IndexToWorld(index);

                        float diffX = realTilePos.x - currentPos.x;
                        float diffZ = realTilePos.z - currentPos.z;
                        float sqrDist = (diffX * diffX) + (diffZ * diffZ);

                        if (sqrDist < minSqrDist)
                        {
                            minSqrDist = sqrDist;
                            closestTilePos = realTilePos;
                            isTileTargetSelected = true;
                            closestEnemyTransform = null; 
                            closestEnemyDamageable = null;
                        }
                    }
                }
            }

            // 타겟 적용
            if (isTileTargetSelected && closestTilePos.HasValue)
            {
                targetPosition = closestTilePos.Value;
                isTargetTile = true;
                targetTransform = null;
                targetDamageable = null;
                return true;
            }
            else if (closestEnemyTransform != null)
            {
                targetTransform = closestEnemyTransform;
                targetDamageable = closestEnemyDamageable;
                isTargetTile = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 가장 가까운 [적 유닛, 적 건물, 적 타일] 대상을 최종 타겟으로 선정
        /// </summary>
        protected bool FindTarget()
        {
            Vector3 currentPos = transform.position;
            isTargetTile = false;
            targetTransform = null;
            targetDamageable = null;

            float minSqrDist = float.MaxValue;
            Transform closestEnemyTransform = null;
            IDamageable closestEnemyDamageable = null;
            Vector3? closestTilePos = null;
            bool isTileTargetSelected = false;
            string targetDebugName = "None";

            // 1. 맵 전체에서 가장 가까운 적 유닛 탐색
            if (UnitManager.Instance != null)
            {
                List<UnitBase> enemyUnits = (enemyColor == TeamColor.Blue) 
                    ? UnitManager.Instance.activeBlueUnits 
                    : UnitManager.Instance.activeRedUnits;

                if (enemyUnits != null)
                {
                    int unitCount = enemyUnits.Count;
                    for (int i = 0; i < unitCount; i++)
                    {
                        UnitBase otherUnit = enemyUnits[i];
                        if (otherUnit == null || !otherUnit.gameObject.activeInHierarchy || otherUnit.gameObject == this.gameObject || otherUnit.IsDead) 
                            continue;

                        float diffX = otherUnit.transform.position.x - currentPos.x;
                        float diffZ = otherUnit.transform.position.z - currentPos.z;
                        float sqrDist = (diffX * diffX) + (diffZ * diffZ);

                        if (sqrDist < minSqrDist)
                        {
                            minSqrDist = sqrDist;
                            closestEnemyTransform = otherUnit.transform;
                            closestEnemyDamageable = otherUnit;
                            isTileTargetSelected = false;
                            targetDebugName = $"유닛 ({otherUnit.name})";
                        }
                    }
                }
            }

            // 2. 맵 전체에서 가장 가까운 적 건물 탐색 (GridManager 배열 + 씬 내 건물 전체 탐색 폴백)
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
                int buildingCount = buildings.Length;
                HashSet<TowerBase> visitedBuildings = new HashSet<TowerBase>();

                for (int i = 0; i < buildingCount; i++)
                {
                    TowerBase b = buildings[i];
                    if (b == null || b.TeamColor != enemyColor || b.IsDead || visitedBuildings.Contains(b)) 
                        continue;
                    visitedBuildings.Add(b);

                    float diffX = b.transform.position.x - currentPos.x;
                    float diffZ = b.transform.position.z - currentPos.z;
                    float sqrDist = (diffX * diffX) + (diffZ * diffZ);

                    if (sqrDist < minSqrDist)
                    {
                        minSqrDist = sqrDist;
                        closestEnemyTransform = b.transform;
                        closestEnemyDamageable = b;
                        isTileTargetSelected = false;
                        targetDebugName = $"건물 ({b.name})";
                    }
                }
            }

            // 3. 맵 전체에서 가장 가까운 적 타일 탐색
            if (GridManager.Instance != null)
            {
                List<int> enemyTileIndices = (enemyColor == TeamColor.Blue) 
                    ? GridManager.Instance.blueTileIndices 
                    : GridManager.Instance.redTileIndices;

                if (enemyTileIndices != null)
                {
                    int tileCount = enemyTileIndices.Count;
                    for (int j = 0; j < tileCount; j++)
                    {
                        int index = enemyTileIndices[j];

                        // 건물이 이미 위치한 타일은 타겟 검색에서 배제
                        if (GridManager.Instance.gridBuildings[index] != null)
                            continue;

                        Vector3 realTilePos = GridManager.Instance.IndexToWorld(index);

                        // Y축 편차를 무시하고 2D 평면상에서의 거리 제곱 계산
                        float diffX = realTilePos.x - currentPos.x;
                        float diffZ = realTilePos.z - currentPos.z;
                        float sqrDist = (diffX * diffX) + (diffZ * diffZ);

                        if (sqrDist < minSqrDist)
                        {
                            minSqrDist = sqrDist;
                            closestTilePos = realTilePos;
                            isTileTargetSelected = true;
                            closestEnemyTransform = null;
                            closestEnemyDamageable = null;
                            targetDebugName = $"적 타일 (Index: {index}, 좌표: {realTilePos})";
                        }
                    }
                }
            }

            // 4. 최종 결정된 타겟 적용 및 디버그 출력
            if (isTileTargetSelected && closestTilePos.HasValue)
            {
                targetPosition = closestTilePos.Value;
                isTargetTile = true;
                targetTransform = null;
                targetDamageable = null;
                //Debug.Log($"[{gameObject.name} ({teamColor})] 새로운 타겟 지정: {targetDebugName}, 거리 제곱: {minSqrDist}");
                return true;
            }
            else if (closestEnemyTransform != null)
            {
                targetTransform = closestEnemyTransform;
                targetDamageable = closestEnemyDamageable;
                isTargetTile = false;
                //Debug.Log($"[{gameObject.name} ({teamColor})] 새로운 타겟 지정: {targetDebugName}, 거리 제곱: {minSqrDist}");
                return true;
            }

            Debug.Log($"[{gameObject.name} ({teamColor})] 타겟을 찾지 못했습니다.");
            return false;
        }
    }
}
