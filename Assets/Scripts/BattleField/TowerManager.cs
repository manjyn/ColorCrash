using UnityEngine;
using ColorCrash.Tower;

/// <summary>
/// 타워(건물) 관련 전역 관리를 담당하는 매니저 클래스.
/// UnitManager와 대칭되는 구조를 가지며, TowerDatabase 및 향후 타워 풀링/수명 주기를 관리합니다.
/// </summary>
public class TowerManager : MonoBehaviour
{
    public static TowerManager Instance { get; private set; }

    [Header("Tower Database")]
    [SerializeField] private TowerDatabase towerDatabase;
    public TowerDatabase TowerDatabase => towerDatabase;

    [Header("Tower Prefab")]
    public TowerBase towerPrefabBarricade;
    public TowerBase towerPrefabCannon;
    public TowerBase towerPrefabMortar;

    // 오브젝트 풀링을 위한 딕셔너리와 컨테이너
    private System.Collections.Generic.Dictionary<string, System.Collections.Generic.Queue<TowerBase>> towerPools = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Queue<TowerBase>>();
    private Transform poolContainer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 타워 풀 컨테이너 초기화
        poolContainer = new GameObject("TowerPool").transform;
        poolContainer.SetParent(transform);
    }

    private void Start()
    {
        // TowerDatabase 초기화 (빠른 O(1) 검색을 위한 캐싱)
        if (towerDatabase != null)
        {
            towerDatabase.Initialize();
        }
        else
        {
            Debug.LogError("[TowerManager] TowerDatabase가 할당되지 않았습니다.");
        }
    }

    /// <summary>
    /// 타워를 풀에서 꺼내오거나 새로 생성합니다.
    /// </summary>
    public TowerBase SpawnTower(TowerBase prefab, Vector3 position, TeamColor teamColor)
    {
        if (prefab == null) return null;

        string key = prefab.name;

        if (!towerPools.ContainsKey(key))
        {
            towerPools[key] = new System.Collections.Generic.Queue<TowerBase>();
        }

        TowerBase tower = null;
        if (towerPools[key].Count > 0)
        {
            tower = towerPools[key].Dequeue();
            tower.transform.position = position;
            tower.transform.rotation = Quaternion.identity;
        }
        else
        {
            tower = Instantiate(prefab, position, Quaternion.identity, poolContainer);
            tower.name = key; // Clone 접미사 제거를 위해 원본 이름 유지
        }

        if (tower != null)
        {
            tower.gameObject.SetActive(true);
            tower.Initialize(teamColor);
        }

        return tower;
    }

    /// <summary>
    /// 타워가 파괴될 때 호출되어 메모리에서 삭제하지 않고 풀로 반환합니다.
    /// </summary>
    public void DespawnTower(TowerBase tower)
    {
        if (tower == null) return;

        tower.gameObject.SetActive(false);

        string key = tower.name;
        if (!towerPools.ContainsKey(key))
        {
            towerPools[key] = new System.Collections.Generic.Queue<TowerBase>();
        }

        towerPools[key].Enqueue(tower);
    }
}
