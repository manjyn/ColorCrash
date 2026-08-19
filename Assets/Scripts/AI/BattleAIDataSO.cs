using System.Collections.Generic;
using UnityEngine;

namespace ColorCrash.AI
{
    public enum AIArchetype
    {
        AgroPainter,     // 영토 선점형 (소형 맵 전용)
        BunkerFortress,  // 요새 방어형 (중/대형 맵 전용)
        AdaptiveCounter  // 유저 카운터형 (정예/보스전)
    }

    [CreateAssetMenu(fileName = "BattleAIData_", menuName = "ColorCrash/Battle AI Data Profile")]
    public class BattleAIDataSO : ScriptableObject
    {
        [Header("기본 성향 설정")]
        public string ProfileName = "Normal_Bunker";
        public AIArchetype Archetype = AIArchetype.BunkerFortress;

        [Header("의사결정 및 자원 파라미터")]
        [Tooltip("전황을 평가하고 다음 행동을 결정하는 의사결정 평가 주기 (초)")]
        public float EvaluationInterval = 0.8f;

        [Tooltip("카드 간 연속 소환 최소 딜레이 간격 (초)")]
        public float MinActionInterval = 0.2f;

        [Tooltip("기본 잉크 충전 가중치 배율")]
        public float InkMultiplier = 1.0f;

        [Tooltip("Phase 4 컬러 피버 시 잉크 충전 및 행동 가속 배율")]
        public float FeverTempoMultiplier = 1.5f;

        [Tooltip("AI 점령률이 이 수치 미만으로 떨어지면 위기/분노 모드 발동 (0.0~1.0)")]
        [Range(0f, 1f)]
        public float CrisisTerritoryThreshold = 0.35f;

        [Tooltip("유저 카운터 카드/스펠(Ink Bomb) 시전 성공 확률 (0.0~1.0)")]
        [Range(0f, 1f)]
        public float CounterChance = 0.6f;

        [Tooltip("카운터 카드가 손패에 없을 때 1 Ink 리롤 활용 확률 (0.0~1.0)")]
        [Range(0f, 1f)]
        public float RerollChance = 0.3f;

        [Header("유닛 vs 건물 소환 비율 밸런스 설정")]
        [Tooltip("유닛 대 건물 최소 목표 비율 (예: 2.5 -> 유닛 2.5기당 건물 1개 비율)")]
        public float MinUnitBuildingRatio = 2.5f;

        [Tooltip("유닛 대 건물 최대 목표 비율 (예: 4.5 -> 유닛 4.5기당 건물 1개 비율)")]
        public float MaxUnitBuildingRatio = 4.5f;

        [Header("선호 카드 덱 설정")]
        public List<BattleObjectType> PreferredUnitCards = new List<BattleObjectType> { BattleObjectType.Footman, BattleObjectType.EliteUnit };
        public List<BattleObjectType> PreferredSpellCards = new List<BattleObjectType> { BattleObjectType.InkBomb };

        [Header("타겟팅 정밀도")]
        [Tooltip("타일 배치 목표 지점 오차 범위 (0: 100% 최적, 1~2: 오차 발생)")]
        public int TileTargetOffset = 1;
    }
}
