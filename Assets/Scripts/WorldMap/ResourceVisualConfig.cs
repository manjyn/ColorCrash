using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace ColorCrash.WorldMap
{
    /// <summary>
    /// 월드맵 타일 자원의 비주얼(아이콘 등)을 한곳에서 설정하고 매핑해주는 데이터 에셋
    /// </summary>
    //[CreateAssetMenu(fileName = "ResourceVisualConfig", menuName = "ColorCrash/Resource Visual Config")]
    public class ResourceVisualConfig : ScriptableObject
    {
        [System.Serializable]
        public struct ResourceIconData
        {
            public TileResourceType resourceType;
            public Sprite iconSprite;
        }

        [System.Serializable]
        public struct BanditIconData
        {
            public int banditLevel;
            public Sprite iconSprite;
        }

        [Header("MainBase 아이콘")]
        [SerializeField] private Sprite mainBaseIconSprite = null;

        [Header("자원 아이콘 리스트")]
        [SerializeField] private List<ResourceIconData> iconDataList = new List<ResourceIconData>();

        [Header("도적단 아이콘 리스트")]
        [SerializeField] private List<BanditIconData> iconBanditDataList = new List<BanditIconData>();
        

        private Dictionary<TileResourceType, Sprite> iconCache;

        /// <summary>
        /// 특정 자원 타입에 해당하는 스프라이트(아이콘)를 반환
        /// </summary>
        public Sprite GetIcon(TileResourceType type)
        {
            // 첫 호출 시 리스트의 데이터를 딕셔너리로 변환하여 캐싱
            if (iconCache == null)
            {
                iconCache = new Dictionary<TileResourceType, Sprite>();
                foreach (var data in iconDataList)
                {
                    if (!iconCache.ContainsKey(data.resourceType))
                    {
                        iconCache.Add(data.resourceType, data.iconSprite);
                    }
                }
            }

            // 딕셔너리에서 스프라이트를 찾아 반환 (없으면 null 반환)
            if (iconCache.TryGetValue(type, out var sprite))
            {
                return sprite;
            }

            return null;
        }

        /// <summary>
        /// MainBase 아이콘 반환
        /// </summary>
        public Sprite GetMainBase()
        {
            return mainBaseIconSprite;
        }

        public Sprite GetBanditIcon(int level)
        {
            foreach (var data in iconBanditDataList)
            {
                if (data.banditLevel == level)
                    return data.iconSprite;
            }
            return null;
        }

        /// <summary>
        /// 에디터에서 데이터 변경 시 딕셔너리 캐시를 초기화해주는 안전장치
        /// </summary>
        private void OnValidate()
        {
            iconCache = null;
        }
    }
}