using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ColorCrash.UI;

namespace ColorCrash.WorldMap
{
    public class WorldMapManager : MonoBehaviour
    {
        public static WorldMapManager Instance { get; private set; }

        // 월드맵 크기 최대 제한
        public const int MAX_MAP_WIDTH = 30;
        public const int MAX_MAP_HEIGHT = 30;

        [Header("Map Settings")]
        [SerializeField] private int mapWidth = 10;
        [SerializeField] private int mapHeight = 10;
        [SerializeField] private float tileRadius = 5f;
        [SerializeField] private WorldMapTile tilePrefab;
        [SerializeField] private Transform tileParent; // 생성될 타일들 컨테이너
        [SerializeField] private int instantiatesPerFrame = 50; // 한 프레임에 생성할 최대 타일 수

        [Header("Faction Settings")]
        [SerializeField] private int edgeMargin = 1; // 가장자리 구석 방지를 위한 마진 칸 수
        [SerializeField] private float minDistanceRatio = 0.5f; // 아군과 적군 간의 최소 이격 거리 비율

        [Header("Selection Settings")]
        [SerializeField] private GameObject selectionMarkerPrefab; // 타일 선택 마커 프리팹

        [Header("Resource Distribution Settings")]
        [Range(0f, 1f)] [SerializeField] private float mountainRatio = 0.05f;
        [Range(0f, 1f)] [SerializeField] private float goldMineRatio = 0.10f;
        [Range(0f, 1f)] [SerializeField] private float farmlandRatio = 0.15f;
        [Range(0f, 1f)] [SerializeField] private float pastureRatio = 0.10f;

        [Header("Bandit Distribution Settings")]
        [Range(0f, 1f)] [SerializeField] private float banditLv1Ratio = 0.08f;
        [Range(0f, 1f)] [SerializeField] private float banditLv2Ratio = 0.05f;
        [Range(0f, 1f)] [SerializeField] private float banditLv3Ratio = 0.02f;
        [Range(0f, 1f)] [SerializeField] private float banditLv4Ratio = 0.01f;

        // 원본 데이터 (Model)
        private TileOwnerState[] ownerStates;
        private TileFogState[] fogStates;

        // 뷰 컴포넌트 캐싱 배열
        private WorldMapTile[] spawnedTiles;
        private GameObject selectionMarkerInstance;
        private WorldMapTile currentlySelectedTile;

        // 본진 기지 글로벌 레퍼런스
        public WorldMapTile PlayerHQ { get; private set; }
        public WorldMapTile EnemyHQ { get; private set; }

        // 아군 유닛 제한 수치
        public int UnitLimit { get; private set; }      //최대 기본 유닛 수
        public int UnitAddLimit { get; private set; }   //최대 추가 유닛 수

        [Header("Redeploy Settings")]
        [SerializeField] private int redeployBaseAPCost = 1;
        public int RedeployBaseAPCost => redeployBaseAPCost;
        [SerializeField] private float redeployAPCostPerDistance = 0.5f;

        private bool isRedeployActive = false;
        private int redeployBaseTileX = -1;
        private int redeployBaseTileY = -1;
        private Dictionary<int, int> tileUnitCounts = new Dictionary<int, int>();

        [Header("Resource Develop Visual Config")]
        [SerializeField] private ResourceVisualConfig resourceVisualConfig;
        public ResourceVisualConfig ResourceVisualConfig => resourceVisualConfig;

        //[Header("Attack Settings")]
        //[SerializeField] private int baseAttackAPCost = 2;
        //[SerializeField] private float attackAPCostPerUnit = 0.5f;

        private bool isAttackPrepActive = false;
        private int attackTargetTileX = -1;
        private int attackTargetTileY = -1;
                
        private int unit; //보유 유닛 수
        public int Unit  
        { 
            get => unit; 
            private set
            {
                unit = value;
                OnUnitChanged?.Invoke(unit);
            }
        }
        public int unitsPerTurn { get; private set; }

        // 재화 및 턴 상태 프로퍼티
        public int Gold { get; private set; }
        public int GoldPerTurn { get; private set; }
        public int AP { get; private set; }
        public int APPerTurn { get; private set; }
        public int MaxAP => 9;
        public int APRecoveryAmount => 3;
        public int TurnCount { get; private set; }
        public TurnState CurrentTurnState { get; private set; }

        // 턴 및 재화 변경 이벤트
        public event Action<TurnState> OnTurnChanged;
        public event Action<int> OnGoldChanged;
        public event Action<int> OnAPChanged;
        public event Action<int> OnUnitChanged;

        private Coroutine mapGenerationCoroutine;

        /// <summary>
        /// 타일이 점령되었을 때 외부 시스템(UI, AI, 오디오 등)에 알리기 위한 이벤트
        /// </summary>
        public event Action<int, int, TileOwnerState> OnTileCaptured;

        /// <summary>
        /// 타일이 선택되었을 때 발생하는 이벤트 (카메라 이동, UI 팝업용)
        /// </summary>
        public event Action<int, int> OnTileSelected;
        
        /// <summary>
        /// 타일 선택이 해제되었을 때 발생하는 이벤트
        /// </summary>
        public event Action OnTileDeselected;

        /// <summary>
        /// 맵 생성이 완전히 끝났을 때 발생하는 이벤트
        /// </summary>
        public event Action<Vector3> OnMapGenerated;

        private void Awake()
        {
            // 정석적인 싱글턴 패턴: 씬 재시작 시 중복 생성 방지
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            // 씬이 언로드될 때 메모리 릭 및 에러 방지
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            InitializeGameplayStats();

            // 게임 시작 시 비동기로 맵 생성
            mapGenerationCoroutine = StartCoroutine(GenerateMapCoroutine());
        }

        private void InitializeGameplayStats()
        {
            Gold = 0;
            AP = APRecoveryAmount;
            Unit = 0;
            UnitLimit = 30;
            UnitAddLimit = 0;
            TurnCount = 1;
            CurrentTurnState = TurnState.PlayerTurn;

            OnGoldChanged?.Invoke(Gold);
            OnAPChanged?.Invoke(AP);
            OnTurnChanged?.Invoke(CurrentTurnState);
        }

        /// <summary>
        /// (개발자 치트용) 기존 맵을 모두 지우고 주어진 크기로 맵을 다시 생성
        /// </summary>
        public void ResetMap(int newWidth, int newHeight)
        {
            // 1. 기존 생성 코루틴 정지
            if (mapGenerationCoroutine != null)
            {
                StopCoroutine(mapGenerationCoroutine);
                mapGenerationCoroutine = null;
            }

            // 2. 기존 타일 삭제
            if (spawnedTiles != null)
            {
                foreach (var tile in spawnedTiles)
                {
                    if (tile != null)
                    {
                        Destroy(tile.gameObject);
                    }
                }
            }

            // 3. 상태 초기화
            currentlySelectedTile = null;
            if (selectionMarkerInstance != null)
            {
                selectionMarkerInstance.SetActive(false);
            }
            tileUnitCounts.Clear();

            // 4. 새 크기 적용
            mapWidth = newWidth;
            mapHeight = newHeight;

            // 5. 유닛 제한 및 재화, 턴 리셋
            InitializeGameplayStats();

            // 6. 맵 재생성
            mapGenerationCoroutine = StartCoroutine(GenerateMapCoroutine());
        }

