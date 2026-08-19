using System.Collections.Generic;
using UnityEngine;

namespace ColorCrash.Tower
{
    /// <summary>
    /// 단일 타워의 수치 데이터를 정의하는 직렬화 가능 구조체.
    /// </summary>
    [System.Serializable]
    public struct TowerDataRecord
    {
        [SerializeField] private string towerId;
        [SerializeField] private string towerName;
        [SerializeField] private int sizeX;
        [SerializeField] private int sizeZ;
        [SerializeField] private float maxHp;
        [SerializeField] private float defense;
        [SerializeField] private float attackDamage;
        [SerializeField] private float attackRange;
        [SerializeField] private float minAttackRange;
        [SerializeField] private float attackCooldown;
        [SerializeField] private int inkCost;

        public string TowerId => towerId;
        public string TowerName => towerName;
        public int SizeX => sizeX;
        public int SizeZ => sizeZ;
        public float MaxHp => maxHp;
        public float Defense => defense;
        public float AttackDamage => attackDamage;
        public float AttackRange => attackRange;
        public float MinAttackRange => minAttackRange;
        public float AttackCooldown => attackCooldown;
        public int InkCost => inkCost;
    }

    /// <summary>
    /// 모든 타워의 능력치를 하나의 에셋에서 통합 관리하는 ScriptableObject.
    /// 빠른 검색을 위해 Dictionary로 초기화됩니다.
    /// </summary>
    //[CreateAssetMenu(fileName = "TowerDatabase", menuName = "ColorCrash/Tower Database")]
    public class TowerDatabase : ScriptableObject
    {
        [SerializeField] private List<TowerDataRecord> records = new List<TowerDataRecord>();

        private Dictionary<string, TowerDataRecord> recordDict = new Dictionary<string, TowerDataRecord>();

        /// <summary>
        /// 초기화 시 리스트 데이터를 딕셔너리로 변환하여 빠른 접근(O(1))을 제공합니다.
        /// </summary>
        public void Initialize()
        {
            recordDict.Clear();
            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (string.IsNullOrEmpty(record.TowerId))
                {
                    Debug.LogWarning($"[TowerDatabase] 인덱스 {i}의 TowerId가 비어있습니다.");
                    continue;
                }

                if (!recordDict.ContainsKey(record.TowerId))
                {
                    recordDict.Add(record.TowerId, record);
                }
                else
                {
                    Debug.LogWarning($"[TowerDatabase] 중복된 Tower ID가 발견되었습니다: {record.TowerId}");
                }
            }
        }

        /// <summary>
        /// Tower ID를 사용하여 능력치 레코드를 빠르게 검색합니다.
        /// </summary>
        public bool TryGetRecord(string towerId, out TowerDataRecord record)
        {
            if (string.IsNullOrEmpty(towerId))
            {
                record = default;
                return false;
            }

            // 혹시라도 딕셔너리가 초기화되지 않은 상태라면 동적 초기화 수행
            if (recordDict.Count == 0 && records.Count > 0)
            {
                Initialize();
            }

            return recordDict.TryGetValue(towerId, out record);
        }
    }
}
