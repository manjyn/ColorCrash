using System.Collections.Generic;
using UnityEngine;

namespace ColorCrash.AI
{
    /// <summary>
    /// AI의 실시간 런타임 상태(점령률, 잉크, 4-Phase, 손패, 최근 행동)를 유니티 인스펙터에서 시각화 표시하는 디버거 컴포넌트입니다.
    /// </summary>
    public class BattleAIDebugger : MonoBehaviour
    {
        [Header("연동 AI 컴포넌트")]
        [SerializeField] private EnemyAIController _aiController;
        [SerializeField] private BattlefieldEvaluator _evaluator;

        [Header("실시간 AI 런타임 모니터링 (Read-Only)")]
        [SerializeField] private string _profileName = "None";
        [SerializeField] private string _currentPhaseName = "Phase1_Opening";
        [SerializeField] private float _territoryRatio = 0f;
        [SerializeField] private int _frontlineY = 0;
        [SerializeField] private float _threatLevel = 0f;
        [SerializeField] private float _currentInk = 0f;
        [SerializeField] private int _maxInkCap = 10;
        [SerializeField] private bool _isPopCapReached = false;
        [SerializeField] private bool _isCrisisState = false;
        [SerializeField] private string _handCardsSummary = "Empty";
        [SerializeField] private string _lastActionSummary = "No Action Executed";

        private void Awake()
        {
            if (_aiController == null) _aiController = GetComponent<EnemyAIController>();
            if (_evaluator == null) _evaluator = GetComponent<BattlefieldEvaluator>();
        }

        private void OnEnable()
        {
            if (_aiController != null)
            {
                _aiController.OnAIActionExecuted += HandleAIActionExecuted;
            }
        }

        private void OnDisable()
        {
            if (_aiController != null)
            {
                _aiController.OnAIActionExecuted -= HandleAIActionExecuted;
            }
        }

        private void Update()
        {
            RefreshMonitoringData();
        }

        /// <summary>
        /// 인스펙터 모니터링용 읽기 전용 데이터를 실시간 갱신합니다.
        /// </summary>
        private void RefreshMonitoringData()
        {
            if (_aiController != null)
            {
                if (_aiController.AIData != null)
                {
                    _profileName = _aiController.AIData.ProfileName;
                }

                _currentPhaseName = _aiController.CurrentPhase.ToString();
                _currentInk = Mathf.Round(_aiController.CurrentInk * 10f) / 10f;
                _maxInkCap = _aiController.MaxInkCap;
                _isCrisisState = _aiController.IsCrisisState;

                // 손패 요약 갱신
                IReadOnlyList<BattleObjectType> hand = _aiController.HandCards;
                if (hand != null && hand.Count > 0)
                {
                    _handCardsSummary = string.Join(", ", hand);
                }
                else
                {
                    _handCardsSummary = "Empty";
                }
            }

            if (_evaluator != null)
            {
                _territoryRatio = Mathf.Round(_evaluator.TerritoryRatio * 10f) / 10f;
                _frontlineY = _evaluator.FrontlineY;
                _threatLevel = _evaluator.ThreatLevel;
                _isPopCapReached = _evaluator.IsPopCapReached;
            }
        }

        /// <summary>
        /// AI 소환/배치/스펠 행동이 발생했을 때 디버거 텍스트를 업데이트합니다.
        /// </summary>
        private void HandleAIActionExecuted(AISpawnAction action)
        {
            if (action.IsValid)
            {
                _lastActionSummary = $"[{Time.time:F1}s] {action.ObjectType} -> Tile ({action.TargetTile.x}, {action.TargetTile.y}) | Ink Used: {action.InkCost}";
            }
        }
    }
}
