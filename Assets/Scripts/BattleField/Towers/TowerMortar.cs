using System.Collections.Generic;
using UnityEngine;
using ColorCrash.Units;
using ColorCrash.Effects;

public class TowerMortar : TowerAttack
{
    [Header("Mortar Settings")]
    [HideInInspector] public float minAttackRange = 3f;

    public override void Initialize(TeamColor team)
    {
        base.Initialize(team);

        // 추가 스탯 덮어쓰기 (DB 로드)
        if (TowerManager.Instance != null && TowerManager.Instance.TowerDatabase != null)
        {
            if (TowerManager.Instance.TowerDatabase.TryGetRecord(towerId, out var record))
            {
                minAttackRange = record.MinAttackRange;
            }
        }
    }

    private void Awake()
    {
        sizeX = 2;
        sizeZ = 2;
        buildingName = "Mortar";
    }

    protected override bool IsTargetInRange(Vector3 targetPos)
    {
        float sqrDist = (transform.position - targetPos).sqrMagnitude;
        return sqrDist >= (minAttackRange * minAttackRange) && sqrDist <= (attackRange * attackRange);
    }

    protected override void FindTarget()
    {
        ClearTarget();
        float maxSqrDist = attackRange * attackRange;
        float minSqrDist = minAttackRange * minAttackRange;

        // 1. 유닛 탐색 (가장 가까운 유닛)
        List<UnitBase> enemies = (TeamColor == TeamColor.Blue) ? UnitManager.Instance.activeRedUnits : UnitManager.Instance.activeBlueUnits;
        float closestSqrDist = maxSqrDist;

        for (int i = 0; i < enemies.Count; i++)
        {
            UnitBase enemy = enemies[i];
            if (enemy == null || enemy.IsDead) continue;

            float sqrDist = (transform.position - enemy.transform.position).sqrMagnitude;
            if (sqrDist >= minSqrDist && sqrDist <= closestSqrDist)
            {
                closestSqrDist = sqrDist;
                currentTarget = enemy;
            }
        }

        if (currentTarget != null) return;

        // 2. 건물 탐색 (가장 가까운 적 건물, 장애물 제외)
        if (GridManager.Instance != null)
        {
            closestSqrDist = maxSqrDist;
            TowerBase[] gridBuildings = GridManager.Instance.gridBuildings;
            
            for (int i = 0; i < gridBuildings.Length; i++)
            {
                TowerBase building = gridBuildings[i];
                if (building != null && !building.IsDead && 
                    building.TeamColor != TeamColor && 
                    building.TeamColor != TeamColor.Neutral && 
                    building.TeamColor != TeamColor.Obstacle)
                {
                    float sqrDist = (transform.position - building.transform.position).sqrMagnitude;
                    if (sqrDist >= minSqrDist && sqrDist <= closestSqrDist)
                    {
                        closestSqrDist = sqrDist;
                        currentTargetBuilding = building;
                    }
                }
            }
        }

        if (currentTargetBuilding != null) return;

        // 3. 적 타일 탐색 (랜덤 샘플링 O(1), 장애물 타일 제외)
        if (GridManager.Instance != null)
        {
            int maxAttempts = 10;
            float minRadius = minAttackRange;
            float maxRadius = attackRange;

            for (int i = 0; i < maxAttempts; i++)
            {
                Vector2 randCircle = Random.insideUnitCircle.normalized;
                float randDist = Random.Range(minRadius, maxRadius);
                Vector3 samplePos = transform.position + new Vector3(randCircle.x * randDist, 0f, randCircle.y * randDist);

                int index = GridManager.Instance.WorldToIndex(samplePos);
                if (index != -1)
                {
                    TeamColor tileColor = GridManager.Instance.tileStates[index];
                    // 내 영토가 아닌 타일 (적 타일, 중립 타일, 단 장애물 타일 제외)
                    if (tileColor != TeamColor && tileColor != TeamColor.Obstacle)
                    {
                        currentTargetPosition = GridManager.Instance.IndexToWorld(index);
                        return;
                    }
                }
            }
        }
    }

    protected override void FireProjectile()
    {
        if (projectilePrefab == null || firePoint == null) return;
        if (!HasValidTarget()) return;

        if (EffectManager.Instance != null)
        {
            Color effectColor = (teamColor == TeamColor.Blue) ? Color.blue : Color.red;
            EffectManager.Instance.PlayEffect(EffectType.Muzzle, firePoint.position, effectColor);
        }

        MortarProjectile mortarPrefab = projectilePrefab as MortarProjectile;
        if (mortarPrefab != null)
        {
            // 중앙 풀 매니저를 통해 포탄 가져오기
            MortarProjectile proj = null;
            if (ProjectileManager.Instance != null)
            {
                proj = ProjectileManager.Instance.SpawnProjectile(mortarPrefab, firePoint.position) as MortarProjectile;
            }
            else
            {
                proj = Instantiate(mortarPrefab, firePoint.position, Quaternion.identity);
            }
            
            if (proj != null)
            {
                if (currentTarget != null)
                {
                    proj.Initialize(currentTarget.transform.position, damage, TeamColor);
                }
                else if (currentTargetBuilding != null)
                {
                    proj.Initialize(currentTargetBuilding.transform.position, damage, TeamColor);
                }
                else if (currentTargetPosition.HasValue)
                {
                    proj.Initialize(currentTargetPosition.Value, damage, TeamColor);                
                    currentTargetPosition = null;
                }
            }
        }
    }
}
