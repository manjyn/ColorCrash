using UnityEngine;
using System.Collections.Generic;
using ColorCrash;
using ColorCrash.Units;

/// <summary> 
/// 전투맵의 1차원 배열로 타일 상태와 건물 배치등 관리 클래스
/// </summary>
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Settings")]
    public int width { get; private set; }
    public int height { get; private set; }
    public const float tileSize = 10.0f;
    public int totalTiles { get; private set; }

    [Header("Grid Data")]
    // 0: Neutral, 1: Blue, 2: Red
    public TeamColor[] tileStates;
    
    // 타일 위 건물 참조 배열 (빈 곳은 null)
    public TowerBase[] gridBuildings;

    // 타일 위의 유닛 캐시 리스트
    public List<UnitBase>[] gridUnits;

    // 그리드의 기준점 (좌측 하단)
    public Vector3 gridOrigin = Vector3.zero;

    // 각 팀별 점령 타일 인덱스 캐시 리스트
    [HideInInspector] public List<int> blueTileIndices = new List<int>();
    [HideInInspector] public List<int> redTileIndices = new List<int>();
    [HideInInspector] public List<int> neutralTileIndices = new List<int>();

    // 각 팀별 동적 Unit 스폰 포인트 캐시 리스트 (최소 4개 ~ 최대 16개)
    [HideInInspector] public List<int> blueSpawnTileIndices = new List<int>();
    [HideInInspector] public List<int> redSpawnTileIndices = new List<int>();

    // 장애물 O(1) 조기 리턴 캐시 테이블
    [HideInInspector] public bool[] isObstacleTile;
    [HideInInspector] public bool[] isNearObstacleTile;

    [Header("Renderer")]
    public GridRenderer gridRenderer;

    // 타일 점령 상태 변경 이벤트
    public static event System.Action<int, int, int> OnTerritoryChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void InitGrid(MapStageConfig config)
    {
        width = config.Width;
        height = config.Height;
        totalTiles = width * height;

        // 1차원 배열 동적 초기화
        tileStates = new TeamColor[totalTiles];
        gridBuildings = new TowerBase[totalTiles];
        gridUnits = new List<UnitBase>[totalTiles];

        // 리스트 사전 메모리 할당
        for (int i = 0; i < totalTiles; i++)
        {
            gridUnits[i] = new List<UnitBase>(16);
        }

        InitializeMapTerritory(config);

        // GridManager의 타일(tileStates)을 바탕으로 렌더러 초기화 및 그리기
        if (gridRenderer != null)
        {
            gridRenderer.InitializeRenderer(width, height, tileStates);
        }

        // 맵의 크기가 결정되면 Pathfinder의 노드들을 생성
        if (Pathfinder.Instance != null)
        {
            Pathfinder.Instance.InitializeNodes(width, height);
        }

        // 맵 초기 점령 비율 UI에 반영
        NotifyTerritoryChanged();
    }

    /// <summary>
    /// 게임 시작 시 상/하단 15~20% 영역을 Red/Blue 진영으로 지정하고,
    /// 중앙 60~70% 공간은 중립(Neutral) 타일 및 대칭형 장애물(Obstacle)로 배치하며
    /// 맵 크기에 비례하여 최소 4개~최대 16개의 무작위 스폰 포인트를 캐싱합니다.
    /// </summary>
    private void InitializeMapTerritory(MapStageConfig config)
    {
        blueTileIndices.Clear();
        redTileIndices.Clear();
        neutralTileIndices.Clear();
        blueSpawnTileIndices.Clear();
        redSpawnTileIndices.Clear();

        // 맵 높이 비례 약 20% 깊이를 스폰 영역으로 설정 (최소 2행 ~ 최대 5행)
        int spawnDepth = Mathf.Clamp(Mathf.RoundToInt(height * 0.2f), 2, 5);

        List<int> rawRedSpawnZone = new List<int>();
        List<int> rawBlueSpawnZone = new List<int>();

        for (int z = 0; z < height; z++)
        {
            TeamColor teamColor;
            if (z < spawnDepth)
            {
                teamColor = TeamColor.Red; // 북쪽 스폰존 (적군 Red)
            }
            else if (z >= height - spawnDepth)
            {
                teamColor = TeamColor.Blue; // 남쪽 스폰존 (아군 Blue)
            }
            else
            {
                teamColor = TeamColor.Neutral; // 중앙 중립 지역
            }

            int rowStartIndex = z * width;
            for (int x = 0; x < width; x++)
            {
                int index = rowStartIndex + x;
                tileStates[index] = teamColor;

                if (teamColor == TeamColor.Blue)
                {
                    blueTileIndices.Add(index);
                    rawBlueSpawnZone.Add(index);
                }
                else if (teamColor == TeamColor.Red)
                {
                    redTileIndices.Add(index);
                    rawRedSpawnZone.Add(index);
                }
                else if (teamColor == TeamColor.Neutral)
                {
                    neutralTileIndices.Add(index);
                }
            }
        }

        // 맵의 전체 타일 수에 비례하여 4개 ~ 16개 스폰 포인트 개수 결정
        int targetSpawnCount = Mathf.Clamp(Mathf.RoundToInt(totalTiles / 50f), 4, 16);

        // Blue / Red 스폰 존 타일 중 무작위로 targetSpawnCount 개수만큼 선정
        blueSpawnTileIndices = SelectRandomSpawnPoints(rawBlueSpawnZone, targetSpawnCount);
        redSpawnTileIndices = SelectRandomSpawnPoints(rawRedSpawnZone, targetSpawnCount);

        // 중앙 중립 지대에 맵 단계별 동적 장애물(Obstacle) 생성
        GenerateObstacles(config, spawnDepth);
    }

    /// <summary>
    /// 지정된 후보 타일 목록에서 무작위 뭉침(Clustering)을 방지하고
    /// 스폰 포인트들이 비교적 균등하게 분포되도록 최소 이격 거리 필터링을 거쳐 count 개수만큼 타일 인덱스를 추출합니다.
    /// </summary>
    private List<int> SelectRandomSpawnPoints(List<int> candidateIndices, int count)
    {
        List<int> result = new List<int>();
        if (candidateIndices == null || candidateIndices.Count == 0) return result;

        List<int> pool = new List<int>(candidateIndices);
        int extractCount = Mathf.Min(count, pool.Count);

        // 무작위 순서로 섞기 (Fisher-Yates Shuffle)
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int randIdx = Random.Range(0, i + 1);
            int temp = pool[i];
            pool[i] = pool[randIdx];
            pool[randIdx] = temp;
        }

        // 맵 크기 및 타일 개수에 비례한 초기 최소 이격 거리 설정 (기본 타일 크기의 3배 거리)
        float currentMinRadius = tileSize * 3.0f;

        // 후보군이 모자라지 않도록 최소 이격 거리를 단계별로 감쇄(Dampening)하며 탐색
        while (result.Count < extractCount && currentMinRadius >= 0.5f)
        {
            float sqrMinRadius = currentMinRadius * currentMinRadius;

            for (int i = pool.Count - 1; i >= 0; i--)
            {
                int candidateIdx = pool[i];
                Vector3 candidatePos = IndexToWorld(candidateIdx);

                bool isValid = true;
                for (int j = 0; j < result.Count; j++)
                {
                    Vector3 selectedPos = IndexToWorld(result[j]);
                    if ((candidatePos - selectedPos).sqrMagnitude < sqrMinRadius)
                    {
                        isValid = false; // 기존에 선택된 스폰 지점과 너무 가까우면 탈락
                        break;
                    }
                }

                if (isValid)
                {
                    result.Add(candidateIdx);
                    pool.RemoveAt(i);
                    if (result.Count >= extractCount) break;
                }
            }

            // 조건에 맞는 타일이 부족하면 최소 이격 거리 기준을 25%씩 줄여서 재탐색
            currentMinRadius *= 0.75f;
        }

        // 예외 보정: 모자란 개수는 남은 pool에서 순차 보충
        while (result.Count < extractCount && pool.Count > 0)
        {
            result.Add(pool[0]);
            pool.RemoveAt(0);
        }

        return result;
    }

    /// <summary>
    /// 캐싱된 스폰 포인트 중 건물이 설치되어 있지 않은 무작위 타일을 선택하여 월드 좌표를 반환합니다.
    /// 유닛 뭉침(Blobbing)을 방지하기 위해 소범위 무작위 오프셋(-0.5f ~ +0.5f)이 적용됩니다.
    /// </summary>
    public Vector3 GetRandomSpawnPosition(TeamColor teamColor)
    {
        List<int> spawnList = (teamColor == TeamColor.Blue) ? blueSpawnTileIndices : redSpawnTileIndices;
        List<int> fallbackList = (teamColor == TeamColor.Blue) ? blueTileIndices : redTileIndices;

        if (spawnList == null || spawnList.Count == 0)
        {
            spawnList = fallbackList;
        }

        int selectedIndex = -1;

        if (spawnList != null && spawnList.Count > 0)
        {
            // 건물 겹침을 방지하기 위해 건물이 없는 타일을 우선 선택 (최대 5회 무작위 재시도)
            for (int retry = 0; retry < 5; retry++)
            {
                int candidate = spawnList[Random.Range(0, spawnList.Count)];
                if (gridBuildings != null && candidate >= 0 && candidate < gridBuildings.Length)
                {
                    if (gridBuildings[candidate] == null)
                    {
                        selectedIndex = candidate;
                        break;
                    }
                }
                else
                {
                    selectedIndex = candidate;
                    break;
                }
            }

            // 5회 시도에도 건물이 설치된 타일만 걸릴 경우 무작위 1개 강제 선택
            if (selectedIndex < 0)
            {
                selectedIndex = spawnList[Random.Range(0, spawnList.Count)];
            }
        }

        Vector3 baseWorldPos = IndexToWorld(selectedIndex);

        // 유닛 뭉침 방지용 소범위 오프셋
        float offsetX = Random.Range(-0.5f, 0.5f);
        float offsetZ = Random.Range(-0.5f, 0.5f);

        return new Vector3(baseWorldPos.x + offsetX, baseWorldPos.y, baseWorldPos.z + offsetZ);
    }

    /// <summary>
    /// BattleContext에서 제공된 MapStageConfig 수치를 참조하여
    /// 중앙 중립 구역 내에 대칭형 장애물(2x2 단위 블록)을 생성합니다.
    /// </summary>
    private void GenerateObstacles(MapStageConfig config, int spawnDepth)
    {
        if (config.ObstacleMax <= 0) return;

        int obstacleCount = Random.Range(config.ObstacleMin, config.ObstacleMax + 1);
        if (obstacleCount <= 0) return;

        int centerZ = height / 2;
        int usableMinZ = spawnDepth + 1;
        int usableMaxZ = height - spawnDepth - 2;

        // 장애물 배치 템플릿 (2x2 대칭 블록)
        int leftX = Mathf.Max(1, width / 4 - 1);
        int rightX = Mathf.Min(width - 3, (width * 3) / 4);

        List<Vector2Int> obstacleCenters = new List<Vector2Int>();

        if (obstacleCount == 1)
        {
            obstacleCenters.Add(new Vector2Int(width / 2 - 1, centerZ - 1));
        }
        else if (obstacleCount <= 3)
        {
            obstacleCenters.Add(new Vector2Int(leftX, centerZ - 1));
            obstacleCenters.Add(new Vector2Int(rightX, centerZ - 1));
        }
        else
        {
            int offsetZ = Mathf.Max(2, (usableMaxZ - usableMinZ) / 4);
            obstacleCenters.Add(new Vector2Int(leftX, centerZ - offsetZ));
            obstacleCenters.Add(new Vector2Int(rightX, centerZ - offsetZ));
            obstacleCenters.Add(new Vector2Int(leftX, centerZ + offsetZ - 1));
            obstacleCenters.Add(new Vector2Int(rightX, centerZ + offsetZ - 1));
        }

        // 2x2 블록 형태로 Obstacle 타일 적용 (각 장애물마다 독립 무작위 -2~+2 오프셋 적용)
        foreach (var center in obstacleCenters)
        {
            int randX = Random.Range(-2, 3);
            int randZ = Random.Range(-2, 3);

            int finalCenterX = Mathf.Clamp(center.x + randX, 1, width - 3);
            int finalCenterZ = Mathf.Clamp(center.y + randZ, usableMinZ, usableMaxZ - 1);

            for (int dz = 0; dz < 2; dz++)
            {
                for (int dx = 0; dx < 2; dx++)
                {
                    int obX = finalCenterX + dx;
                    int obZ = finalCenterZ + dz;

                    if (obX >= 0 && obX < width && obZ >= usableMinZ && obZ <= usableMaxZ)
                    {
                        int index = obZ * width + obX;
                        if (tileStates[index] == TeamColor.Neutral)
                        {
                            neutralTileIndices.Remove(index);
                        }
                        tileStates[index] = TeamColor.Obstacle;
                    }
                }
            }
        }

        // 장애물 정적 캐시 테이블 빌드 (O(1) 조기 리턴용)
        BuildObstacleCacheTable();
    }

    /// <summary>
    /// 장애물 정적 지형 정보를 O(1) 배열로 미리 캐싱하여 유닛의 프레임별 탐색 연산을 극소화합니다.
    /// </summary>
    private void BuildObstacleCacheTable()
    {
        isObstacleTile = new bool[totalTiles];
        isNearObstacleTile = new bool[totalTiles];

        for (int i = 0; i < totalTiles; i++)
        {
            if (tileStates[i] == TeamColor.Obstacle)
            {
                isObstacleTile[i] = true;

                int cX = i % width;
                int cZ = i / width;

                for (int z = cZ - 1; z <= cZ + 1; z++)
                {
                    for (int x = cX - 1; x <= cX + 1; x++)
                    {
                        if (x >= 0 && x < width && z >= 0 && z < height)
                        {
                            int nearIndex = z * width + x;
                            isNearObstacleTile[nearIndex] = true;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 월드 좌표를 1차원 배열 인덱스로 변환 (맵 밖은 -1 반환)
    /// </summary>
    public int WorldToIndex(Vector3 worldPos)
    {
        float halfWidthWorld = width * tileSize * 0.5f;
        float halfHeightWorld = height * tileSize * 0.5f;

        int x = Mathf.FloorToInt((halfWidthWorld - worldPos.x) / tileSize);
        int z = Mathf.FloorToInt((halfHeightWorld - worldPos.z) / tileSize);

        if (x < 0 || x >= width || z < 0 || z >= height)
        {
            return -1;
        }

        return z * width + x;
    }

    /// <summary>
    /// 인덱스를 타일 중앙 월드 좌표로 반환
    /// </summary>
    public Vector3 IndexToWorld(int index)
    {
        if (index < 0 || index >= totalTiles)
        {
            return gridOrigin; 
        }

        int x = index % width;
        int z = index / width;

        float halfWidthWorld = width * tileSize * 0.5f;
        float halfHeightWorld = height * tileSize * 0.5f;

        return new Vector3(
            halfWidthWorld - (x * tileSize) - (tileSize * 0.5f),
            gridOrigin.y,
            halfHeightWorld - (z * tileSize) - (tileSize * 0.5f)
        );
    }

    /// <summary>
    /// 건물의 월드 좌표(중심점)를 기반으로 해당 건물이 시작하는 타일 인덱스를 계산
    /// </summary>
    private void GetStartIndicesFromCenter(Vector3 centerPos, int sizeX, int sizeZ, out int startX, out int startZ)
    {
        float halfWidthWorld = width * tileSize * 0.5f;
        float halfHeightWorld = height * tileSize * 0.5f;

        startX = Mathf.RoundToInt((halfWidthWorld - centerPos.x) / tileSize - sizeX * 0.5f);
        startZ = Mathf.RoundToInt((halfHeightWorld - centerPos.z) / tileSize - sizeZ * 0.5f);
    }

    /// <summary>
    /// 좌표와 건물 사이즈를 입력받아 그리드 칸에 완벽하게 맞춰진 월드 좌표를 반환
    /// </summary>
    public Vector3 GetSnappedPosition(Vector3 rawWorldPos, int sizeX, int sizeZ)
    {
        GetStartIndicesFromCenter(rawWorldPos, sizeX, sizeZ, out int startX, out int startZ);

        // 맵 밖을 벗어나는 경우 제한(Clamp)
        startX = Mathf.Clamp(startX, 0, width - sizeX);
        startZ = Mathf.Clamp(startZ, 0, height - sizeZ);

        float halfWidthWorld = width * tileSize * 0.5f;
        float halfHeightWorld = height * tileSize * 0.5f;

        float snappedX = halfWidthWorld - (startX * tileSize) - (sizeX * tileSize * 0.5f);
        float snappedZ = halfHeightWorld - (startZ * tileSize) - (sizeZ * tileSize * 0.5f);

        return new Vector3(snappedX, rawWorldPos.y, snappedZ);
    }

    /// <summary>
    /// 건물 건설 가능 여부 확인
    /// </summary>
    public bool CanBuild(Vector3 centerPos, int sizeX, int sizeZ, TeamColor teamColor)
    {
        GetStartIndicesFromCenter(centerPos, sizeX, sizeZ, out int startX, out int startZ);

        // 전체 면적이 맵 내부에 있는지 검사
        if (startX < 0 || startX + sizeX > width || startZ < 0 || startZ + sizeZ > height)
        {
            return false;
        }

        for (int z = 0; z < sizeZ; z++)
        {
            // 최적화: 매 row의 시작 인덱스를 미리 계산
            int rowStartIndex = (startZ + z) * width + startX;
            for (int x = 0; x < sizeX; x++)
            {
                int index = rowStartIndex + x;

                // 조건: 소유권이 일치해야 하고, 이미 건물이 있으면 안 됨
                if (tileStates[index] != teamColor) return false;
                if (gridBuildings[index] != null) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 건물 배치 처리
    /// </summary>
    public void PlaceBuilding(Vector3 centerPos, int sizeX, int sizeZ, TowerBase buildingObj)
    {
        GetStartIndicesFromCenter(centerPos, sizeX, sizeZ, out int startX, out int startZ);

        // 안전을 위한 바운더리 체크
        if (startX < 0 || startX + sizeX > width || startZ < 0 || startZ + sizeZ > height)
        {
            return;
        }

        for (int z = 0; z < sizeZ; z++)
        {
            int rowStartIndex = (startZ + z) * width + startX;
            for (int x = 0; x < sizeX; x++)
            {
                gridBuildings[rowStartIndex + x] = buildingObj;
            }
        }
    }

    /// <summary>
    /// 건물 철거/파괴 처리
    /// </summary>
    public void RemoveBuilding(Vector3 centerPos, int sizeX, int sizeZ)
    {
        GetStartIndicesFromCenter(centerPos, sizeX, sizeZ, out int startX, out int startZ);

        if (startX < 0 || startX + sizeX > width || startZ < 0 || startZ + sizeZ > height)
        {
            return;
        }

        for (int z = 0; z < sizeZ; z++)
        {
            int rowStartIndex = (startZ + z) * width + startX;
            for (int x = 0; x < sizeX; x++)
            {
                gridBuildings[rowStartIndex + x] = null;
            }
        }
    }

    /// <summary>
    /// 단일 타일 색상(소유권) 칠하기
    /// </summary>
    public bool PaintTile(Vector3 worldPos, TeamColor teamColor)
    {
        int index = WorldToIndex(worldPos);

        // 맵 밖인 경우
        if (index == -1) return false;

        // 장애물 타일인 경우 칠할 수 없음
        if (tileStates[index] == TeamColor.Obstacle) return false;

        // 이미 건물이 있는 경우 칠할 수 없음
        if (gridBuildings[index] != null) return false;

        // 이미 같은 색상인 경우 변경 불필요
        if (tileStates[index] == teamColor) return false;

        // 기존 색상 캐시 리스트에서 제거
        TeamColor prevColor = tileStates[index];
        if (prevColor == TeamColor.Blue)
        {
            blueTileIndices.Remove(index);
        }
        else if (prevColor == TeamColor.Red)
        {
            redTileIndices.Remove(index);
        }
        else if (prevColor == TeamColor.Neutral)
        {
            neutralTileIndices.Remove(index);
        }

        // 조건 충족: 상태 변경
        tileStates[index] = teamColor;

        // 신규 색상 캐시 리스트에 추가
        if (teamColor == TeamColor.Blue)
        {
            blueTileIndices.Add(index);
        }
        else if (teamColor == TeamColor.Red)
        {
            redTileIndices.Add(index);
        }
        else if (teamColor == TeamColor.Neutral)
        {
            neutralTileIndices.Add(index);
        }

        // 시각적 업데이트 추가 (Renderer 연동 - 큐 기반)
        if (gridRenderer != null)
        {
            int x = index % width;
            int z = index / width;
            gridRenderer.QueueTileUpdate(x, z);
        }

        // 타일 점령 상태 변경 이벤트 통지
        NotifyTerritoryChanged();

        return true;
    }

    /// <summary>
    /// 점령 타일 수 통지 헬퍼 메서드 
    /// </summary>
    public void NotifyTerritoryChanged()
    {
        int blueCount = blueTileIndices.Count;
        int neutralCount = neutralTileIndices.Count;
        int redCount = redTileIndices.Count;
        OnTerritoryChanged?.Invoke(blueCount, neutralCount, redCount);
    }

    /// <summary>
    /// 유닛의 소속 타일 인덱스를 갱신
    /// </summary>
    public void UpdateUnitTile(UnitBase unit, int oldIndex, int newIndex)
    {
        if (oldIndex != -1 && oldIndex >= 0 && oldIndex < totalTiles)
        {
            RemoveUnitFromTile(unit, oldIndex);
        }

        if (newIndex != -1 && newIndex >= 0 && newIndex < totalTiles)
        {
            gridUnits[newIndex].Add(unit);
        }
    }

    /// <summary>
    /// 타일에서 유닛을 제거
    /// </summary>
    public void RemoveUnitFromTile(UnitBase unit, int index)
    {
        if (index < 0 || index >= totalTiles) return;

        var list = gridUnits[index];
        int idx = list.IndexOf(unit);
        if (idx != -1)
        {
            int lastIdx = list.Count - 1;
            list[idx] = list[lastIdx];
            list.RemoveAt(lastIdx);
        }
    }

    /// <summary>
    /// 특정 타일에 속한 유닛 리스트를 반환
    /// </summary>
    public List<UnitBase> GetUnitsInTile(int index)
    {
        if (index < 0 || index >= totalTiles) return null;
        return gridUnits[index];
    }
}
