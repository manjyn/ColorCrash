using UnityEngine;
using System.Collections.Generic;
using ColorCrash.Units;
using ColorCrash.Projectiles;

/// <summary>
/// 마법 스펠 (Ink Bomb) 전용 스폰 핸들러.
/// 시전 시 Grid Map 바깥 상공에서 포물선 투사체가 날아와 3x3 타일에 착탄하여 폭발 및 50 데미지를 입힙니다.
/// </summary>
public class SpellSpawnHandler : ISpawnHandler
{
    private int spellRadius;
    private float spellDamage;

    // 다중 타일 점유 건물 중복 타격 방지 및 GC Zero를 위한 스태틱 캐시
    private static readonly HashSet<TowerBase> hitTowersCache = new HashSet<TowerBase>();

    // 팀별 단 1개 전용 인스턴스 (Zero Instantiation) 캐시
    private static InkBombProjectile blueTeamProjectile;
    private static InkBombProjectile redTeamProjectile;

    /// <summary>
    /// 생성자. 기본적으로 3x3 영역(radius 1 = 3x3) 및 데미지 50 설정.
    /// </summary>
    public SpellSpawnHandler(int radius = 1, float damage = 50f)
    {
        spellRadius = radius;
        spellDamage = damage;
    }

    public Vector3 UpdatePreviewPosition(Vector3 raycastHitPoint, GameObject dummy)
    {
        if (GridManager.Instance != null)
        {
            // 스펠 위치를 타일 그리드 중심점으로 스냅
            Vector3 snapped = GridManager.Instance.GetSnappedPosition(raycastHitPoint, 1, 1);
            if (dummy != null)
            {
                dummy.transform.position = snapped;
            }
            return snapped;
        }

        if (dummy != null)
        {
            dummy.transform.position = raycastHitPoint;
        }
        return raycastHitPoint;
    }

    /// <summary>
    /// 전장 타일 내부에만 위치하면 어디든 시전 가능.
    /// </summary>
    public bool IsValidPosition(Vector3 position, TeamColor team)
    {
        if (GridManager.Instance == null) return false;
        int tileIndex = GridManager.Instance.WorldToIndex(position);
        return tileIndex != -1;
    }

    /// <summary>
    /// 지정된 위치 중심 3x3 범위 타일을 향해 Grid Map 바깥 상공에서 잉크 폭탄 투사체를 발사합니다.
    /// </summary>
    public void ExecuteSpawn(Vector3 position, TeamColor team)
    {
        if (GridManager.Instance == null) return;

        int centerIndex = GridManager.Instance.WorldToIndex(position);
        if (centerIndex == -1) return;

        int width = GridManager.Instance.width;

        int cX = centerIndex % width;
        int cZ = centerIndex / width;

        LaunchInkBombSpell(new Vector2Int(cX, cZ), team, spellDamage, spellRadius);
    }

    /// <summary>
    /// Grid Map 밖 상공에서 출발하는 잉크 폭탄 투사체를 발사합니다.
    /// (플레이어 및 AI 공통 시전 연동)
    /// </summary>
    public static void LaunchInkBombSpell(Vector2Int targetCenterTile, TeamColor team, float damage = 50f, int radius = 1)
    {
        if (GridManager.Instance == null) return;

        InkBombProjectile proj = GetOrCreateTeamProjectile(team);
        if (proj == null)
        {
            // 예외 처리: 즉시 도색 및 데미지
            ExecuteSpellAtTile(targetCenterTile, team, damage, radius);
            return;
        }

        Vector3 startOrigin = GetLaunchOrigin(team);
        int targetIndex = targetCenterTile.y * GridManager.Instance.width + targetCenterTile.x;
        Vector3 targetWorldPos = GridManager.Instance.IndexToWorld(targetIndex);

        proj.Launch(startOrigin, targetWorldPos, targetCenterTile, team, damage, radius);
    }

    /// <summary>
    /// GridManager의 맵 크기에 맞춰 진영별 맵 밖 상공 발포 시작점 계산
    /// </summary>
    public static Vector3 GetLaunchOrigin(TeamColor team)
    {
        if (GridManager.Instance == null) return Vector3.zero;

        float width = GridManager.Instance.width;
        float height = GridManager.Instance.height;
        float tileSize = GridManager.tileSize;
        Vector3 origin = GridManager.Instance.gridOrigin;

        float centerX = origin.x + (width * tileSize * 0.5f);
        float flyHeight = origin.y + 14f; // 상공 고도

        if (team == TeamColor.Blue)
        {
            // 유저 (남쪽 바깥)
            float launchZ = origin.z - (tileSize * 2.5f);
            return new Vector3(centerX, flyHeight, launchZ);
        }
        else
        {
            // 적 AI (북쪽 바깥)
            float launchZ = origin.z + (height * tileSize) + (tileSize * 2.5f);
            return new Vector3(centerX, flyHeight, launchZ);
        }
    }

