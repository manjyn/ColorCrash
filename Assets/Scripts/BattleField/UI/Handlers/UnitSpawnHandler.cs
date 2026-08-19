using UnityEngine;
using ColorCrash.Units;

/// <summary>
/// 유닛 전용 스폰 핸들러.
/// 마우스를 부드럽게 따라다니며, 그리드 스냅 없이 1x1 칸의 유효성만 검사하여 생성.
/// </summary>
public class UnitSpawnHandler : ISpawnHandler
{
    private UnitBase unitPrefab;
    
    /// <summary>
    /// 생성자: 스폰할 유닛 프리팹 캐싱.
    /// </summary>
    public UnitSpawnHandler(UnitBase prefab)
    {
        unitPrefab = prefab;
    }

    /// <summary>
    /// 건물과 달리 그리드 스냅 미적용, 마우스 포인터의 위치(raycastHitPoint) 그대로 반환.
    /// </summary>
    public Vector3 UpdatePreviewPosition(Vector3 raycastHitPoint, GameObject dummy)
    {
        if (dummy != null)
        {
            dummy.transform.position = raycastHitPoint;
        }
        return raycastHitPoint;
    }

    /// <summary>
    /// 마우스 포인트가 위치한 해당 1칸의 타일이 아군 영토인지 검사.
    /// </summary>
    public bool IsValidPosition(Vector3 position, TeamColor team)
    {
        if (GridManager.Instance == null) return false;
        int tileIndex = GridManager.Instance.WorldToIndex(position);
        if (tileIndex == -1) return false;
        return GridManager.Instance.tileStates[tileIndex] == team;
    }

    /// <summary>
    /// UnitManager를 통해 실제 유닛 객체를 스폰 리스트에 등록 후 생성.
    /// </summary>
    public void ExecuteSpawn(Vector3 position, TeamColor team)
    {
        if (UnitManager.Instance != null && unitPrefab != null)
        {
            UnitManager.Instance.SpawnUnit(unitPrefab, team, position);
        }
    }

    /// <summary>
    /// 유닛은 기본적으로 1칸(1x1) 면적만 검사하고 하이라이트하므로 (1, 1) 반환.
    /// </summary>
    public Vector2Int GetOccupiedSize()
    {
        return new Vector2Int(1, 1);
    }
}
