using UnityEngine;

public class TowerBarricade : TowerObstacle
{
    private void Awake()
    {
        sizeX = 4;
        sizeZ = 1;
        buildingName = "Barricade";
    }

    public override void OnRemoved()
    {
        // 파괴 이펙트, 사운드 등 처리
        Debug.Log($"{buildingName}이(가) 파괴되었습니다.");
        
        if (GridManager.Instance != null)
        {
            GridManager.Instance.RemoveBuilding(transform.position, SizeX, SizeZ);
        }

        // 풀링 반환
        if (TowerManager.Instance != null)
        {
            TowerManager.Instance.DespawnTower(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
