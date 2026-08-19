using UnityEngine;
using System.Collections.Generic;
using ColorCrash.Projectiles;

/// <summary>
/// 모든 발사체의 글로벌 오브젝트 풀링과 밸런스 데이터베이스를 통제하는 매니저.
/// </summary>
public class ProjectileManager : MonoBehaviour
{
    public static ProjectileManager Instance { get; private set; }

    [Header("Projectile Database")]
    [SerializeField] private ProjectileDatabase projectileDatabase;
    public ProjectileDatabase ProjectileDatabase => projectileDatabase;

    // 풀링 자료구조
    private Dictionary<string, Queue<ProjectileBase>> projectilePools = new Dictionary<string, Queue<ProjectileBase>>();
    private Transform poolContainer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        poolContainer = new GameObject("ProjectilePool").transform;
        poolContainer.SetParent(transform);
    }

    private void Start()
    {
        if (projectileDatabase != null)
        {
            projectileDatabase.Initialize();
        }
        else
        {
            Debug.LogError("[ProjectileManager] ProjectileDatabase가 할당되지 않았습니다.");
        }
    }

    /// <summary>
    /// 풀에서 발사체를 꺼내오거나 새로 생성하여 반환합니다.
    /// </summary>
    public ProjectileBase SpawnProjectile(ProjectileBase prefab, Vector3 startPos)
    {
        if (prefab == null) return null;

        string key = prefab.name;

        if (!projectilePools.ContainsKey(key))
        {
            projectilePools[key] = new Queue<ProjectileBase>();
        }

        ProjectileBase proj = null;
        if (projectilePools[key].Count > 0)
        {
            proj = projectilePools[key].Dequeue();
            proj.transform.position = startPos;
        }
        else
        {
            proj = Instantiate(prefab, startPos, Quaternion.identity, poolContainer);
            proj.name = key; // (Clone) 이름 고정
        }

        proj.gameObject.SetActive(true);
        return proj;
    }

    /// <summary>
    /// 수명을 다한 발사체를 비활성화시키고 풀로 반납합니다.
    /// </summary>
    public void DespawnProjectile(ProjectileBase proj)
    {
        if (proj == null) return;

        proj.gameObject.SetActive(false);

        string key = proj.name;
        if (!projectilePools.ContainsKey(key))
        {
            projectilePools[key] = new Queue<ProjectileBase>();
        }

        projectilePools[key].Enqueue(proj);
    }
}
