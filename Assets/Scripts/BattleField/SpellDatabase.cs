using System.Collections.Generic;
using UnityEngine;

namespace ColorCrash.Spell
{
    /// <summary>
    /// 단일 마법 스펠의 수치 데이터를 정의하는 직렬화 구조체.
    /// </summary>
    [System.Serializable]
    public struct SpellDataRecord
    {
        [SerializeField] private string spellId;
        [SerializeField] private string spellName;
        [SerializeField] private float damage;
        [SerializeField] private int radius; // 1 = 3x3 범위, 2 = 5x5 범위
        [SerializeField] private int inkCost;

        public SpellDataRecord(string spellId, string spellName, float damage, int radius, int inkCost)
        {
            this.spellId = spellId;
            this.spellName = spellName;
            this.damage = damage;
            this.radius = radius;
            this.inkCost = inkCost;
        }

        public string SpellId => spellId;
        public string SpellName => spellName;
        public float Damage => damage;
        public int Radius => radius;
        public int InkCost => inkCost;
    }

    /// <summary>
    /// 모든 마법 스펠 능력치를 에셋에서 통합 관리하는 ScriptableObject.
    /// O(1) 딕셔너리 빠른 검색을 지원합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "SpellDatabase", menuName = "ColorCrash/Spell Database")]
    public class SpellDatabase : ScriptableObject
    {
        public static SpellDatabase Instance { get; private set; }

        [SerializeField] private List<SpellDataRecord> records = new List<SpellDataRecord>();

        private Dictionary<string, SpellDataRecord> recordDict = new Dictionary<string, SpellDataRecord>();

        private void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        public void Initialize()
        {
            Instance = this;
            recordDict.Clear();

            // 기본 데이터가 없을 경우 InkBomb 기본 레코드 추가
            if (records.Count == 0)
            {
                records.Add(new SpellDataRecord("InkBomb", "Ink Bomb", 50f, 1, 3));
            }

            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (string.IsNullOrEmpty(record.SpellId))
                {
                    Debug.LogWarning($"[SpellDatabase] 인덱스 {i}의 SpellId가 비어있습니다.");
                    continue;
                }

                if (!recordDict.ContainsKey(record.SpellId))
                {
                    recordDict.Add(record.SpellId, record);
                }
                else
                {
                    Debug.LogWarning($"[SpellDatabase] 중복된 Spell ID가 발견되었습니다: {record.SpellId}");
                }
            }
        }

        public bool TryGetRecord(string spellId, out SpellDataRecord record)
        {
            if (string.IsNullOrEmpty(spellId))
            {
                record = default;
                return false;
            }

            if (recordDict.Count == 0)
            {
                Initialize();
            }

            return recordDict.TryGetValue(spellId, out record);
        }

        /// <summary>
        /// 스펠 ID로 데미지를 반환하는 정적 헬퍼 (DB 참조 실패 시 기본값 50f)
        /// </summary>
        public static float GetSpellDamage(string spellId, float defaultDamage = 50f)
        {
            if (Instance != null && Instance.TryGetRecord(spellId, out SpellDataRecord record))
            {
                return record.Damage;
            }
            return defaultDamage;
        }

        /// <summary>
        /// 스펠 ID로 반경을 반환하는 정적 헬퍼 (DB 참조 실패 시 기본값 1)
        /// </summary>
        public static int GetSpellRadius(string spellId, int defaultRadius = 1)
        {
            if (Instance != null && Instance.TryGetRecord(spellId, out SpellDataRecord record))
            {
                return record.Radius;
            }
            return defaultRadius;
        }
    }
}
