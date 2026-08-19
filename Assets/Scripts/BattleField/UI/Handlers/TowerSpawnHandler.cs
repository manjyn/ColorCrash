using UnityEngine;

/// <summary>
/// 건물(타워) 전용 스폰 핸들러.
/// 건물 크기(sizeX, sizeZ)에 맞춰 그리드 단위로 스냅 이동하며 타일 점유 데이터 갱신.
/// </summary>
public class TowerSpawnHandler : ISpawnHandler
{
    private TowerBase towerPrefab;
    private int sizeX;
    private int sizeZ;

    /// <summary>
    /// 생성자: 스폰할 타워 프리팹을 입력받아 크기(가로/세로) 캐싱.
    /// </summary>
    public TowerSpawnHandler(TowerBase prefab)
    {
        towerPrefab = prefab;
        sizeX = prefab.SizeX;
        sizeZ = prefab.SizeZ;

        // DB에서 최신 스탯(크기)을 가져와 프리뷰에 반영합니다.
        if (TowerManager.Instance != null && TowerManager.Instance.TowerDatabase != null)
        {
            if (TowerManager.Instance.TowerDatabase.TryGetRecord(prefab.TowerId, out var record))
            {
                sizeX = record.SizeX;
                sizeZ = record.SizeZ;
            }
        }
    }

    /// <summary>
    /// 마우스 위치를 그리드 칸(TileSize) 단위에 맞추어 스냅(Snap) 갱신.
    /// </summary>
    public Vector3 UpdatePreviewPosition(Vector3 raycastHitPoint, GameObject dummy)
    {
        if (GridManager.Instance == null) return raycastHitPoint;
        
        // 중심점을 기반으로 한 그리드 스냅 좌표 계산
        Vector3 snapped = GridManager.Instance.GetSnappedPosition(raycastHitPoint, sizeX, sizeZ);
        if (dummy != null)
        {
            dummy.transform.position = snapped;
        }
        return snapped;
    }

    /// <summary>
    /// 선택한 위치의 타워 건설 가능 여부를 GridManager를 통해 판별. (영토 소유권, 중복 배치 여부)
    /// </summary>
    public bool IsValidPosition(Vector3 position, TeamColor team)
    {
        if (GridManager.Instance == null) return false;
        return GridManager.Instance.CanBuild(position, sizeX, sizeZ, team);
    }

    /// <summary>
    /// 타워 객체를 생성하고 GridManager에 점유 데이터 등록.
    /// </summary>
    public void ExecuteSpawn(Vector3 position, TeamColor team)
    {
        if (GridManager.Instance != null && towerPrefab != null && TowerManager.Instance != null)
        {
            // 오브젝트 풀링 적용: 매번 Instantiate 하지 않고 풀에서 가져오거나 생성
            TowerBase newTower = TowerManager.Instance.SpawnTower(towerPrefab, position, team);
            
            if (newTower != null)
            {
                GridManager.Instance.PlaceBuilding(position, newTower.SizeX, newTower.SizeZ, newTower);
            }
        }
    }

    /// <summary>
    /// 타워가 차지하는 크기(sizeX, sizeZ) 반환.
    /// </summary>
    public Vector2Int GetOccupiedSize()
    {
        return new Vector2Int(sizeX, sizeZ);
    }
}
