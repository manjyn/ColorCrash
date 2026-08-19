using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorCrash.AI
{
    /// <summary>
    /// AI의 의사결정 소환 액션 데이터를 전달하는 구조체입니다.
    /// </summary>
    public struct AISpawnAction
    {
        public bool IsValid;
        public BattleObjectType ObjectType;
        public Vector2Int TargetTile;
        public int InkCost;
        public BuildingDisplacementInfo DisplacementInfo;
    }

    /// <summary>
    /// AI의 유닛 소환, 건물 방어선 축조, 마법 스펠(Ink Bomb) 시전 의사결정을 수행하는 플래너 클래스입니다.
    /// </summary>
    public class AISpawnPlanner : MonoBehaviour
    {
        [Header("연동 컴포넌트")]
        [SerializeField] private AITileTargeting _tileTargeting;
        [SerializeField] private BattlefieldEvaluator _evaluator;

        // 동적 스폰 타일 점유 캐시 버퍼
        private List<Vector2Int> _occupiedSpawnTileBuffer = new List<Vector2Int>(16);

        public event Action<Vector2Int, int, float> OnInkBombTriggered;

        public void InitializePlanner(AITileTargeting targeting, BattlefieldEvaluator evaluator)
        {
            _tileTargeting = targeting;
            _evaluator = evaluator;
        }

        #region Unit Spawn Planning
        /// <summary>
        /// 유저의 위험 진격 라인(X)에 대응하는 유닛 소환 의사결정을 수행합니다.
        /// </summary>
        public AISpawnAction PlanUnitSpawn(
            BattleAIDataSO aiData,
            int currentInk,
            List<BattleObjectType> currentHandCards,
            int targetUserLineX)
        {
            AISpawnAction action = new AISpawnAction { IsValid = false };

            if (aiData == null || currentHandCards == null || currentHandCards.Count == 0)
            {
                return action;
            }

            // 1단계: 유저 유닛 구성 스캔 기반 스마트 카운터 유닛 선별
            BattleObjectType counterUnit = SelectSmartCounterUnit(currentHandCards, currentInk);
            int inkCost = GetInkCost(counterUnit);

            if (currentInk < inkCost)
            {
                return action;
            }

            int tileTargetOffset = aiData.TileTargetOffset;
            Vector2Int spawnTile = _tileTargeting.GetBestSpawnTile(targetUserLineX, _occupiedSpawnTileBuffer, tileTargetOffset);

            action.ObjectType = counterUnit;
            action.TargetTile = spawnTile;
            action.InkCost = inkCost;
            action.IsValid = true;

            RegisterOccupiedSpawnTile(spawnTile);

            return action;
        }

        /// <summary>
        /// Blue팀 유닛 구성(Footman, Archer, Elite) 및 전황 지표를 스캔하여 
        /// 가장 우위의 상성을 가지는 유닛 카드를 손패 중에서 지능적으로 선별합니다.
        /// </summary>
        private BattleObjectType SelectSmartCounterUnit(List<BattleObjectType> handCards, int currentInk)
        {
            List<BattleObjectType> playableUnits = new List<BattleObjectType>();
            for (int i = 0; i < handCards.Count; i++)
            {
                BattleObjectType card = handCards[i];
                if (IsUnitCard(card) && currentInk >= GetInkCost(card))
                {
                    playableUnits.Add(card);
                }
            }

            if (playableUnits.Count == 0)
            {
                return BattleObjectType.Footman;
            }

            int blueFootman = (_evaluator != null) ? _evaluator.BlueFootmanCount : 0;
            int blueArcher = (_evaluator != null) ? _evaluator.BlueArcherCount : 0;
            int blueElite = (_evaluator != null) ? _evaluator.BlueEliteCount : 0;

            BattleObjectType preferredCounter = BattleObjectType.Footman;
            if (blueArcher >= 2 && playableUnits.Contains(BattleObjectType.Footman))
            {
                preferredCounter = BattleObjectType.Footman;
            }
            else if (blueFootman >= 2 && playableUnits.Contains(BattleObjectType.Archer))
            {
                preferredCounter = BattleObjectType.Archer;
            }
            else if (blueElite >= 1)
            {
                if (playableUnits.Contains(BattleObjectType.Warlord)) preferredCounter = BattleObjectType.Warlord;
                else if (playableUnits.Contains(BattleObjectType.EliteUnit)) preferredCounter = BattleObjectType.EliteUnit;
                else if (playableUnits.Contains(BattleObjectType.Footman)) preferredCounter = BattleObjectType.Footman;
            }
            else
            {
                preferredCounter = playableUnits[UnityEngine.Random.Range(0, playableUnits.Count)];
            }

            if (playableUnits.Contains(preferredCounter) && UnityEngine.Random.value <= 0.75f)
            {
                return preferredCounter;
            }

            List<BattleObjectType> weightedPool = new List<BattleObjectType>();
            for (int i = 0; i < playableUnits.Count; i++)
            {
                BattleObjectType type = playableUnits[i];
                int weight = GetInkCost(type);
                for (int w = 0; w < weight; w++)
                {
                    weightedPool.Add(type);
                }
            }

            int randomIndex = UnityEngine.Random.Range(0, weightedPool.Count);
            return weightedPool[randomIndex];
        }
        #endregion

        #region Building Placement Planning
        /// <summary>
        /// 건물(Barricade, Cannon, Mortar) 배치 의사결정을 수행합니다.
        /// (적 3기 이상 & Y축 5타일 이내 밀집 시 최전방 차단 바리게이트 우선 소환)
        /// </summary>
        public AISpawnAction PlanBuildingSpawn(
            BattleAIDataSO aiData,
            int currentInk,
            List<BattleObjectType> currentHandCards,
            int stageScaleLevel,
            Func<Vector2Int, TeamColor> getTileColor,
            Func<Vector2Int, bool> isObstacle,
            List<Vector2Int> blueUnitPositions = null)
        {
            AISpawnAction action = new AISpawnAction { IsValid = false };

            if (aiData == null || currentHandCards == null)
            {
                return action;
            }

            int frontlineY = (_evaluator != null) ? _evaluator.FrontlineY : 0;
            Vector2Int clusterCenter = new Vector2Int(-1, -1);
            bool hasEnemyCluster = (_tileTargeting != null) && _tileTargeting.FindEnemyClusterChokepoint(blueUnitPositions, frontlineY, out clusterCenter);

            BattleObjectType selectedBuilding;
            int selectedCost;

            bool foundBuilding = SelectStrategicBuilding(currentInk, currentHandCards, hasEnemyCluster, out selectedBuilding, out selectedCost);

            if (!foundBuilding)
            {
                return action;
            }

            int dangerLineX = hasEnemyCluster ? clusterCenter.x : ((_evaluator != null) ? _evaluator.MostDangerousLineX : 7);
            BuildingDisplacementInfo displacement;

            Vector2Int bTile = _tileTargeting.GetBestBuildingTile(
                selectedBuilding,
                frontlineY,
                dangerLineX,
                getTileColor,
                isObstacle,
                out displacement);

            if (bTile.x >= 0 && bTile.y >= 0)
            {
                action.ObjectType = selectedBuilding;
                action.TargetTile = bTile;
                action.InkCost = selectedCost;
                action.IsValid = true;
                action.DisplacementInfo = displacement;
            }

            return action;
        }

        /// <summary>
        /// 전황(유저 위협도, 적 밀집 핫스팟, 유닛/건물 조합) 및 전장 건물 희소성/비율을 동적 가상점/감점 수식으로 실시간 평가하여 최적의 방어 건물을 선별합니다.
        /// </summary>
        private bool SelectStrategicBuilding(
            int currentInk,
            List<BattleObjectType> handCards,
            bool hasEnemyCluster,
            out BattleObjectType selectedBuilding,
            out int selectedCost)
        {
            selectedBuilding = BattleObjectType.Barricade;
            selectedCost = 2;

            if (handCards == null || handCards.Count == 0) return false;

            List<BattleObjectType> availableBuildings = new List<BattleObjectType>();
            for (int i = 0; i < handCards.Count; i++)
            {
                BattleObjectType card = handCards[i];
                if ((card == BattleObjectType.Barricade || card == BattleObjectType.Cannon || card == BattleObjectType.Mortar) && currentInk >= GetInkCost(card))
                {
                    if (!availableBuildings.Contains(card)) availableBuildings.Add(card);
                }
            }

            if (availableBuildings.Count == 0)
            {
                return false;
            }

            float bestScore = -999f;
            BattleObjectType bestType = availableBuildings[0];

            // 전황 지표 수집
            float threat = (_evaluator != null) ? _evaluator.ThreatLevel : 0f;
            int blueFootman = (_evaluator != null) ? _evaluator.BlueFootmanCount : 0;
            int blueArcher = (_evaluator != null) ? _evaluator.BlueArcherCount : 0;
            int blueElite = (_evaluator != null) ? _evaluator.BlueEliteCount : 0;
            int redBarricade = (_evaluator != null) ? _evaluator.RedBarricadeCount : 0;
            int redCannon = (_evaluator != null) ? _evaluator.RedCannonCount : 0;
            int redMortar = (_evaluator != null) ? _evaluator.RedMortarCount : 0;

            for (int i = 0; i < availableBuildings.Count; i++)
            {
                BattleObjectType bType = availableBuildings[i];
                float score = 50f; // Base Score (기본 점수)
                int existingCount = 0;

                switch (bType)
                {
                    case BattleObjectType.Barricade:
                        existingCount = redBarricade;
                        if (threat >= 1.0f || blueFootman >= 2) score += 40f; // 전선 차단 가점
                        if (hasEnemyCluster) score += 50f; // [핵심] Y축 5타일 내 3기 이상 적 밀집 시 바리게이트 최우선 구축 가점!
                        if (redBarricade == 0) score += 30f; // 1차 방어선 구축 보너스
                        break;

                    case BattleObjectType.Cannon:
                        existingCount = redCannon;
                        if (blueElite >= 1) score += 40f; // 정예 유닛 화력 제압 가점
                        if (redBarricade >= 1) score += 30f; // Barricade 후방 화력 지원 보너스
                        break;

                    case BattleObjectType.Mortar:
                        existingCount = redMortar;
                        if (blueArcher >= 2) score += 40f; // 적 원거리 궁수 저격 가점
                        if (redBarricade >= 1 || redCannon >= 1) score += 20f; // 안전 포격 지원 보너스
                        break;
                }

                // 1. 희소성 동적 가상점 (Rarity Bonus)
                if (existingCount == 0) score += 35f;
                else if (existingCount == 1) score += 15f;

                // 2. 개수 누적 소프트 감점 (Accumulated Quantity Penalty)
                float penalty = existingCount * 25f;
                score -= penalty;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestType = bType;
                }
            }

            selectedBuilding = bestType;
            selectedCost = GetInkCost(bestType);
            return true;
        }
        #endregion

        #region Ink Bomb Independent Spell Planning & Execution
        /// <summary>
        /// [2단계 개편] Ink Bomb (3 Ink, 3x3 범위) 독립 마법 스펠 시전 의사결정을 수행합니다.
        /// 손패 4장과 상관없이 3 Ink 보유 및 유저 2기 이상 밀집 시 언제든지 독립 발동합니다.
        /// </summary>
        public AISpawnAction PlanSpellSpawn(
            BattleAIDataSO aiData,
            int currentInk,
            List<Vector2Int> blueUnitPositions,
            Func<Vector2Int, bool> isObstacle)
        {
            AISpawnAction action = new AISpawnAction { IsValid = false };

            int spellCost = GetInkCost(BattleObjectType.InkBomb);
            if (currentInk < spellCost)
            {
                return action;
            }

            // 유저 유닛 또는 유저 건물 유효 대상 확인 (유닛 2기 이상 또는 유닛+건물 밀집 시 시전)
            int targetCount = (blueUnitPositions != null) ? blueUnitPositions.Count : 0;
            if (GridManager.Instance != null && GridManager.Instance.gridBuildings != null)
            {
                for (int i = 0; i < GridManager.Instance.gridBuildings.Length; i++)
                {
                    TowerBase b = GridManager.Instance.gridBuildings[i];
                    if (b != null && !b.IsDead && b.TeamColor == TeamColor.Blue)
                    {
                        targetCount++;
                    }
                }
            }

            if (targetCount < 2)
            {
                return action;
            }

            // AI의 스펠 시전 성공 확률(CounterChance) 주사위 판정
            float chance = (aiData != null) ? aiData.CounterChance : 0.6f;
            bool passedDice = (UnityEngine.Random.value <= chance);

            Debug.Log($"[AISpawnPlanner] 독립 InkBomb 스펠 조건 체크 - 유저유닛수: {blueUnitPositions.Count}, 잉크: {currentInk}/{spellCost}, 주사위: {passedDice}");

            if (!passedDice)
            {
                return action;
            }

            Vector2Int targetTile = _tileTargeting.GetBestInkBombTile(
                blueUnitPositions,
                aiData != null ? aiData.TileTargetOffset : 0,
                isObstacle);

            action.ObjectType = BattleObjectType.InkBomb;
            action.TargetTile = targetTile;
            action.InkCost = spellCost;
            action.IsValid = true;

            Debug.Log($"[AISpawnPlanner] 💣 독립 InkBomb 스펠 시전 확정! -> 타깃 타일: {targetTile}");

            return action;
        }

        public void ExecuteInkBombSpell(Vector2Int targetCenterTile)
        {
            int radius = ColorCrash.Spell.SpellDatabase.GetSpellRadius("InkBomb", 1);
            float damage = ColorCrash.Spell.SpellDatabase.GetSpellDamage("InkBomb", 50f);

            SpellSpawnHandler.LaunchInkBombSpell(targetCenterTile, TeamColor.Red, damage, radius);
            OnInkBombTriggered?.Invoke(targetCenterTile, radius, damage);
        }
        #endregion

        #region Utility & Hand Checking Methods
        /// <summary>
        /// 손패 4장 내 유닛 카드가 1장이라도 포함되어 있는지 여부를 스캔합니다.
        /// </summary>
        public bool HasUnitCardInHand(List<BattleObjectType> handCards)
        {
            if (handCards == null || handCards.Count == 0) return false;
            for (int i = 0; i < handCards.Count; i++)
            {
                if (IsUnitCard(handCards[i])) return true;
            }
            return false;
        }

        /// <summary>
        /// 손패 4장 내 건물 카드가 1장이라도 포함되어 있는지 여부를 스캔합니다.
        /// </summary>
        public bool HasBuildingCardInHand(List<BattleObjectType> handCards)
        {
            if (handCards == null || handCards.Count == 0) return false;
            for (int i = 0; i < handCards.Count; i++)
            {
                BattleObjectType card = handCards[i];
                if (card == BattleObjectType.Barricade || card == BattleObjectType.Cannon || card == BattleObjectType.Mortar)
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsUnitCard(BattleObjectType type)
        {
            return type == BattleObjectType.Footman ||
                   type == BattleObjectType.Archer ||
                   type == BattleObjectType.EliteUnit ||
                   type == BattleObjectType.Warlord;
        }

        public int GetInkCost(BattleObjectType type)
        {
            switch (type)
            {
                case BattleObjectType.Footman: return 2;
                case BattleObjectType.Archer: return 3;
                case BattleObjectType.EliteUnit: return 3;
                case BattleObjectType.Warlord: return 4;
                case BattleObjectType.Barricade: return 2;
                case BattleObjectType.Cannon: return 4;
                case BattleObjectType.Mortar: return 5;
                case BattleObjectType.InkBomb: return 3;
                default: return 2;
            }
        }

        public void RegisterOccupiedSpawnTile(Vector2Int tile)
        {
            if (!_occupiedSpawnTileBuffer.Contains(tile))
            {
                _occupiedSpawnTileBuffer.Add(tile);
            }
        }

        public void ClearOccupiedSpawnTiles()
        {
            _occupiedSpawnTileBuffer.Clear();
        }
        #endregion
    }
}
