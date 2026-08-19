using System.Collections.Generic;
using UnityEngine;

namespace ColorCrash.Units
{
    public class Warlord : MeleeUnit
    {
        protected override void Awake()
        {
            base.Awake();
            unitId = "Warlord";
        }

        /// <summary>
        /// Warlord 장군 유닛 전용 타깃 탐색: 적 유닛(1순위) ➔ 적 건물(2순위) ➔ 타일 점령(3순위)
        /// 최고 등급 장군 유닛으로서 적 유닛 및 적 건물을 최우선 타격합니다.
        /// </summary>
        protected override bool FindTargetOptimized()
        {
            Vector3 currentPos = transform.position;
            isTargetTile = false;
            targetTransform = null;
            targetDamageable = null;

            // [1순위] 전장 전체의 적 유닛 우선 탐색 (가장 가까운 적 유닛)
            List<UnitBase> enemyUnits = (teamColor == TeamColor.Blue) ? UnitManager.Instance.activeRedUnits : UnitManager.Instance.activeBlueUnits;
            float closestUnitSqrDist = float.MaxValue;
            UnitBase closestEnemyUnit = null;

            if (enemyUnits != null)
            {
                for (int i = 0; i < enemyUnits.Count; i++)
                {
                    UnitBase unit = enemyUnits[i];
                    if (unit == null || unit.IsDead || !unit.gameObject.activeInHierarchy) continue;

                    float sqrDist = (unit.transform.position - currentPos).sqrMagnitude;
                    if (sqrDist < closestUnitSqrDist)
                    {
                        closestUnitSqrDist = sqrDist;
                        closestEnemyUnit = unit;
                    }
                }
            }

            if (closestEnemyUnit != null)
            {
                targetTransform = closestEnemyUnit.transform;
                targetDamageable = closestEnemyUnit;
                isTargetTile = false;
                return true;
            }

            // [2순위] 적 건물/요새 탐색 (타일보다 무조건 우선)
            TowerBase[] buildings = GridManager.Instance != null ? GridManager.Instance.gridBuildings : FindObjectsByType<TowerBase>(FindObjectsSortMode.None);
            float closestBuildingSqrDist = float.MaxValue;
            TowerBase closestBuilding = null;

            if (buildings != null)
            {
                HashSet<TowerBase> visitedBuildings = new HashSet<TowerBase>();
                for (int i = 0; i < buildings.Length; i++)
                {
                    TowerBase b = buildings[i];
                    if (b == null || b.IsDead || b.TeamColor == teamColor || b.TeamColor == TeamColor.Neutral || b.TeamColor == TeamColor.Obstacle || visitedBuildings.Contains(b)) continue;
                    visitedBuildings.Add(b);

                    float sqrDist = (b.transform.position - currentPos).sqrMagnitude;
                    if (sqrDist < closestBuildingSqrDist)
                    {
                        closestBuildingSqrDist = sqrDist;
                        closestBuilding = b;
                    }
                }
            }

            if (closestBuilding != null)
            {
                targetTransform = closestBuilding.transform;
                targetDamageable = closestBuilding;
                isTargetTile = false;
                return true;
            }

            // [3순위] 적 유닛과 적 건물이 단 하나도 없을 때만 마지막으로 타일 점령
            return base.FindTargetOptimized();
        }
    }
}