        /// <summary>
        /// 비동기 방식으로 프레임을 분할하여 헥사곤 맵을 부하 없이 생성
        /// </summary>
        private IEnumerator GenerateMapCoroutine()
        {
            if (tilePrefab == null)
            {
                Debug.LogError("[WorldMapManager] 타일 프리팹이 할당되지 않았습니다!");
                yield break;
            }

            // [안전 장치] 월드맵 생성 전 크기 제한 검사 및 보정
            if (mapWidth > MAX_MAP_WIDTH || mapHeight > MAX_MAP_HEIGHT)
            {
                Debug.LogWarning($"[WorldMapManager] 맵 크기 제한 초과! 최대 크기({MAX_MAP_WIDTH}x{MAX_MAP_HEIGHT})로 자동 제한됩니다.");
                mapWidth = Mathf.Clamp(mapWidth, 1, MAX_MAP_WIDTH);
                mapHeight = Mathf.Clamp(mapHeight, 1, MAX_MAP_HEIGHT);
            }

            int totalTiles = mapWidth * mapHeight;
            
            // 데이터 원본 및 객체 캐싱 배열 생성
            ownerStates = new TileOwnerState[totalTiles];
            fogStates = new TileFogState[totalTiles];
            spawnedTiles = new WorldMapTile[totalTiles];
            tileUnitCounts.Clear();

            int spawnCountThisFrame = 0;

            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    int index = GetTileIndex(x, y);

                    // Pointy-Topped 헥사곤 3D 좌표 변환 공식
                    float px = (x + (y % 2) * 0.5f) * Mathf.Sqrt(3f) * tileRadius;
                    float py = 0f;
                    float pz = y * 1.5f * tileRadius; // 월드맵 뷰이므로 Z축을 깊이로 사용

                    Vector3 spawnPos = new Vector3(px, py, pz);

                    // 부모 Transform을 인자로 넘겨 이중 연산(Instantiate 이후 SetParent) 방지
                    WorldMapTile newTile = Instantiate(tilePrefab, spawnPos, Quaternion.identity, tileParent != null ? tileParent : transform);
                    
                    // 즉시 컴포넌트 로직 캐싱
                    newTile.Init(x, y);
                    spawnedTiles[index] = newTile;

                    // 상태 초기화 (기본적으로 맵 전체를 안개로 덮음)
                    ownerStates[index] = TileOwnerState.Neutral;
                    fogStates[index] = TileFogState.Blind;
                    newTile.UpdateFogVisual(TileFogState.Blind);

                    spawnCountThisFrame++;

                    // 지정된 임계치에 도달하면 생성 로직을 다음 프레임으로 양보
                    if (spawnCountThisFrame >= instantiatesPerFrame)
                    {
                        spawnCountThisFrame = 0;
                        yield return null; // 프레임 스파이크 완화
                    }
                }
            }

            Debug.Log($"[WorldMapManager] 비동기 맵 생성 완료! 맵 크기: {mapWidth}x{mapHeight}, 타일 수: {totalTiles}");

            // 본진(기지) 위치 선정을 먼저 호출하여 본진 타일 결정
            Vector3 focusPos = SetupStartingFactions();

            // 본진 타일을 제외한 나머지 타일에 자원을 고르게 분배
            DistributeResourcesWithFilter();

            // 도적단 타일 고르게 배치
            DistributeBanditsWithFilter();
            
            // 최초 1턴(플레이어 턴) 시작
            StartPlayerTurn();

