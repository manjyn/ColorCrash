using System.Collections.Generic;
using UnityEngine;
using ColorCrash.Units;
using ColorCrash.Effects;

public class TowerCannon : TowerAttack
{
    public override void Initialize(TeamColor team)
    {
        base.Initialize(team);
    }

    private void Awake()
    {
        sizeX = 2;
        sizeZ = 2;
        buildingName = "Cannon";
    }

    protected override void FindTarget()
    {
        ClearTarget();
        float maxSqrDist = attackRange * attackRange;

        // 1. 유닛 탐색 (가장 가까운 유닛)
        List<UnitBase> enemies = (TeamColor == TeamColor.Blue) ? UnitManager.Instance.activeRedUnits : UnitManager.Instance.activeBlueUnits;
        float closestSqrDist = maxSqrDist;

        for (int i = 0; i < enemies.Count; i++)
        {
            UnitBase enemy = enemies[i];
            if (enemy == null || enemy.IsDead) continue;

            float sqrDist = (transform.position - enemy.transform.position).sqrMagnitude;
            if (sqrDist <= closestSqrDist)
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
                    if (sqrDist <= closestSqrDist)
                    {
                        closestSqrDist = sqrDist;
                        currentTargetBuilding = building;
                    }
                }
            }
        }

        if (currentTargetBuilding != null) return;

        // 3. 적 타일 탐색 (가장 가까운 타일, 장애물 타일 제외)
        if (GridManager.Instance != null)
        {
            float tileSize = GridManager.tileSize;
            int radiusTiles = Mathf.CeilToInt(attackRange / tileSize);
            
            int centerIndex = GridManager.Instance.WorldToIndex(transform.position);
            if (centerIndex != -1)
            {
                int centerX = centerIndex % GridManager.Instance.width;
                int centerZ = centerIndex / GridManager.Instance.width;

                float closestTileSqrDist = attackRange * attackRange;
                bool found = false;

                for (int x = centerX - radiusTiles; x <= centerX + radiusTiles; x++)
                {
                    for (int z = centerZ - radiusTiles; z <= centerZ + radiusTiles; z++)
                    {
                        if (x >= 0 && x < GridManager.Instance.width && z >= 0 && z < GridManager.Instance.height)
                        {
                            int index = z * GridManager.Instance.width + x;
                            TeamColor tileColor = GridManager.Instance.tileStates[index];
                            
                            // 내 영토가 아닌 타일 (적 타일, 중립 타일, 단 장애물 타일 제외)
                            if (tileColor != TeamColor && tileColor != TeamColor.Obstacle)
                            {
                                Vector3 tileWorldPos = GridManager.Instance.IndexToWorld(index);
                                float sqrDist = (transform.position - tileWorldPos).sqrMagnitude;
                                
                                if (sqrDist <= closestTileSqrDist)
                                {
                                    closestTileSqrDist = sqrDist;
                                    currentTargetPosition = tileWorldPos;
                                    found = true;
                                }
                            }
                        }
                    }
                }
                
                if (found) return;
            }
        }
    }

    protected override void FireProjectile()
    {
        if (projectilePrefab == null || firePoint == null) return;
        if (!HasValidTarget()) return;

        Vector3 targetPos = Vector3.zero;
        if (currentTarget != null)
        {
            targetPos = currentTarget.transform.position;
        }
        else if (currentTargetBuilding != null)
        {
            targetPos = currentTargetBuilding.transform.position;
        }
        else if (currentTargetPosition.HasValue)
        {
            targetPos = currentTargetPosition.Value;
            // 타겟 포지션을 사용하고 난 뒤에는 여기서 null로 만들면 안 되며(한 발 쏘고 타겟 잃음 방지),
            // TowerCannon의 FindTarget 로직에서 매번 초기화하므로 여기서는 읽기만 합니다.
        }

        if (EffectManager.Instance != null)
        {
            //Color effectColor = (teamColor == TeamColor.Blue) ? Color.blue : Color.red;
            Quaternion effectRot = Quaternion.identity;            
            if (targetPos != Vector3.zero)
            {
                Vector3 dir = (firePoint.position - targetPos).normalized;
                if (dir != Vector3.zero)
                    effectRot = Quaternion.LookRotation(dir);
            }
            EffectManager.Instance.PlayEffect(EffectType.CannonFire, firePoint.position, effectRot, Color.white);
        }

        CannonProjectile cannonPrefab = projectilePrefab as CannonProjectile;
        if (cannonPrefab != null)
        {
            CannonProjectile proj = null;
            if (ProjectileManager.Instance != null)
            {
                proj = ProjectileManager.Instance.SpawnProjectile(cannonPrefab, firePoint.position) as CannonProjectile;
            }
            else
            {
                proj = Instantiate(cannonPrefab, firePoint.position, Quaternion.identity);
            }
            
            if (proj != null)
            {
                if (currentTargetPosition.HasValue && targetPos == currentTargetPosition.Value)
                {
                    currentTargetPosition = null; // 발사 후 초기화
                }
                
                proj.Initialize(targetPos, damage, TeamColor);
            }
        }
    }
}
