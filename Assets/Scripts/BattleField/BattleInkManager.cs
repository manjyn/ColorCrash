using System;
using UnityEngine;
using ColorCrash;
using ColorCrash.Units;
using ColorCrash.Tower;

/// <summary>
/// 전투 중 잉크(Ink) 자원 충전, 검증, 차감 및 Phase/점령율 보정을 총괄하는 전용 매니저 클래스
/// </summary>
public class BattleInkManager : MonoBehaviour
{
    public static BattleInkManager Instance { get; private set; }

    public const float MAX_INK = 10f;
    public const float STARTING_INK = 4f;

    [Header("Current Ink Status")]
    [SerializeField] private float blueInk = STARTING_INK;
    [SerializeField] private float redInk = STARTING_INK;

    [Header("Settings & Multipliers")]
    private int stageUnitScale = 5;
    private int currentPhase = 1;
    private float blueTerritoryRatio = 0.5f;
    private float redTerritoryRatio = 0.5f;
    private bool isBattleActive = true;

    /// <summary>
    /// 잉크 변경 이벤트 (팀 대상, 현재 잉크, 최대 잉크)
    /// </summary>
    public event Action<TeamColor, float, float> OnInkChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        isBattleActive = true;
    }

    /// <summary>
    /// 전투 시작 시 유닛 규모 인자(N)를 받아 잉크 시스템을 초기화합니다.
    /// </summary>
    public void Initialize(int unitScale)
    {
        stageUnitScale = Mathf.Clamp(unitScale, 1, 30);
        blueInk = STARTING_INK;
        redInk = STARTING_INK;
        currentPhase = 1;
        blueTerritoryRatio = 0.5f;
        redTerritoryRatio = 0.5f;
        isBattleActive = true;

        // 초기 보유 잉크 갱신 이벤트 발행
        OnInkChanged?.Invoke(TeamColor.Blue, blueInk, MAX_INK);
        OnInkChanged?.Invoke(TeamColor.Red, redInk, MAX_INK);

        Debug.Log($"[BattleInkManager] 잉크 시스템 초기화 완료. N={stageUnitScale}, 초기 잉크={STARTING_INK}/{MAX_INK}");
    }

    public void SetBattleActive(bool active)
    {
        isBattleActive = active;
    }

    public void UpdateBattleState(int phase, float blueRatio, float redRatio)
    {
        currentPhase = phase;
        blueTerritoryRatio = blueRatio;
        redTerritoryRatio = redRatio;
    }

    private void Update()
    {
        if (!isBattleActive) return;

        RegenInk(TeamColor.Blue, ref blueInk, blueTerritoryRatio);
        RegenInk(TeamColor.Red, ref redInk, redTerritoryRatio);
    }

    /// <summary>
    /// 프레임 기반 팀 잉크 리젠 계산
    /// </summary>
    private void RegenInk(TeamColor team, ref float inkRef, float territoryRatio)
    {
        if (inkRef >= MAX_INK) return;

        float regenSpeed = GetCurrentRegenSpeed(territoryRatio);
        float prevInk = inkRef;
        inkRef = Mathf.Min(MAX_INK, inkRef + regenSpeed * Time.deltaTime);

        if (prevInk != inkRef)
        {
            OnInkChanged?.Invoke(team, inkRef, MAX_INK);
        }
    }

    /// <summary>
    /// 현재 조건(N 인자, Phase 4 피버, 점령율 40% 미만 분노의 잉크)을 종합하여 초당 잉크 충전 속도를 산출합니다.
    /// </summary>
    public float GetCurrentRegenSpeed(float territoryRatio)
    {
        float baseSpeed;

        // Phase 4 (컬러 피버): 0.5초당 1 Ink (초당 2.0 Ink)
        if (currentPhase >= 4)
        {
            baseSpeed = 2.0f;
        }
        else
        {
            // 기본 충전 주기(초) = 1.2초 - (N * 0.02초)
            float chargeInterval = Mathf.Max(0.6f, 1.2f - (stageUnitScale * 0.02f));
            baseSpeed = 1.0f / chargeInterval;
        }

        // 열세 팀 스노우볼 방지 [분노의 잉크]: 점령율 40% 미만 시 30% 가속
        if (territoryRatio < 0.4f)
        {
            baseSpeed *= 1.3f;
        }

        return baseSpeed;
    }

    public float GetCurrentInk(TeamColor team)
    {
        return (team == TeamColor.Blue) ? blueInk : redInk;
    }

    public int GetCurrentInkInt(TeamColor team)
    {
        return Mathf.FloorToInt(GetCurrentInk(team));
    }

    public bool CanSpendInk(TeamColor team, int cost)
    {
        return GetCurrentInkInt(team) >= cost;
    }

    public bool TrySpendInk(TeamColor team, int cost)
    {
        if (!CanSpendInk(team, cost))
        {
            Debug.LogWarning($"[BattleInkManager] {team} 팀 잉크 부족! 필요: {cost}, 보유: {GetCurrentInkInt(team)}");
            return false;
        }

        if (team == TeamColor.Blue)
        {
            blueInk -= cost;
            OnInkChanged?.Invoke(TeamColor.Blue, blueInk, MAX_INK);
        }
        else
        {
            redInk -= cost;
            OnInkChanged?.Invoke(TeamColor.Red, redInk, MAX_INK);
        }

        Debug.Log($"[BattleInkManager] {team} 팀 {cost} Ink 소모 완료. 남은 잉크: {(team == TeamColor.Blue ? blueInk : redInk):F2}");
        return true;
    }

    public void AddInk(TeamColor team, float amount)
    {
        if (team == TeamColor.Blue)
        {
            blueInk = Mathf.Clamp(blueInk + amount, 0f, MAX_INK);
            OnInkChanged?.Invoke(TeamColor.Blue, blueInk, MAX_INK);
        }
        else
        {
            redInk = Mathf.Clamp(redInk + amount, 0f, MAX_INK);
            OnInkChanged?.Invoke(TeamColor.Red, redInk, MAX_INK);
        }
    }

    /// <summary>
    /// UnitDatabase 및 TowerDatabase를 참조하여 BattleObjectType의 잉크 소모 비용을 가져옵니다.
    /// </summary>
    public int GetInkCost(BattleObjectType type)
    {
        switch (type)
        {
            case BattleObjectType.Footman:
                return GetUnitInkCost("Footman", 2);
            case BattleObjectType.EliteUnit:
                return GetUnitInkCost("Elite", 3);
            case BattleObjectType.Warlord:
                return GetUnitInkCost("Warlord", 4);
            case BattleObjectType.Archer:
                return GetUnitInkCost("Archer", 3);
            case BattleObjectType.Barricade:
                return GetTowerInkCost("Barricade", 2);
            case BattleObjectType.Cannon:
                return GetTowerInkCost("Cannon", 4);
            case BattleObjectType.Mortar:
                return GetTowerInkCost("Mortar", 5);
            case BattleObjectType.InkBomb:
                return 3;
            default:
                return 0;
        }
    }

    public int GetUnitInkCost(string unitId, int fallbackCost)
    {
        if (UnitManager.Instance != null && UnitManager.Instance.UnitDatabase != null)
        {
            if (UnitManager.Instance.UnitDatabase.TryGetRecord(unitId, out UnitDataRecord record))
            {
                if (record.InkCost > 0) return record.InkCost;
            }
        }
        return fallbackCost;
    }

    public int GetTowerInkCost(string towerId, int fallbackCost)
    {
        if (TowerManager.Instance != null && TowerManager.Instance.TowerDatabase != null)
        {
            if (TowerManager.Instance.TowerDatabase.TryGetRecord(towerId, out TowerDataRecord record))
            {
                if (record.InkCost > 0) return record.InkCost;
            }
        }
        return fallbackCost;
    }
}
