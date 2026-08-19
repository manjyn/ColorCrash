using System.Collections.Generic;
using UnityEngine;

namespace ColorCrash.Projectiles
{
    /// <summary>
    /// 발사체의 수치 데이터를 정의하는 구조체.
    /// </summary>
    [System.Serializable]
    public struct ProjectileDataRecord
    {
        [SerializeField] private string projectileId;
        [SerializeField] private string projectileName;
        [SerializeField] private float speed;
        [SerializeField] private float splashRadius;
        [SerializeField] private float arcHeight;

        public string ProjectileId => projectileId;
        public string ProjectileName => projectileName;
        public float Speed => speed;
        public float SplashRadius => splashRadius;
        public float ArcHeight => arcHeight;
    }

    /// <summary>
    /// 모든 발사체의 밸런스 능력치를 하나의 에셋에서 통합 관리하는 ScriptableObject.
    /// </summary>
    //[CreateAssetMenu(fileName = "ProjectileDatabase", menuName = "ColorCrash/Projectile Database")]
    public class ProjectileDatabase : ScriptableObject
    {
        [SerializeField] private List<ProjectileDataRecord> records = new List<ProjectileDataRecord>();

        private Dictionary<string, ProjectileDataRecord> recordDict = new Dictionary<string, ProjectileDataRecord>();

        public void Initialize()
        {
            recordDict.Clear();
            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (string.IsNullOrEmpty(record.ProjectileId))
                {
                    Debug.LogWarning($"[ProjectileDatabase] 인덱스 {i}의 ProjectileId가 비어있습니다.");
                    continue;
                }

                if (!recordDict.ContainsKey(record.ProjectileId))
                {
                    recordDict.Add(record.ProjectileId, record);
                }
                else
                {
                    Debug.LogWarning($"[ProjectileDatabase] 중복된 Projectile ID가 발견되었습니다: {record.ProjectileId}");
                }
            }
        }

        public bool TryGetRecord(string projectileId, out ProjectileDataRecord record)
        {
            if (string.IsNullOrEmpty(projectileId))
            {
                record = default;
                return false;
            }

            if (recordDict.Count == 0 && records.Count > 0)
            {
                Initialize();
            }

            return recordDict.TryGetValue(projectileId, out record);
        }
    }
}
