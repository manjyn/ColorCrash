using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorCrash.AI
{
    /// <summary>
    /// AI의 의사결정 상태를 나타내는 전투 Phase 열거형입니다 (GDD 4.1).
    /// </summary>
    public enum BattlePhase
    {
        Phase1_Opening = 1,  // 0% ~ 20% 시간 경과 (초반 관망/탐색)
        Phase2_Fortress = 2, // 20% ~ 50% 시간 경과 (요새 방어선 형성)
        Phase3_Siege = 3,    // 50% ~ 80% 시간 경과 (전면 진격/공성)
        Phase4_Fever = 4     // 80% ~ 100% 시간 경과 (피버 템포 가속)
    }

    /// <summary>
    /// 적 팀(Red) 전투 AI의 FSM 상태 및 동적 잉크 템포 충전, 손패 덱 순환, 의사결정 파이프라인을 통괄하는 메인 컨트롤러입니다.
    /// </summary>
    [RequireComponent(typeof(BattlefieldEvaluator))]
    [RequireComponent(typeof(AISpawnPlanner))]
    [RequireComponent(typeof(AITileTargeting))]
    public class EnemyAIController : MonoBehaviour
    {
        [Header("AI 프로필 데이터 지정")]
        [SerializeField] private BattleAIDataSO _aiData;

        [Header("실시간 AI 런타임 상태 (Inspector 모니터링용)")]
        [SerializeField] private float _currentInk = 4f;
        [SerializeField] private int _maxInkCap = 10;
        [SerializeField] private BattlePhase _currentPhase = BattlePhase.Phase1_Opening;
        [SerializeField] private bool _isCrisisState = false;
        [SerializeField] private bool _isPaused = false;

        [Header("연동 헬퍼 컴포넌트")]
        [SerializeField] private BattlefieldEvaluator _evaluator;
        [SerializeField] private AISpawnPlanner _planner;
        [SerializeField] private AITileTargeting _targeting;

        // 세션 파라미터 캐싱
        private float _matchDuration = 180f;
        private int _mapScaleLevel = 3;
        private int _unitScaleParam = 5;

        // 덱 관리 필드 (손패 4슬롯 드로우 시스템)
        private List<BattleObjectType> _unlockedDeckPool = new List<BattleObjectType>();
        private List<BattleObjectType> _handCards = new List<BattleObjectType>(4);

        // 런타임 타이머 필드
        private float _elapsedTime = 0f;
        private float _evaluationTimer = 0f;
        private float _actionCooldownTimer = 0f;
        private float _rerollCooldownTimer = 0f;
        private float _currentTargetRatio = 3.5f;

        // 델리게이트 런타임 콜백
        private Func<Vector2Int, TeamColor> _getTileColorCallback;
        private Func<Vector2Int, bool> _isObstacleCallback;

        // 유공 타깃 정보 버퍼
        private List<Vector2Int> _blueUnitPositions = new List<Vector2Int>();
        private int _targetUserLineX = 7;

        #region Public Properties & Events
        public BattleAIDataSO AIData => _aiData;
        public float CurrentInk => _currentInk;
        public int MaxInkCap => _maxInkCap;
        public BattlePhase CurrentPhase => _currentPhase;
        public bool IsCrisisState => _isCrisisState;
        public bool IsPaused => _isPaused;
        public IReadOnlyList<BattleObjectType> HandCards => _handCards;

        /// <summary>
        /// AI 소환/배치/스펠 시전 행동이 확정되었을 때 발생하는 실시간 이벤트입니다.
        /// </summary>
        public event Action<AISpawnAction> OnAIActionExecuted;
        #endregion

        #region Pause & Resume Control
        /// <summary>
        /// AI의 모든 로직(잉크 충전, 타이머, 의사결정 파이프라인)을 일시정지합니다.
        /// </summary>
        public void PauseAI()
        {
            _isPaused = true;
        }

        /// <summary>
        /// 일시정지된 AI 로직을 다시 재개합니다.
        /// </summary>
        public void ResumeAI()
        {
            _isPaused = false;
        }

        /// <summary>
        /// AI 일시정지 상태를 지정한 값으로 설정합니다.
        /// </summary>
        public void SetPause(bool isPaused)
        {
            _isPaused = isPaused;
        }

        /// <summary>
        /// AI 일시정지 상태를 토글(Pause ↔ Resume)합니다.
        /// </summary>
        public void TogglePauseAI()
        {
            _isPaused = !_isPaused;
        }
        #endregion

        private void Awake()
        {
            if (_evaluator == null) _evaluator = GetComponent<BattlefieldEvaluator>();
            if (_planner == null) _planner = GetComponent<AISpawnPlanner>();
        }

        /// <summary>
        /// AI 컨트롤러 런타임 세션을 초기화합니다.
        /// </summary>
        public void InitializeSession(
            BattleAIDataSO aiData,
            float matchDuration,
            int mapScaleLevel,
            int unitScaleParam,
            List<BattleObjectType> unlockedDeck,
            Func<Vector2Int, TeamColor> getTileColor,
            Func<Vector2Int, bool> isObstacle)
        {
            if (aiData != null)
            {
                _aiData = aiData;
            }
            else if (_aiData == null)
            {
                _aiData = ScriptableObject.CreateInstance<BattleAIDataSO>();
                _aiData.ProfileName = "Default_Normal";
                _aiData.EvaluationInterval = 0.8f;
                _aiData.MinActionInterval = 0.2f;
                _aiData.InkMultiplier = 1.0f;
                _aiData.CounterChance = 0.6f;
                _aiData.RerollChance = 0.3f;
                _aiData.PreferredUnitCards = new List<BattleObjectType> { BattleObjectType.Footman, BattleObjectType.Archer, BattleObjectType.EliteUnit };
                _aiData.PreferredSpellCards = new List<BattleObjectType> { BattleObjectType.InkBomb };
                Debug.LogWarning("[EnemyAIController] aiData가 null이어서 기본 디폴트 AI 프로필을 동적으로 100% 자동 생성하였습니다.");
            }

            _matchDuration = Mathf.Max(30f, matchDuration);
            _mapScaleLevel = Mathf.Clamp(mapScaleLevel, 1, 5);
            _unitScaleParam = unitScaleParam;
            _getTileColorCallback = getTileColor;
            _isObstacleCallback = isObstacle;

            _maxInkCap = (_mapScaleLevel == 5) ? 15 : 10;
            _currentInk = 4f; // 기본 초기 보유 4 Ink 스타트
            _elapsedTime = 0f;
            _currentPhase = BattlePhase.Phase1_Opening;
            _isCrisisState = false;
            _isPaused = false;

            _evaluationTimer = 0f;
            _actionCooldownTimer = 0f;

            // [3단계 추가] 세션 무작위 목표 유닛/건물 비율 다이내믹 롤링
            float minRatio = (_aiData != null) ? _aiData.MinUnitBuildingRatio : 2.5f;
            float maxRatio = (_aiData != null) ? _aiData.MaxUnitBuildingRatio : 4.5f;
            _currentTargetRatio = UnityEngine.Random.Range(minRatio, maxRatio);
            Debug.Log($"[EnemyAIController] 세션 목표 유닛/건물 비율 롤링 확정: {_currentTargetRatio:F2} (범위: {minRatio} ~ {maxRatio})");

            InitializeHandDeck(unlockedDeck);

            if (_evaluator == null) _evaluator = GetComponent<BattlefieldEvaluator>();
            if (_planner == null) _planner = GetComponent<AISpawnPlanner>();
            if (_targeting == null) _targeting = GetComponent<AITileTargeting>();

            int w = (BattleContext.MapStages != null && _mapScaleLevel <= BattleContext.MapStages.Length) ? BattleContext.MapStages[_mapScaleLevel - 1].Width : 14;
            int h = (BattleContext.MapStages != null && _mapScaleLevel <= BattleContext.MapStages.Length) ? BattleContext.MapStages[_mapScaleLevel - 1].Height : 24;

            if (_evaluator != null) _evaluator.InitializeMapGrid(w, h, _unitScaleParam);
            if (_targeting != null) _targeting.InitializeTargeting(w, h);
            if (_planner != null) _planner.InitializePlanner(_targeting, _evaluator);
        }

        private void Update()
        {
            if (_isPaused) return;

            float dt = Time.deltaTime;

            // 1. 전투 경과 시간 및 4-Phase FSM 전환
            UpdatePhaseState(dt);

            // 2. 동적 잉크 템포 충전 엔진
            UpdateInkCharge(dt);

            // 3. 타이머 차감
            if (_actionCooldownTimer > 0f) _actionCooldownTimer -= dt;
            if (_rerollCooldownTimer > 0f) _rerollCooldownTimer -= dt;

            // 4. 인적 감성 주기별 의사결정 파이프라인 루프
            _evaluationTimer += dt;
            float evalInterval = (_aiData != null) ? _aiData.EvaluationInterval : 0.8f;

            if (_evaluationTimer >= evalInterval)
            {
                _evaluationTimer = 0f;

                if (_actionCooldownTimer <= 0f)
                {
                    ExecuteDecisionPipeline();
                }
            }
        }

        private void UpdatePhaseState(float dt)
        {
            _elapsedTime += dt;
            float progressRatio = Mathf.Clamp01(_elapsedTime / _matchDuration);

            if (progressRatio < 0.20f)
            {
                _currentPhase = BattlePhase.Phase1_Opening;
            }
            else if (progressRatio < 0.50f)
            {
                _currentPhase = BattlePhase.Phase2_Fortress;
            }
            else if (progressRatio < 0.80f)
            {
                _currentPhase = BattlePhase.Phase3_Siege;
            }
            else
            {
                _currentPhase = BattlePhase.Phase4_Fever;
                _maxInkCap = 15;
            }

            float crisisThreshold = (_aiData != null) ? _aiData.CrisisTerritoryThreshold : 0.35f;
            _isCrisisState = (_evaluator != null && _evaluator.TerritoryRatio < (crisisThreshold * 100f));
        }

        private void UpdateInkCharge(float dt)
        {
            if (_currentInk >= _maxInkCap) return;

            float baseInterval = 1.2f - (_unitScaleParam * 0.02f);
            baseInterval = Mathf.Max(0.6f, baseInterval);

            float chargeRatePerSec = 1.0f / baseInterval;

            if (_currentPhase == BattlePhase.Phase4_Fever)
            {
                float feverMultiplier = (_aiData != null) ? _aiData.FeverTempoMultiplier : 1.5f;
                chargeRatePerSec = (1.0f / 0.5f) * feverMultiplier;
            }
            else if (_isCrisisState)
            {
                chargeRatePerSec = 1.0f / 0.77f;
            }

            if (_aiData != null)
            {
                chargeRatePerSec *= _aiData.InkMultiplier;
            }

            _currentInk = Mathf.Min(_maxInkCap, _currentInk + (chargeRatePerSec * dt));
        }

        /// <summary>
        /// 계층적 2단계 트리 파이프라인 (Pass 1: 독립 InkBomb 스펠 ➔ Pass 2: 동적 비율 & 손패 쏠림 폴백 ➔ Pass 3: 전략 소환)
        /// </summary>
        private void ExecuteDecisionPipeline()
        {
            if (_planner == null || _aiData == null || _handCards.Count == 0) return;

            int intInk = Mathf.FloorToInt(_currentInk);

            if (ShouldSaveInkForHighCostCard(intInk))
            {
                return;
            }

            // [제 1선택] Ink Bomb 독립 마법 스펠 시전 판단 (손패 미포함, 3 Ink 소모)
            AISpawnAction spellAction = _planner.PlanSpellSpawn(
                _aiData,
                intInk,
                _blueUnitPositions,
                _isObstacleCallback);

            if (spellAction.IsValid)
            {
                ExecuteAction(spellAction);
                _planner.ExecuteInkBombSpell(spellAction.TargetTile);
                return;
            }

            // [제 2선택] 유닛 vs 건물 동적 비율 및 손패 쏠림 폴백 평가
            int currentUnits = _evaluator.CurrentRedUnitCount;
            int currentBuildings = _evaluator.RedStructureCount;
            float currentRatio = (currentUnits) / (float)Mathf.Max(1, currentBuildings);

            // [버그 수정 1] _mapScaleLevel > 2 제약 제거하여 모든 맵에서 건물 소환 가능
            bool wantBuilding = (currentRatio >= _currentTargetRatio);

            bool hasBuildingCard = _planner.HasBuildingCardInHand(_handCards);
            bool hasUnitCard = _planner.HasUnitCardInHand(_handCards);
            float threatLevel = _evaluator.ThreatLevel;

            // [버그 수정 2] 비상 전황 오버라이드 수치 완화 (threatLevel >= 4.0f 또는 Red 유닛 0기 전멸 시에만 오버라이드)
            if (currentUnits == 0 || threatLevel >= 4.0f)
            {
                if (wantBuilding)
                {
                    wantBuilding = false;
                }
            }

            // 손패 쏠림 극단 예외 폴백 (Hand Bias Fallback Engine)
            if (wantBuilding && !hasBuildingCard)
            {
                wantBuilding = false;

                if (intInk >= 4 && _rerollCooldownTimer <= 0f && UnityEngine.Random.value <= _aiData.RerollChance)
                {
                    TryInkReroll();
                    return;
                }
            }
            else if (!wantBuilding && !hasUnitCard)
            {
                wantBuilding = true;

                if (intInk >= 4 && _rerollCooldownTimer <= 0f && UnityEngine.Random.value <= _aiData.RerollChance)
                {
                    TryInkReroll();
                    return;
                }
            }

            // [제 3단계] 전략적 소환 실행
            if (wantBuilding)
            {
                AISpawnAction buildingAction = _planner.PlanBuildingSpawn(
                    _aiData,
                    intInk,
                    _handCards,
                    _mapScaleLevel,
                    _getTileColorCallback,
                    _isObstacleCallback,
                    _blueUnitPositions);

                if (buildingAction.IsValid)
                {
                    ExecuteAction(buildingAction);
                    return;
                }
            }

            // 유닛 소환 시도 (또는 건물 배치 실패 시 유닛 소환 폴백)
            int targetLineX = _evaluator.MostDangerousLineX;
            AISpawnAction unitAction = _planner.PlanUnitSpawn(_aiData, intInk, _handCards, targetLineX);

            if (unitAction.IsValid)
            {
                ExecuteAction(unitAction);
                return;
            }

            // 손패 리롤 (Ink Reroll) 시도
            if (intInk >= 4 && _rerollCooldownTimer <= 0f)
            {
                if (UnityEngine.Random.value <= _aiData.RerollChance)
                {
                    TryInkReroll();
                    return;
                }
            }

            // 저코스트 덱 순환 소환 (Fallback Order)
            AISpawnAction fallbackAction = PlanFallbackSpawn(intInk);
            if (fallbackAction.IsValid)
            {
                ExecuteAction(fallbackAction);
            }
        }

        /// <summary>
        /// 위협이 긴급하지 않을 때 3코스트 이상 고가치 카드를 출격시키기 위해 잉크를 비축해야 하는지 판단합니다.
        /// </summary>
        private bool ShouldSaveInkForHighCostCard(int currentInk)
        {
            if (currentInk >= 3) return false; // 이미 3 Ink 이상 모였으면 저축 대기 불필요

            // 유저 위협도가 2 이상이면 긴급 상황이므로 대기하지 않고 즉시 응수
            if (_evaluator != null && _evaluator.ThreatLevel >= 2.0f) return false;

            // 손패 중 3~5코스트 카드가 존재하는지 확인
            bool hasHighCostCard = false;
            for (int i = 0; i < _handCards.Count; i++)
            {
                int cost = _planner.GetInkCost(_handCards[i]);
                if (cost >= 3)
                {
                    hasHighCostCard = true;
                    break;
                }
            }

            // 손패에 3코스트 이상 카드가 있고 위협이 전면적이지 않다면 잉크가 3이 될 때까지 대기
            return hasHighCostCard && currentInk < 3;
        }

        /// <summary>
        /// [3단계 리팩토링 핵심] 최저 코스트 고정 선택 버그를 개선하여 소환 가능한 유닛 카드 중 가중치 무작위 선택으로 다채롭게 순환합니다.
        /// </summary>
        private AISpawnAction PlanFallbackSpawn(int currentInk)
        {
            AISpawnAction action = new AISpawnAction { IsValid = false };
            if (_evaluator != null && _evaluator.IsPopCapReached) return action;

            List<BattleObjectType> availableFallbackUnits = new List<BattleObjectType>();
            for (int i = 0; i < _handCards.Count; i++)
            {
                BattleObjectType card = _handCards[i];
                if (_planner.IsUnitCard(card))
                {
                    int cost = _planner.GetInkCost(card);
                    if (currentInk >= cost)
                    {
                        availableFallbackUnits.Add(card);
                    }
                }
            }

            if (availableFallbackUnits.Count > 0 && _planner != null)
            {
                int randIdx = UnityEngine.Random.Range(0, availableFallbackUnits.Count);
                BattleObjectType chosenType = availableFallbackUnits[randIdx];

                int targetLineX = (_evaluator != null) ? _evaluator.MostDangerousLineX : _targetUserLineX;
                Vector2Int spawnTile = Vector2Int.zero;

                action.ObjectType = chosenType;
                action.TargetTile = spawnTile;
                action.InkCost = _planner.GetInkCost(chosenType);
                action.IsValid = true;
            }

            return action;
        }

        private void ExecuteAction(AISpawnAction action)
        {
            _currentInk -= action.InkCost;
            _currentInk = Mathf.Max(0f, _currentInk);

            ConsumeAndDrawCard(action.ObjectType);

            float minInterval = (_aiData != null) ? _aiData.MinActionInterval : 0.2f;
            _actionCooldownTimer = minInterval;

            if (_planner != null && action.ObjectType != BattleObjectType.InkBomb)
            {
                _planner.RegisterOccupiedSpawnTile(action.TargetTile);
            }

            OnAIActionExecuted?.Invoke(action);
        }

        public void TryInkReroll()
        {
            if (_currentInk < 1f || _rerollCooldownTimer > 0f) return;

            _currentInk -= 1f;
            _rerollCooldownTimer = 2.0f;

            RerollHandCards();
        }

        #region Hand Deck Management
        private void InitializeHandDeck(List<BattleObjectType> unlockedDeck)
        {
            _unlockedDeckPool.Clear();
            _handCards.Clear();

            if (unlockedDeck != null && unlockedDeck.Count > 0)
            {
                _unlockedDeckPool.AddRange(unlockedDeck);
            }
            else
            {
                _unlockedDeckPool.Add(BattleObjectType.Footman);
                _unlockedDeckPool.Add(BattleObjectType.Archer);
                _unlockedDeckPool.Add(BattleObjectType.EliteUnit);
                _unlockedDeckPool.Add(BattleObjectType.Warlord);
                _unlockedDeckPool.Add(BattleObjectType.Barricade);
                _unlockedDeckPool.Add(BattleObjectType.Cannon);
                _unlockedDeckPool.Add(BattleObjectType.Mortar);
                _unlockedDeckPool.Add(BattleObjectType.InkBomb);
            }

            RerollHandCards();
        }

        private void RerollHandCards()
        {
            _handCards.Clear();
            List<BattleObjectType> poolCopy = new List<BattleObjectType>(_unlockedDeckPool);

            int drawCount = Mathf.Min(4, poolCopy.Count);
            for (int i = 0; i < drawCount; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, poolCopy.Count);
                _handCards.Add(poolCopy[randomIndex]);
                poolCopy.RemoveAt(randomIndex);
            }
        }

        private void ConsumeAndDrawCard(BattleObjectType usedCard)
        {
            if (_handCards.Contains(usedCard))
            {
                _handCards.Remove(usedCard);

                List<BattleObjectType> candidates = new List<BattleObjectType>(_unlockedDeckPool);
                for (int i = 0; i < _handCards.Count; i++)
                {
                    candidates.Remove(_handCards[i]);
                }

                if (candidates.Count > 0)
                {
                    int randIdx = UnityEngine.Random.Range(0, candidates.Count);
                    _handCards.Add(candidates[randIdx]);
                }
                else
                {
                    _handCards.Add(usedCard);
                }
            }
        }

        public void UpdatePerceivedTargetInfo(List<Vector2Int> blueUnitPositions, int targetUserLineX)
        {
            _blueUnitPositions = blueUnitPositions ?? new List<Vector2Int>();
            _targetUserLineX = targetUserLineX;
        }
        #endregion
    }
}
