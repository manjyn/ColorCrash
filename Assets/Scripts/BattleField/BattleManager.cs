using System.Collections.Generic;
using UnityEngine;
using ColorCrash;
using ColorCrash.AI;
using ColorCrash.Units;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("Red Team AI")]
    [SerializeField] private EnemyAIController _enemyAIController = null;
    [SerializeField] private BattleAIDataSO _defaultAIData = null;

    [Header("Battle Rules")]
    private float maxBattleTime = 180f; // 전투 시간 제한
    private float timeRemaining;
    private float uiUpdateTimer = 0f;
    private bool isBattleActive = false;

    private BattleContext currentContext;

    public EnemyAIController AIController => _enemyAIController;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        InitializeBattle();
    }

    private void InitializeBattle()
    {
        if (CoreManager.Instance != null)
        {
            currentContext = CoreManager.Instance.CurrentBattleContext;
        }

        if (currentContext == null)
        {
            Debug.LogWarning("[BattleManager] BattleContext가 없습니다. 에디터 테스트용 더미 데이터를 생성합니다.");
            currentContext = new BattleContext
            {
                AttackerWorldTileX = 0, AttackerWorldTileY = 0,
                TargetWorldTileX = 1, TargetWorldTileY = 1,
                AttackerUnitCount = 50, DefenderUnitCount = 10, IsResolved = false
            };
            
            if (CoreManager.Instance != null)
            {
                CoreManager.Instance.CurrentBattleContext = currentContext;
            }
        }

        MapStageConfig config = currentContext.GetMapConfig();

        if (Camera.main != null && Camera.main.orthographic)
        {
            Camera.main.orthographicSize = config.Projection;
        }

        if (GridManager.Instance != null)
        {
            GridManager.Instance.InitGrid(config);
        }

        maxBattleTime = config.BattleTime;
        timeRemaining = config.BattleTime;
        isBattleActive = true;
        uiUpdateTimer = 0f;

        if (BattleInkManager.Instance == null)
        {
            gameObject.AddComponent<BattleInkManager>();
        }
        
        int unitScale = currentContext.StageUnitScale;
        BattleInkManager.Instance.Initialize(unitScale);

        if (BattleUIManager.Instance != null)
        {
            BattleUIManager.Instance.OnUpdateTimer((int)timeRemaining);
        }

        // ==========================================
        // [AI 연동 핵심 1] 적 AI 세션 시작 및 소환 이벤트 바인딩
        // ==========================================
        if (_enemyAIController == null)
        {
            _enemyAIController = FindFirstObjectByType<EnemyAIController>();
        }

        if (_enemyAIController != null)
        {
            // Red 팀 AI 전용 해금 덱 풀
            List<BattleObjectType> aiUnlockedDeck = new List<BattleObjectType>();
            if (currentContext.UnlockSettings.UnlockFootman) aiUnlockedDeck.Add(BattleObjectType.Footman);
            if (currentContext.UnlockSettings.UnlockArcher) aiUnlockedDeck.Add(BattleObjectType.Archer);
            if (currentContext.UnlockSettings.UnlockElite) aiUnlockedDeck.Add(BattleObjectType.EliteUnit);
            if (currentContext.UnlockSettings.UnlockWarlord) aiUnlockedDeck.Add(BattleObjectType.Warlord);
            if (currentContext.UnlockSettings.UnlockBarricade) aiUnlockedDeck.Add(BattleObjectType.Barricade);
            if (currentContext.UnlockSettings.UnlockCannon) aiUnlockedDeck.Add(BattleObjectType.Cannon);
            if (currentContext.UnlockSettings.UnlockMortar) aiUnlockedDeck.Add(BattleObjectType.Mortar);

            int gridLevel = currentContext.StageGridLevel;
            _enemyAIController.InitializeSession(_defaultAIData, config.BattleTime, gridLevel, unitScale, aiUnlockedDeck, 
                (pos) => GridManager.Instance != null ? GridManager.Instance.tileStates[pos.y * config.Width + pos.x] : TeamColor.Neutral,
                (pos) => GridManager.Instance != null ? GridManager.Instance.isObstacleTile[pos.y * config.Width + pos.x] : false);

            _enemyAIController.OnAIActionExecuted += HandleAIActionExecuted;
        }
    }

    /// <summary>
    /// Battle AI 연동 - Red 팀 인공지능 액션 처리
    /// </summary>
    private void HandleAIActionExecuted(AISpawnAction action)
    {
        if (!action.IsValid) return;

        Debug.Log($"[BattleManager] AI 소환 명령 실행: {action.ObjectType} -> 타일 ({action.TargetTile.x}, {action.TargetTile.y})");

        UnitBase unitPrefab = null;
        TowerBase towerPrefab = null;

        switch (action.ObjectType)
        {
            case BattleObjectType.Footman: if (UnitManager.Instance != null) unitPrefab = UnitManager.Instance.unitPrefabFootman; break;
            case BattleObjectType.EliteUnit: if (UnitManager.Instance != null) unitPrefab = UnitManager.Instance.unitPrefabElite; break;
            case BattleObjectType.Warlord: if (UnitManager.Instance != null) unitPrefab = UnitManager.Instance.unitPrefabWarlord; break;
            case BattleObjectType.Archer: if (UnitManager.Instance != null) unitPrefab = UnitManager.Instance.unitPrefabArcher; break;

            case BattleObjectType.Barricade: if (TowerManager.Instance != null) towerPrefab = TowerManager.Instance.towerPrefabBarricade; break;
            case BattleObjectType.Cannon: if (TowerManager.Instance != null) towerPrefab = TowerManager.Instance.towerPrefabCannon; break;
            case BattleObjectType.Mortar: if (TowerManager.Instance != null) towerPrefab = TowerManager.Instance.towerPrefabMortar; break;

            case BattleObjectType.InkBomb:
                break;
        }

        if (unitPrefab != null && UnitManager.Instance != null)
        {
            Vector3 spawnWorldPos = GridManager.Instance.GetRandomSpawnPosition(TeamColor.Red);
            UnitManager.Instance.SpawnUnit(unitPrefab, TeamColor.Red, spawnWorldPos);
        }
        else if (towerPrefab != null && GridManager.Instance != null)
        {
            int tileIndex = action.TargetTile.y * GridManager.Instance.width + action.TargetTile.x;
            Vector3 originWorldPos = GridManager.Instance.IndexToWorld(tileIndex);

            int bSizeX = (action.ObjectType == BattleObjectType.Barricade) ? 4 : 2;
            int bSizeY = (action.ObjectType == BattleObjectType.Barricade) ? 1 : 2;

            // 3D 건물 프리팹 정중앙 피벗 좌표 보정 (4x1, 2x2 영역의 정확한 3D 중앙)
            float halfOffsetX = (bSizeX - 1) * (GridManager.tileSize * 0.5f);
            float halfOffsetY = (bSizeY - 1) * (GridManager.tileSize * 0.5f);
            Vector3 exactBuildingCenterPos = new Vector3(originWorldPos.x - halfOffsetX, originWorldPos.y, originWorldPos.z - halfOffsetY);

            if (TowerManager.Instance != null)
            {
                TowerBase spawnedBuilding = TowerManager.Instance.SpawnTower(towerPrefab, exactBuildingCenterPos, TeamColor.Red);

                for (int x = 0; x < bSizeX; x++)
                {
                    for (int y = 0; y < bSizeY; y++)
                    {
                        int fillX = action.TargetTile.x + x;
                        int fillY = action.TargetTile.y + y;
                        if (fillX >= 0 && fillX < GridManager.Instance.width && fillY >= 0 && fillY < GridManager.Instance.height)
                        {
                            Vector3 tileWorld = GridManager.Instance.IndexToWorld(fillY * GridManager.Instance.width + fillX);
                            GridManager.Instance.PaintTile(tileWorld, TeamColor.Red);
                            int fillIdx = fillY * GridManager.Instance.width + fillX;
                            if (GridManager.Instance.gridBuildings != null && fillIdx >= 0 && fillIdx < GridManager.Instance.gridBuildings.Length)
                            {
                                GridManager.Instance.gridBuildings[fillIdx] = spawnedBuilding;
                            }
                        }
                    }
                }
            }
            else
            {
                Debug.Log($"[BattleManager] 건물 스폰 실패: TowerManager.Instance가 null입니다.");
            }
        }
    }

    private void Update()
    {
        if (!isBattleActive) return;

        timeRemaining -= Time.deltaTime;

        // ==========================================
        // [AI 연동 핵심 2] 매 프레임 AI 유저 타깃 위치 전달
        // ==========================================
        if (_enemyAIController != null)
        {
            _enemyAIController.UpdatePerceivedTargetInfo(GetBlueUnitPositions(), 7);
        }

        float elapsedTime = maxBattleTime - timeRemaining;
        float progressRatio = maxBattleTime > 0 ? elapsedTime / maxBattleTime : 0f;
        int currentPhase = 1;
        if (progressRatio >= 0.8f) currentPhase = 4;
        else if (progressRatio >= 0.5f) currentPhase = 3;
        else if (progressRatio >= 0.2f) currentPhase = 2;

        float blueRatio = 0.5f;
        float redRatio = 0.5f;
        if (GridManager.Instance != null)
        {
            int blueCount = GridManager.Instance.blueTileIndices.Count;
            int redCount = GridManager.Instance.redTileIndices.Count;
            int total = blueCount + redCount;
            if (total > 0)
            {
                blueRatio = (float)blueCount / total;
                redRatio = (float)redCount / total;
            }
        }

        if (BattleInkManager.Instance != null)
        {
            BattleInkManager.Instance.UpdateBattleState(currentPhase, blueRatio, redRatio);
        }

        uiUpdateTimer += Time.deltaTime;
        if (uiUpdateTimer >= 1f)
        {
            uiUpdateTimer = 0f;
            if (BattleUIManager.Instance != null)
            {
                BattleUIManager.Instance.OnUpdateTimer((int)timeRemaining);
            }
        }

        CheckBattleConditions();
    }

    private List<Vector2Int> GetBlueUnitPositions()
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        int footmanCount = 0;
        int archerCount = 0;
        int eliteCount = 0;

        if (UnitManager.Instance != null && UnitManager.Instance.activeBlueUnits != null)
        {
            for (int i = 0; i < UnitManager.Instance.activeBlueUnits.Count; i++)
            {
                var u = UnitManager.Instance.activeBlueUnits[i];
                if (u != null && u.gameObject.activeInHierarchy)
                {
                    Vector2Int tilePos;
                    if (GridManager.Instance != null)
                    {
                        int tileIdx = GridManager.Instance.WorldToIndex(u.transform.position);
                        if (tileIdx != -1)
                        {
                            tilePos = new Vector2Int(tileIdx % GridManager.Instance.width, tileIdx / GridManager.Instance.width);
                        }
                        else
                        {
                            tilePos = new Vector2Int(Mathf.RoundToInt(u.transform.position.x), Mathf.RoundToInt(u.transform.position.z));
                        }
                    }
                    else
                    {
                        tilePos = new Vector2Int(Mathf.RoundToInt(u.transform.position.x), Mathf.RoundToInt(u.transform.position.z));
                    }
                    positions.Add(tilePos);

                    string nameLower = u.name.ToLower();
                    if (nameLower.Contains("footman")) footmanCount++;
                    else if (nameLower.Contains("archer")) archerCount++;
                    else if (nameLower.Contains("elite") || nameLower.Contains("warlord")) eliteCount++;
                    else footmanCount++;
                }
            }
        }

        // AI 평가기에 Red팀 건물 및 Blue팀 유닛 타입별 세부 스캔 전달
        int redBarricade = 0;
        int redCannon = 0;
        int redMortar = 0;

        TowerBase[] activeTowers = FindObjectsByType<TowerBase>(FindObjectsSortMode.None);
        if (activeTowers != null)
        {
            for (int i = 0; i < activeTowers.Length; i++)
            {
                TowerBase t = activeTowers[i];
                if (t != null && !t.IsDead && t.gameObject.activeInHierarchy && t.TeamColor == TeamColor.Red)
                {
                    string tName = t.name.ToLower();
                    if (tName.Contains("barricade")) redBarricade++;
                    else if (tName.Contains("cannon")) redCannon++;
                    else if (tName.Contains("mortar")) redMortar++;
                }
            }
        }

        var evaluator = FindFirstObjectByType<ColorCrash.AI.BattlefieldEvaluator>();
        if (evaluator != null)
        {
            evaluator.UpdateRedBuildingDetails(redBarricade, redCannon, redMortar);
            evaluator.UpdateBlueComposition(footmanCount, archerCount, eliteCount);
            evaluator.EvaluateThreatLevel(positions);
        }

        return positions;
    }

    private void CheckBattleConditions()
    {
        if (GridManager.Instance == null) return;

        if (GridManager.Instance.redTileIndices.Count == 0)
        {
            ResolveBattle(true);
            return;
        }

        if (GridManager.Instance.blueTileIndices.Count == 0)
        {
            timeRemaining = 0;
            ResolveBattle(false);
            return;
        }

        if (timeRemaining <= 0)
        {
            timeRemaining = 0;
            int totalTiles = GridManager.Instance.totalTiles;
            float blueRatio = totalTiles > 0 ? (float)GridManager.Instance.blueTileIndices.Count / totalTiles : 0f;
            float targetRatio = currentContext != null ? currentContext.DecisionVictoryRatio : 0.7f;
            ResolveBattle(blueRatio >= targetRatio);
        }
    }

    public void ForfeitBattle()
    {
        if (!isBattleActive) return;
        ResolveBattle(false);
    }

    public void ResolveBattle(bool isVictory)
    {
        isBattleActive = false;
        if (BattleInkManager.Instance != null)
        {
            BattleInkManager.Instance.SetBattleActive(false);
        }
        
        if (currentContext != null)
        {
            currentContext.IsResolved = true;
            currentContext.IsPlayerVictory = isVictory;
            float survivalRate = Mathf.Max(0.2f, Mathf.Sqrt(timeRemaining / maxBattleTime));
            currentContext.SurvivedAttackerUnitCount = Mathf.RoundToInt(currentContext.AttackerUnitCount * survivalRate);
        }

        EndBattle();
    }

    public void EndBattle()
    {
        if (CoreManager.Instance != null)
        {
            CoreManager.Instance.UnloadBattleScene();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(Define.Scene.Menu);
        }
    }
}
