using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ColorCrash.Units;

/// <summary>
/// 대규모 타일 강탈 디펜스 게임의 유닛 스폰 및 오브젝트 풀링 관리자
/// 가비지 컬렉터(GC) 스파이크를 방지하기 위해 딕셔너리 기반의 프리팹 풀링을 지원합니다.
/// </summary>
public class UnitManager : MonoBehaviour
{
    public static UnitManager Instance { get; private set; }

    [Header("Unit Database")]
    [SerializeField] private UnitDatabase unitDatabase;
    public UnitDatabase UnitDatabase => unitDatabase;

    [Header("Unit Prefab")]
    public UnitBase unitPrefabFootman;
    public UnitBase unitPrefabElite;
    public UnitBase unitPrefabWarlord;
    public UnitBase unitPrefabArcher;

    [Header("Pool Settings")]
    [Tooltip("게임 시작 시 미리 생성할 진영별 유닛 최대 개수 (동적 풀링 기본값)")]
    public int initialPoolSizePerPrefab = 20;

    // 프리팹의 이름을 Key로 사용하는 딕셔너리 기반 풀 구조
    private Dictionary<string, Queue<UnitBase>> unitPools = new Dictionary<string, Queue<UnitBase>>();
    private Transform poolContainer;

    // 활성화된 유닛 리스트 (진영별)
    public List<UnitBase> activeBlueUnits = new List<UnitBase>();
    public List<UnitBase> activeRedUnits = new List<UnitBase>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (unitDatabase != null)
        {
            unitDatabase.Initialize();
        }
        else
        {
            Debug.LogError("[UnitManager] UnitDatabase 에셋 참조가 누락되었습니다.");
        }

        poolContainer = new GameObject("UnitPool").transform;
        poolContainer.SetParent(transform);
    }

    private void Start()
    {
        // [피드백 적용] 웨이브 자체를 비활성화 (추후 개발 예정)
        // StartCoroutine(WaveRoutine());
    }

    /// <summary>
    /// 입력받은 프리팹을 오브젝트 풀에서 꺼내 활성화합니다.
    /// 풀이 비어있으면 새로 생성하여 할당합니다.
    /// </summary>
    public void SpawnUnit(UnitBase prefab, TeamColor teamColor, Vector3 basePos)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[UnitManager] 스폰하려는 유닛 프리팹이 null입니다.");
            return;
        }

        string key = prefab.name;

        // 해당 프리팹 전용 풀이 없다면 생성
        if (!unitPools.ContainsKey(key))
        {
            unitPools[key] = new Queue<UnitBase>();
        }

        // [핵심 최적화 1] 유닛 뭉침(Blobbing) 방지 - 위치 계산을 인스턴스화 전에 선행
        float offsetX = Random.Range(-0.5f, 0.5f);
        float offsetZ = Random.Range(-0.5f, 0.5f);
        Vector3 spawnPos = new Vector3(basePos.x + offsetX, basePos.y, basePos.z + offsetZ);

        Queue<UnitBase> pool = unitPools[key];
        UnitBase unit = null;

        if (pool.Count > 0)
        {
            unit = pool.Dequeue();
            // 풀에서 꺼낸 유닛은 활성화 전에 위치 설정
            unit.transform.position = spawnPos;
        }
        else
        {
            // 풀이 비어있다면 새로 인스턴스화할 때 생성 위치와 회전값을 매개변수로 전달
            // 이를 통해 OnEnable()이 즉시 실행되어도 올바른 위치에서 타겟팅이 시작됨
            unit = Instantiate(prefab, spawnPos, Quaternion.identity, poolContainer);
            unit.name = key; // (Clone) 접미사 제거를 위해 이름을 프리팹 명으로 고정
        }

        if (unit != null)
        {
            // 이전 사이클의 잔재 타일 인덱스 및 이동 상태 강제 초기화
            unit.currentTileIndex = -1;
            unit.ResetMovementState();

            // Initialize 호출하여 DB 스탯 및 팀 컬러 세팅 (이 안에서 unit.moveSpeed도 기본값으로 세팅됨)
            unit.Initialize(teamColor);

            // [핵심 최적화 2] 대열의 자연스러운 속도 편차 부여 (반드시 Initialize 이후에 적용)
            unit.moveSpeed *= Random.Range(0.95f, 1.05f);

            // 유닛 활성화 (여기서 OnEnable이 실행되며 FSM 코루틴 시작)
            unit.gameObject.SetActive(true);

            // 활성 목록에 추가
            if (teamColor == TeamColor.Blue)
                activeBlueUnits.Add(unit);
            else
                activeRedUnits.Add(unit);
        }
    }

    /// <summary>
    /// 유닛의 체력이 0이 되거나 게임 리셋 시 호출되어 풀로 반환합니다.
    /// </summary>
    public void DespawnUnit(UnitBase unit)
    {
        if (unit == null) return;

        unit.ResetMovementState();

        if (GridManager.Instance != null)
        {
            GridManager.Instance.RemoveUnitFromTile(unit, unit.currentTileIndex);
        }

        // 활성 목록에서 제거
        if (unit.teamColor == TeamColor.Blue)
            activeBlueUnits.Remove(unit);
        else
            activeRedUnits.Remove(unit);

        // 전투 중 Destroy 금지, 비활성화로 재사용
        unit.gameObject.SetActive(false);

        string key = unit.name;

        if (!unitPools.ContainsKey(key))
        {
            unitPools[key] = new Queue<UnitBase>();
        }

        unitPools[key].Enqueue(unit);
    }

    // [피드백 적용] 웨이브 시스템 비활성화 (추후 재개발 예정)
    /*
    private IEnumerator WaveRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(waveInterval);
            StartCoroutine(SpawnWaveProcess());
        }
    }

    private IEnumerator SpawnWaveProcess()
    {
        if (blueNexus == null || redNexus == null)
        {
            //Debug.LogWarning("[UnitManager] 본진(Nexus) Transform이 지정되지 않았습니다.");
            yield break;
        }

        Vector3 bluePos = blueNexus.position;
        Vector3 redPos = redNexus.position;

        for (int i = 0; i < unitsPerWave; i++)
        {
            SpawnUnit(TeamColor.Blue, bluePos);
            SpawnUnit(TeamColor.Red, redPos);

            // N마리를 동시에 스폰하지 않고 지연 시간(0.1초)을 두어 순차 스폰 (줄지어 이동 유도)
            yield return new WaitForSeconds(spawnDelay);
        }
    }
    */
}

