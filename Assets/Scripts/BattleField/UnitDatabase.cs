using System.Collections.Generic;
using UnityEngine;

namespace ColorCrash.Units
{
    /// <summary>
    /// 단일 유닛의 수치 데이터를 정의하는 직렬화 구조체
    /// </summary>
    [System.Serializable]
    public struct UnitDataRecord
    {
        [SerializeField] private string unitId;
        [SerializeField] private string unitName;
        [SerializeField] private float maxHp;
        [SerializeField] private float defense;
        [SerializeField] private float moveSpeed;
        [SerializeField] private float meleeAttackDamage;
        [SerializeField] private float meleeAttackRange;  // 근접 공격 범위
        [SerializeField] private float meleeAttackCooldown;
        [SerializeField] private float rangeAttackDamage;
        [SerializeField] private float rangeAttackRange;  // 원거리 공격 범위
        [SerializeField] private float rangeAttackCooldown;
        [SerializeField] private float searchRadius; // 현재 사용하지 않음
        [SerializeField] private float separationRadius; // 유닛끼리 서로 겹치지 않게 밀어내는 범위
        [SerializeField] private int inkCost;
        [SerializeField] private float tileCaptureDelay;
        [SerializeField] private int requiredTileHits;

        public string UnitId => unitId;
        public string UnitName => unitName;
        public float MaxHp => maxHp;
        public float Defense => defense;
        public float MoveSpeed => moveSpeed;
        public float MeleeAttackDamage => meleeAttackDamage;
        public float MeleeAttackRange => meleeAttackRange;
        public float MeleeAttackCooldown => meleeAttackCooldown;
        public float RangeAttackDamage => rangeAttackDamage;
        public float RangeAttackRange => rangeAttackRange;
        public float RangeAttackCooldown => rangeAttackCooldown;
        public float SearchRadius => searchRadius;
        public float SeparationRadius => separationRadius;
        public int InkCost => inkCost;
        public float TileCaptureDelay
        {
            get
            {
                if (tileCaptureDelay > 0f) return tileCaptureDelay;
                return 0.3f;
            }
        }
        public int RequiredTileHits
        {
            get
            {
                if (requiredTileHits > 0) return requiredTileHits;
                return 1;
            }
        }
    }

    /// <summary>
    /// 모든 유닛의 능력치를 하나의 에셋에서 통합 관리하는 ScriptableObject
    /// 런타임 최적화를 위해 빠른 O(1) 탐색을 지원하는 Dictionary 구조를 내부적으로 초기화합니다.
    /// </summary>
    //[CreateAssetMenu(fileName = "UnitDatabase", menuName = "ColorCrash/Unit Database")]
    public class UnitDatabase : ScriptableObject
    {
        [SerializeField] private List<UnitDataRecord> records = new List<UnitDataRecord>();

        private Dictionary<string, UnitDataRecord> recordDict = new Dictionary<string, UnitDataRecord>();

        /// <summary>
        /// 런타임 빠른 검색을 위해 리스트의 데이터를 딕셔너리로 빌드합니다.
        /// 게임 시작 시 UnitManager 등에서 최초 1회 호출되어야 합니다.
        /// </summary>
        public void Initialize()
        {
            recordDict.Clear();
            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (string.IsNullOrEmpty(record.UnitId))
                {
                    Debug.LogWarning($"[UnitDatabase] 인덱스 {i}의 UnitId가 비어있습니다.");
                    continue;
                }

                if (!recordDict.ContainsKey(record.UnitId))
                {
                    recordDict.Add(record.UnitId, record);
                }
                else
                {
                    Debug.LogWarning($"[UnitDatabase] 중복된 Unit ID가 발견되었습니다: {record.UnitId}");
                }
            }
        }

        /// <summary>
        /// Unit ID를 활용하여 능력치 레코드를 빠르게 검색합니다.
        /// </summary>
        public bool TryGetRecord(string unitId, out UnitDataRecord record)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                record = default;
                return false;
            }

            // 런타임에 딕셔너리가 초기화되지 않은 상태라면 동적 초기화 수행 (방어적 코드)
            if (recordDict.Count == 0 && records.Count > 0)
            {
                Initialize();
            }

            return recordDict.TryGetValue(unitId, out record);
        }
    }
}
