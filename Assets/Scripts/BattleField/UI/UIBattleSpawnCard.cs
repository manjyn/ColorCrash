using UnityEngine;
using UnityEngine.UI;
using ColorCrash.Units;
using TMPro;

public class UIBattleSpawnCard : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI textName;
    [SerializeField] private TeamColor teamColor = TeamColor.Blue;
    [SerializeField] private BattleObjectType battleObjType = BattleObjectType.None;

    private UnitBase unitPrefab = null;
    private TowerBase towerPrefab = null;
    private bool isSpellCard = false;

    public BattleObjectType CurrentObjectType => battleObjType;
    public TeamColor CardTeamColor => teamColor;

    /// <summary>
    /// 카드가 클릭되었을 때 슬롯 컨트롤러(UIBattleObjCardList)에 알리는 콜백
    /// </summary>
    public System.Action<UIBattleSpawnCard> OnCardClicked;

    private void Start()
    {
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
        }

        RefreshCardInfo();
    }

    private void OnEnable()
    {
        if (BattleInkManager.Instance != null)
        {
            BattleInkManager.Instance.OnInkChanged += OnInkChanged;
        }
        UpdateCardState();
    }

    private void OnDisable()
    {
        if (BattleInkManager.Instance != null)
        {
            BattleInkManager.Instance.OnInkChanged -= OnInkChanged;
        }
    }

    public void SetCardType(BattleObjectType type)
    {
        battleObjType = type;
        RefreshCardInfo();
    }

    public void RefreshCardInfo()
    {
        unitPrefab = null;
        towerPrefab = null;
        isSpellCard = false;

        if (textName != null)
        {
            switch (battleObjType)
            {
                case BattleObjectType.None:
                    textName.text = "";
                    break;
                case BattleObjectType.Footman:
                    textName.text = "Footman";
                    if (UnitManager.Instance != null) unitPrefab = UnitManager.Instance.unitPrefabFootman;
                    break;
                case BattleObjectType.EliteUnit:
                    textName.text = "Elite";
                    if (UnitManager.Instance != null) unitPrefab = UnitManager.Instance.unitPrefabElite;
                    break;
                case BattleObjectType.Warlord:
                    textName.text = "Warlord";
                    if (UnitManager.Instance != null) unitPrefab = UnitManager.Instance.unitPrefabWarlord;
                    break;
                case BattleObjectType.Archer:
                    textName.text = "Archer";
                    if (UnitManager.Instance != null) unitPrefab = UnitManager.Instance.unitPrefabArcher;
                    break;
                case BattleObjectType.Barricade:
                    textName.text = "Barricade";
                    if (TowerManager.Instance != null) towerPrefab = TowerManager.Instance.towerPrefabBarricade;
                    break;
                case BattleObjectType.Cannon:
                    textName.text = "Cannon";
                    if (TowerManager.Instance != null) towerPrefab = TowerManager.Instance.towerPrefabCannon;
                    break;
                case BattleObjectType.Mortar:
                    textName.text = "Mortar";
                    if (TowerManager.Instance != null) towerPrefab = TowerManager.Instance.towerPrefabMortar;
                    break;
                case BattleObjectType.InkBomb:
                    textName.text = "Ink Bomb";
                    isSpellCard = true;
                    break;
                default:
                    textName.text = battleObjType.ToString();
                    break;
            }
        }

        UpdateCardState();
    }

    private void OnInkChanged(TeamColor team, float currentInk, float maxInk)
    {
        if (team == teamColor)
        {
            UpdateCardState();
        }
    }

    public void UpdateCardState()
    {
        if (button == null) return;

        if (battleObjType == BattleObjectType.None)
        {
            button.interactable = false;
            return;
        }

        // BattleContext에서 해당 객체의 생성 해금 여부 확인 (스펠 제외)
        /*var context = ColorCrash.CoreManager.Instance != null ? ColorCrash.CoreManager.Instance.CurrentBattleContext : null;
        if (context != null && !context.IsUnlocked(battleObjType) && !isSpellCard)
        {
            button.interactable = false;
            return;
        }*/

        if (BattleInkManager.Instance != null)
        {
            int cost = GetInkCost();
            button.interactable = BattleInkManager.Instance.CanSpendInk(teamColor, cost);
        }
        else
        {
            button.interactable = true;
        }
    }

    public int GetInkCost()
    {
        if (BattleInkManager.Instance != null)
            return BattleInkManager.Instance.GetInkCost(battleObjType);

        if (isSpellCard)
            return 3;

        return 0;
    }

    private void OnButtonClick()
    {
        if (battleObjType == BattleObjectType.None) return;

        var context = ColorCrash.CoreManager.Instance != null ? ColorCrash.CoreManager.Instance.CurrentBattleContext : null;
        if (context != null && !context.IsUnlocked(battleObjType) && !isSpellCard)
        {
            Debug.LogWarning($"[UIBattleSpawnCard] {battleObjType}은(는) 이번 전투에서 잠겨있어 생성할 수 없습니다.");
            return;
        }

        int cost = GetInkCost();

        // 유닛 카드는 전장 클릭 배치 모드를 생략하고 스폰 존에 즉시 자동 생성
        if (unitPrefab != null)
        {
            // 1. 최대 인구수 제한 검사 (팀당 최대 인구수 = 10 + N)
            if (UnitManager.Instance != null)
            {
                int activeCount = (teamColor == TeamColor.Blue) ? UnitManager.Instance.activeBlueUnits.Count : UnitManager.Instance.activeRedUnits.Count;
                int unitScaleParam = context != null ? context.StageUnitScale : 15;
                int maxCap = 10 + unitScaleParam;

                if (activeCount >= maxCap)
                {
                    Debug.LogWarning($"[UIBattleSpawnCard] 팀 최대 인구수 제한({maxCap}기)에 도달하여 더 이상 유닛을 생성할 수 없습니다. 현재: {activeCount}기");
                    return;
                }
            }

            // 2. 잉크 검사 및 차감
            if (BattleInkManager.Instance != null)
            {
                if (!BattleInkManager.Instance.TrySpendInk(teamColor, cost))
                {
                    Debug.LogWarning($"[UIBattleSpawnCard] 잉크가 부족하여 소환할 수 없습니다. 필요: {cost}, 보유: {BattleInkManager.Instance.GetCurrentInkInt(teamColor)}");
                    return;
                }
            }

            // 3. 스폰 위치 산출 및 유닛 즉시 스폰
            Vector3 spawnPos = Vector3.zero;
            if (GridManager.Instance != null)
            {
                spawnPos = GridManager.Instance.GetRandomSpawnPosition(teamColor);
            }

            if (UnitManager.Instance != null)
            {
                UnitManager.Instance.SpawnUnit(unitPrefab, teamColor, spawnPos);
                Debug.Log($"[UIBattleSpawnCard] {unitPrefab.name} 유닛이 스폰 구역({spawnPos})에 자동 배치되었습니다.");
            }

            // 4. 손패 순환 트리거
            OnCardClicked?.Invoke(this);
            return;
        }

        // 건물 및 마법 카드는 기존의 마우스 클릭/드래그 배치 모드 진행
        if (BattleInkManager.Instance != null && !BattleInkManager.Instance.CanSpendInk(teamColor, cost))
        {
            Debug.LogWarning($"[UIBattleSpawnCard] 잉크가 부족하여 소환할 수 없습니다. 필요: {cost}, 보유: {BattleInkManager.Instance.GetCurrentInkInt(teamColor)}");
            return;
        }

        if (BattleUIManager.Instance != null)
        {
            if (towerPrefab != null)
            {
                ISpawnHandler handler = new TowerSpawnHandler(towerPrefab);
                BattleUIManager.Instance.SelectObjectToSpawn(towerPrefab.gameObject, handler, teamColor, cost, () => OnCardClicked?.Invoke(this));
            }
            else if (isSpellCard)
            {
                int radius = ColorCrash.Spell.SpellDatabase.GetSpellRadius("InkBomb", 1);
                float damage = ColorCrash.Spell.SpellDatabase.GetSpellDamage("InkBomb", 50f);
                ISpawnHandler handler = new SpellSpawnHandler(radius, damage); // DB 데이터 적용
                
                // 더미용 임시 오브젝트 (없으면 null 처리 지원)
                GameObject spellDummy = new GameObject("InkBombPreviewDummy");
                BattleUIManager.Instance.SelectObjectToSpawn(spellDummy, handler, teamColor, cost);
                Destroy(spellDummy, 0.1f);
            }
        }
    }
}