            // 생성 완료 이벤트 발송 (카메라 바운더리 등 재계산)
            OnMapGenerated?.Invoke(focusPos);
        }

        /// <summary>
        /// 시작 진영(아군, 적군)을 랜덤하게 배치 후 아군(Blue)의 월드 좌표를 반환
        /// </summary>
        private Vector3 SetupStartingFactions()
        {
            int safeMargin = Mathf.Min(edgeMargin, Mathf.Min(mapWidth, mapHeight) / 3);

            // 맵을 4등분(사분면)하여 아군과 적군이 무조건 대각선 반대편 모서리에 스폰되도록 강제
            // 0: 남서(SW), 1: 북동(NE), 2: 북서(NW), 3: 남동(SE)
            int blueQuadrant = UnityEngine.Random.Range(0, 4);
            int redQuadrant = blueQuadrant == 0 ? 1 : 
                              blueQuadrant == 1 ? 0 : 
                              blueQuadrant == 2 ? 3 : 2;

            int halfWidth = mapWidth / 2;
            int halfHeight = mapHeight / 2;

            // 맵 중앙으로부터 일정 비율(15%)만큼 스폰을 금지하는 데드존 반경 설정
            int deadZoneX = Mathf.FloorToInt(mapWidth * 0.15f);
            int deadZoneY = Mathf.FloorToInt(mapHeight * 0.15f);

            // 로컬 함수: 지정된 사분면의 모서리(가장자리) 구역 안에서만 랜덤 좌표 반환
            (int x, int y) GetRandomPositionInQuadrant(int quadrant)
            {
                int minX = safeMargin;
                int maxX = mapWidth - safeMargin;
                int minY = safeMargin;
                int maxY = mapHeight - safeMargin;

                switch (quadrant)
                {
                    case 0: // 남서 (SW)
                        maxX = halfWidth - deadZoneX;
                        maxY = halfHeight - deadZoneY;
                        break;
                    case 1: // 북동 (NE)
                        minX = halfWidth + deadZoneX;
                        minY = halfHeight + deadZoneY;
                        break;
                    case 2: // 북서 (NW)
                        maxX = halfWidth - deadZoneX;
                        minY = halfHeight + deadZoneY;
                        break;
                    case 3: // 남동 (SE)
                        minX = halfWidth + deadZoneX;
                        maxY = halfHeight - deadZoneY;
                        break;
                }

                // 범위 오류 방지
                if (minX >= maxX) maxX = minX + 1;
                if (minY >= maxY) maxY = minY + 1;

                int genX = UnityEngine.Random.Range(minX, maxX);
                int genY = UnityEngine.Random.Range(minY, maxY);
                return (genX, genY);
            }

            // 1. 아군(Blue) 위치 선정
            var bluePos = GetRandomPositionInQuadrant(blueQuadrant);
            int blueX = bluePos.x;
            int blueY = bluePos.y;

            // 2. 적군(Red) 위치 선정 (사분면 내에서 거리 확보)
            int redX = 0;
            int redY = 0;
            float currentMinDistance = Mathf.Max(mapWidth, mapHeight) * minDistanceRatio;
            bool positionFound = false;

            // 중앙 경계선 부근에서 서로 가깝게 스폰되는 것을 방지하기 위해 최소 거리를 강제함
            while (!positionFound && currentMinDistance > 0)
            {
                for (int i = 0; i < 100; i++)
                {
                    var redPos = GetRandomPositionInQuadrant(redQuadrant);
                    redX = redPos.x;
                    redY = redPos.y;

                    // 아군과의 최소 거리를 만족한다면 성공
                    if (GetHexDistance(blueX, blueY, redX, redY) >= currentMinDistance)
                    {
                        positionFound = true;
                        break;
                    }
                }

                // 100번 시도해도 사분면 안에서 거리를 확보하지 못했다면 거리를 1칸 타협
                if (!positionFound)
                {
                    currentMinDistance -= 1f;
                }
            }

            // 3. 타일 점령 처리
            CaptureTile(blueX, blueY, TileOwnerState.Blue);
            CaptureTile(redX, redY, TileOwnerState.Red);

            // 본진(MainBase) 구조물 설정 및 글로벌 레퍼런스 대입
            int blueHQIndex = GetTileIndex(blueX, blueY);
            int redHQIndex = GetTileIndex(redX, redY);
            
            PlayerHQ = spawnedTiles[blueHQIndex];
            EnemyHQ = spawnedTiles[redHQIndex];

            if (PlayerHQ != null) PlayerHQ.SetStructure(TileStructureType.MainBase);
            if (EnemyHQ != null) EnemyHQ.SetStructure(TileStructureType.MainBase);

            // 아군 초기 유닛 설정
            tileUnitCounts[blueHQIndex] = Unit;
            if (PlayerHQ != null) PlayerHQ.UpdateUnitCount(Unit);

            // 4. 아군 시작 타일 및 주변 1칸 시야 밝히기
            RevealSurroundingFog(blueX, blueY);

            // 카메라 이동을 위해 아군 시작 위치 반환
            return GetTileWorldPosition(blueX, blueY);
        }

        /// <summary>
        /// Pointy-Topped 헥사곤 좌표계에서의 정확한 거리를 계산
        /// </summary>
        private float GetHexDistance(int x1, int y1, int x2, int y2)
        {
            // Offset 좌표를 큐브(Axial) 좌표 q, r 로 변환
            int q1 = x1 - (y1 - (y1 & 1)) / 2;
            int r1 = y1;
            
            int q2 = x2 - (y2 - (y2 & 1)) / 2;
            int r2 = y2;

            // 맨해튼 거리 방식을 이용한 헥사곤 두 타일 간의 거리
            return (Mathf.Abs(q1 - q2) + Mathf.Abs(q1 + r1 - q2 - r2) + Mathf.Abs(r1 - r2)) / 2f;
        }

        /// <summary>
        /// 지정된 타일 및 인접한 6방향의 헥사곤 타일의 안개 제거
        /// </summary>
        public void RevealSurroundingFog(int centerX, int centerY)
        {
            // 1. 중심 타일 시야 확보
            UpdateTileFog(centerX, centerY, TileFogState.Revealed);

            // 2. 인접한 6방향 탐색 (Odd-R 규칙에 따라 짝수 줄과 홀수 줄의 상대 좌표가 다름)
            Vector2Int[] evenYOffsets = new Vector2Int[]
            {
                new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 1),
                new Vector2Int(-1, 0), new Vector2Int(-1, -1), new Vector2Int(0, -1)
            };
            Vector2Int[] oddYOffsets = new Vector2Int[]
            {
                new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1),
                new Vector2Int(-1, 0), new Vector2Int(0, -1), new Vector2Int(1, -1)
            };

            Vector2Int[] offsets = (centerY % 2 == 0) ? evenYOffsets : oddYOffsets;

            foreach (var offset in offsets)
            {
                int neighborX = centerX + offset.x;
                int neighborY = centerY + offset.y;
                
                // 맵 범위를 벗어나지 않도록 UpdateTileFog 내부에서 필터링됨
                UpdateTileFog(neighborX, neighborY, TileFogState.Revealed);
            }
        }

        /// <summary>
        /// (개발자 치트용) 맵 전체의 안개를 모두 제거
        /// </summary>
        public void RevealAllFog()
        {
            if (fogStates == null) return;
            
            for (int i = 0; i < fogStates.Length; i++)
            {
                if (fogStates[i] == TileFogState.Blind)
                {
                    fogStates[i] = TileFogState.Revealed;
                    if (spawnedTiles[i] != null)
                    {
                        spawnedTiles[i].UpdateFogVisual(TileFogState.Revealed);
                    }
                }
            }
        }

        /// <summary>
        /// 2D 그리드 좌표를 1차원 배열 인덱스로 변환
        /// </summary>
        private int GetTileIndex(int x, int y)
        {
            return y * mapWidth + x;
        }

        //--------------------------------------------------------------
        #region 월드맵에 자원 배분 로직
        /// <summary>
        /// 셔플 및 인접 타일 중복 방지 필터를 사용하여 자원을 고르게 분배
        /// </summary>
        private void DistributeResourcesWithFilter()
        {
            int totalTiles = spawnedTiles.Length;
            if (totalTiles == 0) return;

            // 1. 본진 타일의 인덱스 파악
            List<int> startingFactionIndices = new List<int>();
            for (int i = 0; i < totalTiles; i++)
            {
                if (ownerStates[i] == TileOwnerState.Blue || ownerStates[i] == TileOwnerState.Red)
                {
                    startingFactionIndices.Add(i);
                }
            }

            int playableTilesCount = totalTiles - startingFactionIndices.Count;

            // 2. 본진을 제외한 플레이 가능 타일 수 기준으로 자원 풀 생성
            List<TileResourceType> resourcePool = GenerateResourcePool(playableTilesCount);

            // 3. Fisher-Yates 셔플
            ShuffleResource(resourcePool);

            // 4. 각 타일 순회하며 중복 검사 및 대기열 스왑
            int poolIndex = 0;
            for (int i = 0; i < totalTiles; i++)
            {
                WorldMapTile currentTile = spawnedTiles[i];
                if (currentTile == null) continue;

                // 본진 타일은 무조건 None(빈 땅)으로 확정 할당하고 패스
                if (ownerStates[i] == TileOwnerState.Blue || ownerStates[i] == TileOwnerState.Red)
                {
                    currentTile.SetTileResource(TileResourceType.None);
                    continue;
                }

                int cx = currentTile.GridX;
                int cy = currentTile.GridY;
                TileResourceType selectedType = resourcePool[poolIndex];

                // None(빈 땅)은 인접해도 무방하므로 중복 체크 제외
                if (selectedType != TileResourceType.None && HasClusteringNeighbor(cx, cy, selectedType))
                {
                    // 대기열 뒤쪽(poolIndex + 1부터 끝까지)에서 이웃과 겹치지 않는 대체 자원을 탐색
                    int swapIndex = FindValidReplacementIndex(poolIndex + 1, cx, cy, resourcePool);
                    if (swapIndex != -1)
                    {
                        // 안전한 자원과 스왑하여 적용
                        TileResourceType temp = resourcePool[poolIndex];
                        resourcePool[poolIndex] = resourcePool[swapIndex];
                        resourcePool[swapIndex] = temp;
                        selectedType = resourcePool[poolIndex];
                    }
                }

                currentTile.SetTileResource(selectedType);
                poolIndex++;
            }

            // 5. 검증을 위한 결과 로그 출력
            LogDistributionResults();
        }

        /// <summary>
        /// 자원 비율 설정에 따라 전체 타일 수만큼의 자원 풀을 생성
        /// </summary>
        private List<TileResourceType> GenerateResourcePool(int totalTiles)
        {
            List<TileResourceType> pool = new List<TileResourceType>(totalTiles);

            int mountainCount = Mathf.RoundToInt(totalTiles * mountainRatio);
            int goldMineCount = Mathf.RoundToInt(totalTiles * goldMineRatio);
            int farmlandCount = Mathf.RoundToInt(totalTiles * farmlandRatio);
            int pastureCount = Mathf.RoundToInt(totalTiles * pastureRatio);
            
            // 다른 모든 자원을 채우고 남은 타일 수를 자동으로 None으로 설정
            int noneCount = totalTiles - (mountainCount + goldMineCount + farmlandCount + pastureCount);
            noneCount = Mathf.Max(0, noneCount);

            for (int i = 0; i < noneCount; i++) pool.Add(TileResourceType.None);
            for (int i = 0; i < mountainCount; i++) pool.Add(TileResourceType.Mountain);
            for (int i = 0; i < goldMineCount; i++) pool.Add(TileResourceType.GoldMine);
            for (int i = 0; i < farmlandCount; i++) pool.Add(TileResourceType.Farmland);
            for (int i = 0; i < pastureCount; i++) pool.Add(TileResourceType.Pasture);

            return pool;
        }

        /// <summary>
        /// Fisher-Yates 셔플
        /// </summary>
        private void ShuffleResource(List<TileResourceType> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int randomIndex = UnityEngine.Random.Range(0, i + 1);
                TileResourceType temp = list[i];
                list[i] = list[randomIndex];
                list[randomIndex] = temp;
            }
        }

        /// <summary>
        /// Pointy-topped 헥사곤 좌표 규칙을 바탕으로 특정 타일의 이웃 타일 리스트를 반환
        /// </summary>
        private List<Vector2Int> GetNeighbors(int x, int y)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>();

            // y가 짝수(y % 2 == 0)일 때와 홀수일 때의 오프셋 정의
            int[][] offsets = (y % 2 == 0) ?
                new int[][] {
                    new int[] {1, 0}, new int[] {-1, 0},
                    new int[] {0, 1}, new int[] {-1, 1},
                    new int[] {0, -1}, new int[] {-1, -1}
                } :
                new int[][] {
                    new int[] {1, 0}, new int[] {-1, 0},
                    new int[] {1, 1}, new int[] {0, 1},
                    new int[] {1, -1}, new int[] {0, -1}
                };

            foreach (var offset in offsets)
            {
                int nx = x + offset[0];
                int ny = y + offset[1];

                if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight)
                {
                    neighbors.Add(new Vector2Int(nx, ny));
                }
            }

            return neighbors;
        }

        /// <summary>
        /// 주변 이웃 타일 중 동일한 자원이 이미 배치되어 있는지 체크
        /// </summary>
        private bool HasClusteringNeighbor(int x, int y, TileResourceType type)
        {
            List<Vector2Int> neighbors = GetNeighbors(x, y);
            foreach (var n in neighbors)
            {
                int nIndex = GetTileIndex(n.x, n.y);
                WorldMapTile neighborTile = spawnedTiles[nIndex];
                if (neighborTile != null && neighborTile.TileResourceType == type)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 중복이 안 되는 대체 자원을 대기열(startIndex부터 끝까지)에서 탐색
        /// </summary>
        private int FindValidReplacementIndex(int startIndex, int x, int y, List<TileResourceType> pool)
        {
            for (int i = startIndex; i < pool.Count; i++)
            {
                TileResourceType candidate = pool[i];
                
                // 후보군 자원 타입이 None(빈 땅)이거나, 이웃들과 중복되지 않는 경우 인덱스를 반환합니다.
                // None 자원은 인접해도 무방하므로 중복 체크를 하지 않습니다.
                if (candidate == TileResourceType.None || !HasClusteringNeighbor(x, y, candidate))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 자원 분배 결과 로그 출력
        /// </summary>
        private void LogDistributionResults()
        {
            int total = spawnedTiles.Length;
            int none = 0, mountain = 0, goldMine = 0, farmland = 0, pasture = 0;
            int duplicatesCount = 0;

            for (int i = 0; i < total; i++)
            {
                WorldMapTile tile = spawnedTiles[i];
                if (tile == null) continue;

                switch (tile.TileResourceType)
                {
                    case TileResourceType.None: none++; break;
                    case TileResourceType.Mountain: mountain++; break;
                    case TileResourceType.GoldMine: goldMine++; break;
                    case TileResourceType.Farmland: farmland++; break;
                    case TileResourceType.Pasture: pasture++; break;
                }

                // 이웃 간 중복 횟수 계산 (중복 집계)
                if (tile.TileResourceType != TileResourceType.None)
                {
                    List<Vector2Int> neighbors = GetNeighbors(tile.GridX, tile.GridY);
                    foreach (var n in neighbors)
                    {
                        int nIndex = GetTileIndex(n.x, n.y);
                        // 단방향 중복 카운트를 위해 인덱스가 자신보다 큰 이웃만 검사
                        if (nIndex > i)
                        {
                            WorldMapTile neighborTile = spawnedTiles[nIndex];
                            if (neighborTile != null && neighborTile.TileResourceType == tile.TileResourceType)
                            {
                                duplicatesCount++;
                            }
                        }
                    }
                }
            }

            Debug.Log($"[WorldMapResource] 분배 결과 통계 - 총 타일: {total}\n" +
                      $"- None(빈 땅): {none} ({((float)none / total * 100f):F1}%)\n" +
                      $"- Mountain(산맥): {mountain} ({((float)mountain / total * 100f):F1}%)\n" +
                      $"- GoldMine(금광): {goldMine} ({((float)goldMine / total * 100f):F1}%)\n" +
                      $"- Farmland(농지): {farmland} ({((float)farmland / total * 100f):F1}%)\n" +
                      $"- Pasture(목초지): {pasture} ({((float)pasture / total * 100f):F1}%)\n" +
                      $"* 인접 타일 중복 발생 수: {duplicatesCount} (None 제외)");
        }

        #endregion
        //--------------------------------------------------------------

        //--------------------------------------------------------------
        #region 월드맵에 도적단(Bandit) 분배 로직

        /// <summary>
        /// 아군/적군 기지까지의 최단 거리를 기준으로 도적단 레벨별 맵에 배치
        /// </summary>
        private void DistributeBanditsWithFilter()
        {
            int totalTiles = spawnedTiles.Length;
            if (totalTiles == 0) return;

            // 1. 배치 후보 타일 수집
            List<WorldMapTile> candidateTiles = new List<WorldMapTile>();
            for (int i = 0; i < totalTiles; i++)
            {
                WorldMapTile tile = spawnedTiles[i];
                if (tile == null) continue;

                // 거대산 및 기지 타일 제외
                if (tile.TileResourceType == TileResourceType.Mountain) continue;
                if (tile.StructureType == TileStructureType.MainBase) continue;

                // 아군 기지 바로 인접 1칸 구역 제외
                //if (PlayerHQ != null && GetHexDistance(tile.GridX, tile.GridY, PlayerHQ.GridX, PlayerHQ.GridY) <= 1.0f)
                //    continue;

                // 적군 기지 바로 인접 1칸 구역 제외
                //if (EnemyHQ != null && GetHexDistance(tile.GridX, tile.GridY, EnemyHQ.GridX, EnemyHQ.GridY) <= 1.0f) 
                //    continue;

                candidateTiles.Add(tile);
            }

            int candidateCount = candidateTiles.Count;
            if (candidateCount == 0) return;

            // 2. 맵의 최대 거리 기준 산정
            float maxMapDistance = Mathf.Max(mapWidth, mapHeight);

            // 3. 기지와의 거리에 따라 후보 타일 분류 (Level 1, 2, 3, 4 영역)
            List<WorldMapTile> lv1Candidates = new List<WorldMapTile>();
            List<WorldMapTile> lv2Candidates = new List<WorldMapTile>();
            List<WorldMapTile> lv3Candidates = new List<WorldMapTile>();
            List<WorldMapTile> lv4Candidates = new List<WorldMapTile>();

            foreach (var tile in candidateTiles)
            {
                float distToPlayerHQ = PlayerHQ != null ? GetHexDistance(tile.GridX, tile.GridY, PlayerHQ.GridX, PlayerHQ.GridY) : 999f;
                float distToEnemyHQ = EnemyHQ != null ? GetHexDistance(tile.GridX, tile.GridY, EnemyHQ.GridX, EnemyHQ.GridY) : 999f;
                float minHQDistance = Mathf.Min(distToPlayerHQ, distToEnemyHQ);

                float normalizedDist = minHQDistance / maxMapDistance;

                if (normalizedDist <= 0.3f)
                {
                    lv1Candidates.Add(tile);
                }
                else if (normalizedDist <= 0.4f)
                {
                    lv2Candidates.Add(tile);
                }
                else if (normalizedDist <= 0.6f)
                {
                    lv3Candidates.Add(tile);
                }
                else
                {
                    lv4Candidates.Add(tile);
                }
            }

            // 4. 각 영역별 도적단 스폰 개수 계산 및 배치
            SpawnBanditsInArea(lv1Candidates, 1, banditLv1Ratio);
            SpawnBanditsInArea(lv2Candidates, 2, banditLv2Ratio);
            SpawnBanditsInArea(lv3Candidates, 3, banditLv3Ratio);
            SpawnBanditsInArea(lv4Candidates, 4, banditLv4Ratio);

            Debug.Log($"[WorldMapManager] 거리 기반 도적단 배치 완료 - 후보지 분류 (Lv1구역: {lv1Candidates.Count}, Lv2구역: {lv2Candidates.Count}, Lv3구역: {lv3Candidates.Count}, Lv4구역: {lv4Candidates.Count})");
        }

        /// <summary>
        /// 특정 구역 후보 타일 리스트에 지정된 레벨의 도적단을 스폰 비율에 따라 분산 배치
        /// </summary>
        private void SpawnBanditsInArea(List<WorldMapTile> areaCandidates, int banditLevel, float spawnRatio)
        {
            int count = areaCandidates.Count;
            if (count == 0) return;

            int targetSpawnCount = Mathf.RoundToInt(count * spawnRatio);
            targetSpawnCount = Mathf.Clamp(targetSpawnCount, 0, count);

            // 스폰 레벨 풀 생성 (banditLevel과 0)
            List<int> pool = new List<int>(count);
            for (int i = 0; i < targetSpawnCount; i++) pool.Add(banditLevel);
            for (int i = 0; i < count - targetSpawnCount; i++) pool.Add(0);

            // 셔플
            ShuffleInts(pool);

            // 인접 뭉침 방지 적용 분배
            int poolIndex = 0;
            for (int i = 0; i < count; i++)
            {
                WorldMapTile tile = areaCandidates[i];
                int cx = tile.GridX;
                int cy = tile.GridY;
                int selectedVal = pool[poolIndex];

                if (selectedVal > 0 && HasBanditNeighbor(cx, cy))
                {
                    int swapIndex = FindValidBanditReplacementIndex(poolIndex + 1, cx, cy, pool);
                    if (swapIndex != -1)
                    {
                        int temp = pool[poolIndex];
                        pool[poolIndex] = pool[swapIndex];
                        pool[swapIndex] = temp;
                        selectedVal = pool[poolIndex];
                    }
                }

                if (selectedVal > 0)
                {
                    tile.SetBandit(selectedVal);
                }
                else
                {
                    tile.SetStructure(TileStructureType.None);
                }
                poolIndex++;
            }
        }

        private void ShuffleInts(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int randomIndex = UnityEngine.Random.Range(0, i + 1);
                int temp = list[i];
                list[i] = list[randomIndex];
                list[randomIndex] = temp;
            }
        }

        private bool HasBanditNeighbor(int x, int y)
        {
            List<Vector2Int> neighbors = GetNeighbors(x, y);
            foreach (var n in neighbors)
            {
                int nIndex = GetTileIndex(n.x, n.y);
                WorldMapTile neighborTile = spawnedTiles[nIndex];
                if (neighborTile != null && neighborTile.StructureType == TileStructureType.Bandit)
                {
                    return true;
                }
            }
            return false;
        }

        private int FindValidBanditReplacementIndex(int startIndex, int x, int y, List<int> pool)
        {
            for (int i = startIndex; i < pool.Count; i++)
            {
                int candidate = pool[i];
                
                // 후보 레벨이 0(없음)이거나, 이웃 타일에 도적단이 없는 경우에만 스왑 후보로 허용
                if (candidate == 0 || !HasBanditNeighbor(x, y))
                {
                    return i;
                }
            }
            return -1;
        }

        #endregion
        //--------------------------------------------------------------

        /// <summary>
        /// 특정 좌표(x, y)의 WorldMapTile 인스턴스를 반환
        /// </summary>
        public WorldMapTile GetTile(int x, int y)
        {
            if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return null;
            int index = GetTileIndex(x, y);
            return spawnedTiles[index];
        }

        /// <summary>
        /// 특정 좌표(x, y)의 소유권 상태를 반환
        /// </summary>
        public TileOwnerState GetTileOwner(int x, int y)
        {
            if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return TileOwnerState.Neutral;
            int index = GetTileIndex(x, y);
            return ownerStates[index];
        }

        /// <summary>
        /// 지정한 좌표의 타일이 아군(Player) 소유의 타일인지 여부를 반환
        /// </summary>
        public bool IsAllyTile(int x, int y)
        {
            return GetTileOwner(x, y) == TileOwnerState.Blue;
        }

        /// <summary>
        /// 지정한 좌표의 타일이 적군(Enemy) 소유의 타일인지 여부를 반환
        /// </summary>
        public bool IsEnemyTile(int x, int y)
        {
            return GetTileOwner(x, y) == TileOwnerState.Red;
        }


        /// <summary>
        /// 특정 좌표(x, y)의 타일에 배치된 유닛 수를 반환
        /// </summary>
        public int GetTileUnitCount(int x, int y)
        {
            if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return 0;
            int index = GetTileIndex(x, y);
            return tileUnitCounts.TryGetValue(index, out int count) ? count : 0;
        }

        /// <summary>
        /// 특정 좌표(x, y)의 타일에 배치된 유닛 수를 설정
        /// </summary>
        public void SetTileUnitCount(int x, int y, int count)
        {
            if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return;
            int index = GetTileIndex(x, y);
            tileUnitCounts[index] = count;
        }

        /// <summary>
        /// 특정 좌표(x, y)의 타일에 배치된 유닛 수를 누적 가산
        /// </summary>
        public void AddTileUnitCount(int x, int y, int amount)
        {
            if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return;
            int index = GetTileIndex(x, y);
            if (tileUnitCounts.ContainsKey(index))
            {
                tileUnitCounts[index] += amount;
            }
            else
            {
                tileUnitCounts[index] = amount;
            }
        }

        /// <summary>
        /// 특정 좌표(x, y)의 타일이 안개(Blind) 상태인지 여부를 반환
        /// </summary>
        public bool IsBlindTile(int x, int y)
        {
            if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return true;
            int index = GetTileIndex(x, y);
            if (fogStates == null || index < 0 || index >= fogStates.Length) return true;
            return fogStates[index] == TileFogState.Blind;
        }

        /// <summary>
        /// 특정 소유주와 특정 자원타입, 그리고 자원개발이 완료된 타일의 개수를 반환
        /// </summary>
        public int GetDevelopedTileCountByOwnerAndType(TileOwnerState owner, TileResourceType resourceType, TileDevelopmentType developmentType)
        {
            if (spawnedTiles == null) return 0;

            int count = 0;
            for (int i = 0; i < spawnedTiles.Length; i++)
            {
                if (spawnedTiles[i] != null &&
                    ownerStates[i] == owner &&
                    spawnedTiles[i].TileResourceType == resourceType &&
                    spawnedTiles[i].DevelopmentType == developmentType)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 농장 개발 타일 수에 따라 최대 유닛 수용 추가 제한치(UnitAddLimit)를 동기화
        /// </summary>
        public void UpdateUnitAddLimit()
        {
            int farmCount = GetDevelopedTileCountByOwnerAndType(TileOwnerState.Blue, TileResourceType.Farmland, TileDevelopmentType.Farm);
            UnitAddLimit = farmCount * 10;
        }

        /// <summary>
        /// 특정 타일 좌표(X, Y)에 대한 3D 월드 공간 좌표를 반환
        /// </summary>
        public Vector3 GetTileWorldPosition(int x, int y)
        {
            if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return Vector3.zero;
            return spawnedTiles[GetTileIndex(x, y)].transform.position;
        }

        /// <summary>
        /// 지정된 좌표의 타일이 아군(Blue) 영토이거나, 아군 영토에 인접해 있는지 확인
        /// </summary>
        private bool IsAllyOrAdjacentToAlly(int x, int y)
        {
            if (GetTileOwner(x, y) == TileOwnerState.Blue)
            {
                return true;
            }

            List<Vector2Int> neighbors = GetNeighbors(x, y);
            foreach (var neighbor in neighbors)
            {
                if (GetTileOwner(neighbor.x, neighbor.y) == TileOwnerState.Blue)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 타일을 선택하고 마커와 하이라이트 연출 활성화
        /// </summary>
        public void SelectTile(int x, int y)
        {
            if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return;

            //유닛 재배치 활성 상태인 경우
            if (isRedeployActive)
            {
                TrySelectTileRedeployActive(x, y);                
                return;
            }

            // 아군/인접지가 아니거나 또는 안개로 가려진 타일인 경우 선택 차단
            if (!IsAllyOrAdjacentToAlly(x, y) || IsBlindTile(x, y))
            {
                DeselectTile();
                return;
            }

            TileOwnerState owner = GetTileOwner(x, y);

            // 중립 또는 적군 타일 클릭 시 팝업 없이 즉시 공격 준비 상태로 진입
            if (owner == TileOwnerState.Neutral || owner == TileOwnerState.Red)
            {
                DeselectTile();
                EnterAttackPreparationState(x, y);
                return;
            }

            int index = GetTileIndex(x, y);
            WorldMapTile targetTile = spawnedTiles[index];

            // 이미 동일한 타일이 선택되어 있다면 무시하거나 해제할 수 있습니다.
            // 여기서는 동일한 타일 클릭 시 선택 해제로 처리합니다.
            if (currentlySelectedTile == targetTile)
            {
                DeselectTile();
                return;
            }

            // 1. 기존 선택 해제
            DeselectTile();

            // 2. 새 타일 선택 적용 (하이라이트 켜기)
            currentlySelectedTile = targetTile;
            currentlySelectedTile.SetHighlight(true);

            // 3. 마커 생성 및 이동
            if (selectionMarkerPrefab != null && selectionMarkerInstance == null)
            {
                selectionMarkerInstance = Instantiate(selectionMarkerPrefab, transform);
            }
            if (selectionMarkerInstance != null)
            {
                // 타일 면과 겹치지 않도록 Y축으로 아주 살짝 띄워줌
                selectionMarkerInstance.transform.position = targetTile.transform.position + Vector3.up * 0.1f;
                selectionMarkerInstance.SetActive(true);
            }

            // 4. 이벤트 발송 (카메라 이동, UI 표시 등)
            OnTileSelected?.Invoke(x, y);
        }

        /// <summary>
        /// 타일의 유닛 재배치 활성 상태 요청
        /// </summary>
        public void TrySelectTileRedeployActive(int x, int y)
        {
            if (GetTileOwner(x, y) == TileOwnerState.Blue)
            {
                // 출발지와 대상지가 같은 경우 예외 처리
                if (redeployBaseTileX == x && redeployBaseTileY == y)
                {
                    NoticePanelUI.Instance?.ShowNotice("Cannot redeploy to the same tile.", NoticeType.Warning);
                    ActiveRedeployState(false, -1, -1);
                    DeselectTile();
                    return;
                }

                int baseIndex = GetTileIndex(redeployBaseTileX, redeployBaseTileY);
                int targetIndex = GetTileIndex(x, y);
                int unitCountToMove = GetTileUnitCount(redeployBaseTileX, redeployBaseTileY);

                // 출발지 유닛 체크
                if (unitCountToMove <= 0)
                {
                    NoticePanelUI.Instance?.ShowNotice("No units available to redeploy.", NoticeType.Warning);
                    ActiveRedeployState(false, -1, -1);
                    DeselectTile();
                    return;
                }

                // 거리 계산 및 최종 AP 소모량 산출
                float hexDist = GetHexDistance(redeployBaseTileX, redeployBaseTileY, x, y);
                int distance = Mathf.RoundToInt(hexDist);
                int finalAPCost = redeployBaseAPCost + Mathf.CeilToInt(distance * redeployAPCostPerDistance);

                // AP 부족 예외 처리
                if (AP < finalAPCost)
                {
                    NoticePanelUI.Instance?.ShowNotice($"Insufficient AP! Need {finalAPCost} AP (Dist: {distance}).", NoticeType.Warning);
                    ActiveRedeployState(false, -1, -1);
                    DeselectTile();
                    return;
                }

                // AP 차감 및 유닛 데이터 이동
                ConsumeAP(finalAPCost);
                tileUnitCounts[targetIndex] = GetTileUnitCount(x, y) + unitCountToMove;
                tileUnitCounts[baseIndex] = 0;

                // 비주얼 갱신
                if (spawnedTiles[baseIndex] != null) spawnedTiles[baseIndex].UpdateUnitCount(0);
                if (spawnedTiles[targetIndex] != null) spawnedTiles[targetIndex].UpdateUnitCount(tileUnitCounts[targetIndex]);

                if (WorldUIManager.Instance != null)
                    WorldUIManager.Instance.UpdateResourceInfo();

                NoticePanelUI.Instance?.ShowNotice($"Redeploy Success! ({finalAPCost} AP Consumed)", NoticeType.Success);
                ActiveRedeployState(false, -1, -1);
            }
            else
            {
                NoticePanelUI.Instance?.ShowNotice("Redeploy on allied tiles only.", NoticeType.Warning);
            }

            DeselectTile();
        }

        /// <summary>
        /// 유닛 재배치 활성 상태 설정
        /// </summary>
        public void ActiveRedeployState(bool active, int x, int y)
        {
            isRedeployActive = active;
            redeployBaseTileX = x;
            redeployBaseTileY = y;

            if (WorldUIManager.Instance != null)
                WorldUIManager.Instance.ShowRedeployTabPanel(isRedeployActive);
        }

        /// <summary>
        /// 특정 좌표(x, y) 주변 인접 타일들 중 아군(Blue) 타일에 주둔한 총 유닛 수를 반환
        /// </summary>
        public int GetAdjacentAllyUnitCount(int x, int y)
        {
            int totalUnits = 0;
            List<Vector2Int> neighbors = GetNeighbors(x, y);
            foreach (var neighbor in neighbors)
            {
                if (GetTileOwner(neighbor.x, neighbor.y) == TileOwnerState.Blue)
                {
                    totalUnits += GetTileUnitCount(neighbor.x, neighbor.y);
                }
            }
            return totalUnits;
        }

        public void EnterAttackPreparationState(int targetX, int targetY)
        {
            // 기존에 공격 준비 상태였다면 정리
            if (isAttackPrepActive)
            {
                CancelAttackPreparationState();
            }

            attackTargetTileX = targetX;
            attackTargetTileY = targetY;
            isAttackPrepActive = true;

            // 대상 타일 하이라이트 켜기
            WorldMapTile targetTile = GetTile(targetX, targetY);
            if (targetTile != null)
            {
                targetTile.SetHighlight(true);
            }

            // 인접한 아군 타일 중 유닛 > 0 인 타일 탐색하여 화살표 활성화 및 회전
            List<Vector2Int> neighbors = GetNeighbors(targetX, targetY);
            Vector3 targetWorldPos = GetTileWorldPosition(targetX, targetY);

            foreach (var neighbor in neighbors)
            {
                if (GetTileOwner(neighbor.x, neighbor.y) == TileOwnerState.Blue)
                {
                    int units = GetTileUnitCount(neighbor.x, neighbor.y);
                    if (units > 0)
                    {
                        WorldMapTile allyTile = GetTile(neighbor.x, neighbor.y);
                        if (allyTile != null)
                        {
                            allyTile.ActivateAttackArrow(targetWorldPos);
                        }
                    }
                }
            }

            NoticePanelUI.Instance?.ShowNotice("Click arrow to attack.", NoticeType.Info);
        }

        public void CancelAttackPreparationState()
        {
            if (!isAttackPrepActive) return;

            // 대상 타일 하이라이트 끄기
            WorldMapTile targetTile = GetTile(attackTargetTileX, attackTargetTileY);
            if (targetTile != null)
            {
                targetTile.SetHighlight(false);
            }

            // 모든 인접 아군 타일의 화살표 비활성화
            List<Vector2Int> neighbors = GetNeighbors(attackTargetTileX, attackTargetTileY);
            foreach (var neighbor in neighbors)
            {
                if (GetTileOwner(neighbor.x, neighbor.y) == TileOwnerState.Blue)
                {
                    WorldMapTile allyTile = GetTile(neighbor.x, neighbor.y);
                    if (allyTile != null)
                    {
                        allyTile.DeactivateAttackArrow();
                    }
                }
            }

            isAttackPrepActive = false;
            attackTargetTileX = -1;
            attackTargetTileY = -1;
        }

        /// <summary>
        /// 현재 선택된 타일을 해제하고 모든 연출을 초기화
        /// </summary>
        public void DeselectTile()
        {
            if (currentlySelectedTile != null)
            {
                currentlySelectedTile.SetHighlight(false);
                currentlySelectedTile = null;

                if (selectionMarkerInstance != null)
                {
                    selectionMarkerInstance.SetActive(false);
                }

                OnTileDeselected?.Invoke();
            }
        }

        /// <summary>
        /// 특정 타일을 점령하여 데이터와 뷰를 즉시 동기화하고 이벤트를 발생
        /// </summary>
        public void CaptureTile(int x, int y, TileOwnerState newOwner)
        {
            // 인덱스 범위 이탈 방어 로직
            if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight)
            {
                Debug.LogWarning($"[WorldMapManager] 타일 접근 에러: 잘못된 범위 ({x}, {y})");
                return;
            }

            int index = GetTileIndex(x, y);

            // 기존 소유권과 동일하다면 렌더링 무시
            if (ownerStates[index] == newOwner) return;

            // 1. 데이터 모델 업데이트
            ownerStates[index] = newOwner;

            // 2. 뷰 즉각 반영
            spawnedTiles[index].UpdateOwnerVisual(newOwner);

            // 만약 새로 점령된 타일이 기존에 도적단 타일이었다면 소멸 처리
            if (spawnedTiles[index].StructureType == TileStructureType.Bandit)
            {
                spawnedTiles[index].SetStructure(TileStructureType.None);
                spawnedTiles[index].UpdateResourceVisuals();
            }

            // 3. 점령 이벤트 발동 (타 시스템과 결합도 최소화)
            OnTileCaptured?.Invoke(x, y, newOwner);
        }

        /// <summary>
        /// 특정 타일이 공격 불가능한 타일(산악 지형 등)인지 체크
        /// </summary>
        public bool IsUnattackableTile(int x, int y)
        {
            if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return false;

            int index = GetTileIndex(x, y);
            WorldMapTile tile = spawnedTiles[index];
            return tile != null && tile.TileResourceType == TileResourceType.Mountain;
        }

        /// <summary>
        /// 타일의 안개(시야) 상태를 동적으로 변경
        /// </summary>
        public void UpdateTileFog(int x, int y, TileFogState newFogState)
        {
            if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return;

            int index = GetTileIndex(x, y);

            if (fogStates[index] == newFogState) return;

            fogStates[index] = newFogState;
            spawnedTiles[index].UpdateFogVisual(newFogState);
        }

        /// <summary>
        /// 카메라 컨트롤러 등이 맵의 범위를 알 수 있도록 경계를 계산하여 반환
        /// </summary>
        public Bounds CalculateMapBounds()
        {
            if (mapWidth == 0 || mapHeight == 0) return new Bounds(Vector3.zero, Vector3.zero);

            // 첫 번째 타일(0,0)의 중심 좌표 (최소점 근처)
            Vector3 minPos = Vector3.zero; 

            // 마지막 타일(Width-1, Height-1)의 중심 좌표 (최대점 근처)
            int lastX = mapWidth - 1;
            int lastY = mapHeight - 1;
            float maxPx = (lastX + (lastY % 2) * 0.5f) * Mathf.Sqrt(3f) * tileRadius;
            float maxPz = lastY * 1.5f * tileRadius;
            Vector3 maxPos = new Vector3(maxPx, 0f, maxPz);

            Bounds bounds = new Bounds();
            bounds.SetMinMax(minPos, maxPos);

            // 타일의 반지름만큼 여유 공간
            bounds.Expand(new Vector3(tileRadius * 2f, 0f, tileRadius * 2f));

            return bounds;
        }

        //-----------------------------------------------------------
        #region 월드맵 턴제 시스템 관련 정의
        /// <summary>
        /// 특정 소유주를 가진 타일의 총 개수를 반환
        /// </summary>
        public int GetTileCountByOwner(TileOwnerState owner)
        {
            if (ownerStates == null) return 0;
            
            int count = 0;
            for (int i = 0; i < ownerStates.Length; i++)
            {
                if (ownerStates[i] == owner)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 플레이어(아군)의 턴을 시작합니다.
        /// </summary>
        public void StartPlayerTurn()
        {
            CurrentTurnState = TurnState.PlayerTurn;
            
            // 1. AP 회복 적용 (이전 남은 AP에 회복량을 더하고 최대 MaxAP(5)로 제한)
            UpdateUnitAddLimit();

            int ranchCount = GetDevelopedTileCountByOwnerAndType(TileOwnerState.Blue, TileResourceType.Pasture, TileDevelopmentType.Ranch);
            APPerTurn = APRecoveryAmount + (ranchCount * 1);
            AP = Mathf.Min(MaxAP, AP + APPerTurn);
            OnAPChanged?.Invoke(AP);

            int blueTileCount = GetTileCountByOwner(TileOwnerState.Blue);
            int mineCount = GetDevelopedTileCountByOwnerAndType(TileOwnerState.Blue, TileResourceType.GoldMine, TileDevelopmentType.Mine);
            GoldPerTurn = (blueTileCount * 2) + (mineCount * 5);
            AddGold(GoldPerTurn);

            int prevUnit = Unit;
            int farmCount = GetDevelopedTileCountByOwnerAndType(TileOwnerState.Blue, TileResourceType.Farmland, TileDevelopmentType.Farm);
            unitsPerTurn = 3 + (farmCount * 2);
            Unit = Mathf.Min(UnitLimit + UnitAddLimit, Unit + unitsPerTurn);
            int actualIncome = Unit - prevUnit;

            if (PlayerHQ != null && actualIncome > 0)
            {
                int hqIndex = GetTileIndex(PlayerHQ.GridX, PlayerHQ.GridY);
                if (tileUnitCounts.ContainsKey(hqIndex))
                    tileUnitCounts[hqIndex] += actualIncome;
                else
                    tileUnitCounts[hqIndex] = actualIncome;

                PlayerHQ.UpdateUnitCount(tileUnitCounts[hqIndex]);
            }

            OnTurnChanged?.Invoke(CurrentTurnState);
            Debug.Log($"[WorldMapManager] 플레이어 턴 시작 (Turn {TurnCount}) - AP: {AP}, Gold: {Gold}, Unit: {Unit}/{UnitLimit + UnitAddLimit} (타일 수: {blueTileCount}, +{GoldPerTurn} Gold, +{unitsPerTurn} Unit)");
            NoticePanelUI.Instance?.ShowNotice($"Start Player Turn (Turn {TurnCount}, +{GoldPerTurn} Gold, +{unitsPerTurn} Unit)", NoticeType.Info);
        }

        /// <summary>
        /// 플레이어 턴을 수동으로 종료하고 적군 턴을 시작
        /// </summary>
        public void EndPlayerTurn()
        {
            if (CurrentTurnState != TurnState.PlayerTurn) return;

            Debug.Log("[WorldMapManager] 플레이어 턴 종료");
            DeselectTile();

            StartEnemyTurn();
        }

        /// <summary>
        /// 적군(AI)의 턴을 시작
        /// </summary>
        private void StartEnemyTurn()
        {
            CurrentTurnState = TurnState.EnemyTurn;
            OnTurnChanged?.Invoke(CurrentTurnState);
            Debug.Log($"[WorldMapManager] 적군(AI) 턴 시작");
            NoticePanelUI.Instance?.ShowNotice("Start Enemy(AI) Turn", NoticeType.Info);

            StartCoroutine(EnemyTurnRoutine());
        }

        /// <summary>
        /// 적군 행동을 시뮬레이션하는 코루틴
        /// </summary>
        private IEnumerator EnemyTurnRoutine()
        {
            // AI 생각 연출 대기
            yield return new WaitForSeconds(2.5f);

            // TODO: 실제 AI 타일 점령이나 이동 연산이 추가될 자리입니다.
            Debug.Log("[WorldMapManager] 적군(AI) 행동 완료");
            NoticePanelUI.Instance?.ShowNotice("[Debug] End Enemy Turn", NoticeType.Debug);

            EndEnemyTurn();
        }

        /// <summary>
        /// 적군 턴을 완전히 종료하고 다음 플레이어 턴
        /// </summary>
        private void EndEnemyTurn()
        {
            TurnCount++;
            StartPlayerTurn();
        }

        #endregion
        //-----------------------------------------------------------

        /// <summary>
        /// 행동력(AP)을 차감
        /// </summary>
        public bool ConsumeAP(int amount)
        {
            if (CurrentTurnState != TurnState.PlayerTurn)
            {
                Debug.LogWarning("[WorldMapManager] 아군 턴이 아닙니다!");
                return false;
            }

            if (AP < amount)
            {
                Debug.LogWarning($"[WorldMapManager] AP가 부족합니다! (현재: {AP}, 필요: {amount})");
                return false;
            }

            AP -= amount;
            OnAPChanged?.Invoke(AP);
            return true;
        }

        /// <summary>
        /// 골드를 획득
        /// </summary>
        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            OnGoldChanged?.Invoke(Gold);
        }

        /// <summary>
        /// 골드를 소모
        /// </summary>
        public bool ConsumeGold(int amount)
        {
            if (Gold < amount)
            {
                Debug.LogWarning($"[WorldMapManager] 골드가 부족합니다! (현재: {Gold}, 필요: {amount})");
                return false;
            }

            Gold -= amount;
            OnGoldChanged?.Invoke(Gold);
            return true;
        }

        //-----------------------------------------------------------
        #region 전투 진입 및 승/패 처리
        [Header("Sleep For Battle Hide")]
        [SerializeField] private GameObject[] sleepForBattleHideObjs;

        public void SleepForBattle()
        {
            foreach (var go in sleepForBattleHideObjs)
            {
                if (go != null)
                    go.SetActive(false);
            }
        }

        public void WakeUpAndApplyResult()
        {
            foreach (var go in sleepForBattleHideObjs)
            {
                if (go != null)
                    go.SetActive(true);
            }

            ApplyBattleResult();
        }

        public void ExecuteAttackFromAlly(int allyX, int allyY)
        {
            if (!isAttackPrepActive) return;

            if (CoreManager.Instance == null)
            {
                NoticePanelUI.Instance?.ShowNotice("CoreManager is Null.", NoticeType.Warning);
                CancelAttackPreparationState();
                return;
            }

            int deployedUnits = GetTileUnitCount(allyX, allyY);
            if (deployedUnits <= 0)
            {
                NoticePanelUI.Instance?.ShowNotice("No units available in this tile.", NoticeType.Warning);
                CancelAttackPreparationState();
                return;
            }

            int finalAttackAP = 3;

            if (AP < finalAttackAP)
            {
                NoticePanelUI.Instance?.ShowNotice($"Insufficient AP! Need {finalAttackAP} AP.", NoticeType.Warning);
                CancelAttackPreparationState();
                return;
            }

            int targetIndex = GetTileIndex(attackTargetTileX, attackTargetTileY);
            int allyIndex = GetTileIndex(allyX, allyY);
            WorldMapTile targetTile = spawnedTiles[targetIndex];

            ConsumeAP(finalAttackAP);

            tileUnitCounts[allyIndex] = 0;
            if (spawnedTiles[allyIndex] != null) spawnedTiles[allyIndex].UpdateUnitCount(0);

            int defenderUnits = GetTileUnitCount(attackTargetTileX, attackTargetTileY);
            if (targetTile.StructureType == TileStructureType.Bandit)
            {
                defenderUnits = targetTile.BanditLevel * 5;
            }

            BattleContext context = new BattleContext
            {
                AttackerWorldTileX = allyX,
                AttackerWorldTileY = allyY,
                TargetWorldTileX = attackTargetTileX,
                TargetWorldTileY = attackTargetTileY,
                AttackerUnitCount = deployedUnits,
                DefenderUnitCount = defenderUnits,
                ResourceType = targetTile.TileResourceType,
                StructureType = targetTile.StructureType,
                BanditLevel = targetTile.BanditLevel,
                IsResolved = false,
                IsPlayerVictory = false,
                SurvivedAttackerUnitCount = 0
            };

            if (WorldUIManager.Instance != null)
                WorldUIManager.Instance.UpdateResourceInfo();

            CancelAttackPreparationState();

            //타겟 타일에 수비 병력이 없다면 즉시 승리 처리
            if (defenderUnits <= 0)
            {
                context.IsResolved = true;
                context.IsPlayerVictory = true;
                context.SurvivedAttackerUnitCount = deployedUnits;

                if (CoreManager.Instance != null)
                    CoreManager.Instance.CurrentBattleContext = context;

                ApplyBattleResult();
            }
            else
            {
                SleepForBattle();
                CoreManager.Instance.LoadBattleSceneAdditive(context);
            }
        }

        public void ExecuteImmediateBattleResult(int allyX, int allyY, bool isVictory)
        {
            if (!isAttackPrepActive) return;

            int deployedUnits = GetTileUnitCount(allyX, allyY);
            if (deployedUnits <= 0)
            {
                NoticePanelUI.Instance?.ShowNotice("No units available in this tile.", NoticeType.Warning);
                CancelAttackPreparationState();
                return;
            }

            int finalAttackAP = 3;

            if (AP < finalAttackAP)
            {
                NoticePanelUI.Instance?.ShowNotice($"Insufficient AP! Need {finalAttackAP} AP.", NoticeType.Warning);
                CancelAttackPreparationState();
                return;
            }

            int targetIndex = GetTileIndex(attackTargetTileX, attackTargetTileY);
            int allyIndex = GetTileIndex(allyX, allyY);
            WorldMapTile targetTile = spawnedTiles[targetIndex];

            ConsumeAP(finalAttackAP);

            tileUnitCounts[allyIndex] = 0;
            if (spawnedTiles[allyIndex] != null) spawnedTiles[allyIndex].UpdateUnitCount(0);

            int defenderUnits = GetTileUnitCount(attackTargetTileX, attackTargetTileY);
            if (targetTile.StructureType == TileStructureType.Bandit)
            {
                defenderUnits = targetTile.BanditLevel * 5;
            }

            BattleContext context = new BattleContext
            {
                AttackerWorldTileX = allyX,
                AttackerWorldTileY = allyY,
                TargetWorldTileX = attackTargetTileX,
                TargetWorldTileY = attackTargetTileY,
                AttackerUnitCount = deployedUnits,
                DefenderUnitCount = defenderUnits,
                ResourceType = targetTile.TileResourceType,
                StructureType = targetTile.StructureType,
                BanditLevel = targetTile.BanditLevel,
                IsResolved = true,
                IsPlayerVictory = isVictory,
                SurvivedAttackerUnitCount = isVictory ? deployedUnits : 0
            };

            if (WorldUIManager.Instance != null)
                WorldUIManager.Instance.UpdateResourceInfo();

            CancelAttackPreparationState();

            if (CoreManager.Instance != null)
                CoreManager.Instance.CurrentBattleContext = context;

            ApplyBattleResult();
        }

        private void ApplyBattleResult()
        {
            var context = CoreManager.Instance?.CurrentBattleContext;
            if (context == null || !context.IsResolved) return;

            int allyIndex = GetTileIndex(context.AttackerWorldTileX, context.AttackerWorldTileY);
            int targetIndex = GetTileIndex(context.TargetWorldTileX, context.TargetWorldTileY);

            int deadUnits = context.AttackerUnitCount - context.SurvivedAttackerUnitCount;
            if (deadUnits > 0)
            {
                Unit = Mathf.Max(0, Unit - deadUnits);
            }

            if (context.IsPlayerVictory)
            {
                if (context.StructureType == TileStructureType.MainBase)
                {
                    NoticePanelUI.Instance?.ShowNotice("Campaign Cleared! Enemy HQ Destroyed!", NoticeType.Success);
                    // TODO: Game Clear UI 연출
                }
                else
                {
                    NoticePanelUI.Instance?.ShowNotice($"Victory! {context.SurvivedAttackerUnitCount} units survived.", NoticeType.Success);
                }
                CaptureTile(context.TargetWorldTileX, context.TargetWorldTileY, TileOwnerState.Blue);
                tileUnitCounts[targetIndex] = context.SurvivedAttackerUnitCount;
                if (spawnedTiles[targetIndex] != null) spawnedTiles[targetIndex].UpdateUnitCount(context.SurvivedAttackerUnitCount);
            }
            else
            {
                NoticePanelUI.Instance?.ShowNotice($"Defeat! Retreating with {context.SurvivedAttackerUnitCount} units.", NoticeType.Warning);
                tileUnitCounts[allyIndex] += context.SurvivedAttackerUnitCount;
                if (spawnedTiles[allyIndex] != null) spawnedTiles[allyIndex].UpdateUnitCount(tileUnitCounts[allyIndex]);
            }

            if (WorldUIManager.Instance != null)
                WorldUIManager.Instance.UpdateResourceInfo();

            if (CoreManager.Instance != null)
                CoreManager.Instance.CurrentBattleContext = null;
        }
        #endregion
        //-----------------------------------------------------------

    }
}