    /// <summary>
    /// 팀별 단 1개 전용 InkBombProjectile 객체 런타임 생성 및 캐싱
    /// </summary>
    private static InkBombProjectile GetOrCreateTeamProjectile(TeamColor team)
    {
        InkBombProjectile targetRef = (team == TeamColor.Blue) ? blueTeamProjectile : redTeamProjectile;

        if (targetRef == null)
        {
            GameObject projGo = new GameObject($"InkBombProjectile_{team}");
            Object.DontDestroyOnLoad(projGo);

            // 간단한 구체 메쉬 및 TrailRenderer 추가
            GameObject meshObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meshObj.transform.SetParent(projGo.transform);
            meshObj.transform.localPosition = Vector3.zero;
            meshObj.transform.localScale = new Vector3(8f, 8f, 8f);
            Object.Destroy(meshObj.GetComponent<Collider>()); // 물리 비활성화

            MeshRenderer mr = meshObj.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            if (mat.shader == null) mat = new Material(Shader.Find("Particles/Standard Unlit"));
            mr.material = mat;

            TrailRenderer tr = projGo.AddComponent<TrailRenderer>();
            tr.time = 0.3f;
            tr.startWidth = 5f;
            tr.endWidth = 0.1f;
            tr.material = mat;

            targetRef = projGo.AddComponent<InkBombProjectile>();
            projGo.SetActive(false);

            if (team == TeamColor.Blue)
                blueTeamProjectile = targetRef;
            else
                redTeamProjectile = targetRef;
        }

        return targetRef;
    }

    /// <summary>
    /// 타일 좌표 중심 3x3 (또는 radius) 영역에 잉크 폭탄 마법을 발동합니다.
    /// (투사체 착탄 완료 시 호출됨)
    /// </summary>
    public static void ExecuteSpellAtTile(Vector2Int centerTile, TeamColor team, float damage = 50f, int radius = 1)
    {
        if (GridManager.Instance == null) return;

        int width = GridManager.Instance.width;
        int height = GridManager.Instance.height;
        int cX = centerTile.x;
        int cZ = centerTile.y;

        // GC Zero를 위해 중복 피격 검사용 스태틱 HashSet 비우기
        hitTowersCache.Clear();

        for (int z = cZ - radius; z <= cZ + radius; z++)
        {
            for (int x = cX - radius; x <= cX + radius; x++)
            {
                if (x >= 0 && x < width && z >= 0 && z < height)
                {
                    int index = z * width + x;
                    Vector3 tilePos = GridManager.Instance.IndexToWorld(index);

                    // 1. 타일 도색
                    GridManager.Instance.PaintTile(tilePos, team);

                    // 2. 적 건물 데미지 (GridManager.gridBuildings O(1) 참조 및 중복 타격 방지)
                    if (GridManager.Instance.gridBuildings != null && index < GridManager.Instance.gridBuildings.Length)
                    {
                        TowerBase building = GridManager.Instance.gridBuildings[index];
                        if (building != null && building.TeamColor != team && !hitTowersCache.Contains(building))
                        {
                            hitTowersCache.Add(building);
                            building.TakeDamage(damage);
                        }
                    }

                    // 3. 적 유닛 데미지 (GridManager.gridUnits O(1) 참조)
                    if (GridManager.Instance.gridUnits != null && index < GridManager.Instance.gridUnits.Length)
                    {
                        List<UnitBase> unitsOnTile = GridManager.Instance.gridUnits[index];
                        if (unitsOnTile != null && unitsOnTile.Count > 0)
                        {
                            // 역순 순회하여 유닛 파괴/데미지 시 리스트 변경 안전성 보장
                            for (int i = unitsOnTile.Count - 1; i >= 0; i--)
                            {
                                UnitBase unit = unitsOnTile[i];
                                if (unit != null && unit.teamColor != team && !unit.IsDead)
                                {
                                    unit.TakeDamage(damage);
                                }
                            }
                        }
                    }
                }
            }
        }

        hitTowersCache.Clear();
        Debug.Log($"[SpellSpawnHandler] Ink Bomb 마법 발동 완료! 타일: ({cX}, {cZ}), 팀: {team}, 데미지: {damage}");
    }

    /// <summary>
    /// 3x3 범위 하이라이트 표시
    /// </summary>
    public Vector2Int GetOccupiedSize()
    {
        int size = spellRadius * 2 + 1;
        return new Vector2Int(size, size);
    }
}


