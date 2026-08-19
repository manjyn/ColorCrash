using UnityEngine;
using ColorCrash.WorldMap;

namespace ColorCrash
{
    [System.Serializable]
    public struct MapStageConfig
    {
        public int Level;
        public int Width;
        public int Height;
        public int Projection;
        public int ObstacleMin;
        public int ObstacleMax;
        public int BattleTime; // Second
    }

    [System.Serializable]
    public class BattleUnlockSettings
    {
        [Header("Units")]
        public bool UnlockFootman = true;
        public bool UnlockArcher = true;
        public bool UnlockElite = true;
        public bool UnlockWarlord = true;

        [Header("Towers")]
        public bool UnlockBarricade = true;
        public bool UnlockCannon = true;
        public bool UnlockMortar = true;

        /// <summary>
        /// 지정된 전투 객체 타입(유닛/타워)의 생성 잠금 해제 여부를 반환합니다.
        /// </summary>
        public bool IsUnlocked(BattleObjectType type)
        {
            switch (type)
            {
                case BattleObjectType.Footman: return UnlockFootman;
                case BattleObjectType.Archer: return UnlockArcher;
                case BattleObjectType.EliteUnit: return UnlockElite;
                case BattleObjectType.Warlord: return UnlockWarlord;
                case BattleObjectType.Barricade: return UnlockBarricade;
                case BattleObjectType.Cannon: return UnlockCannon;
                case BattleObjectType.Mortar: return UnlockMortar;
                default: return true;
            }
        }
    }

    [System.Serializable]
    public class BattleContext
    {
        public int StageGridLevel = 3; // 전장 Grid 규모 레벨
        public int StageUnitScale = 5; // 전장 전투(유닛 및 타워) 스케일
        public float DecisionVictoryRatio = 0.7f; // 타임아웃 승리 커트라인 아군 점령 비율

        // 유닛 및 타워 생성 잠금 설정
        public BattleUnlockSettings UnlockSettings = new BattleUnlockSettings();

        // 미리 정의된 1~5단계 맵 크기 및 규칙 정보
        public static readonly MapStageConfig[] MapStages = new MapStageConfig[]
        {
            new MapStageConfig { Level = 1, Width = 10, Height = 14, Projection = 100, ObstacleMin = 1, ObstacleMax = 1, BattleTime = 90 },
            new MapStageConfig { Level = 2, Width = 12, Height = 20, Projection = 120, ObstacleMin = 2, ObstacleMax = 2, BattleTime = 120 },
            new MapStageConfig { Level = 3, Width = 14, Height = 24, Projection = 140, ObstacleMin = 2, ObstacleMax = 4, BattleTime = 180 },
            new MapStageConfig { Level = 4, Width = 20, Height = 28, Projection = 160, ObstacleMin = 4, ObstacleMax = 6, BattleTime = 240 },
            new MapStageConfig { Level = 5, Width = 24, Height = 36, Projection = 180, ObstacleMin = 6, ObstacleMax = 8, BattleTime = 300 },
        };

        // 전투 좌표 및 세션 정보
        public int AttackerWorldTileX;
        public int AttackerWorldTileY;
        public int TargetWorldTileX;
        public int TargetWorldTileY;

        public int AttackerUnitCount;
        public int DefenderUnitCount;

        public TileResourceType ResourceType;
        public TileStructureType StructureType;
        public int BanditLevel;

        // 전투 결과 데이터
        public bool IsResolved;
        public bool IsPlayerVictory;
        public int SurvivedAttackerUnitCount;

        /// <summary>
        /// 현재 전투 설정에 따른 맵 구성 정보를 반환합니다.
        /// </summary>
        public MapStageConfig GetMapConfig()
        {
            int targetLevel = StageGridLevel;

            // 도적단 타일 전투일 경우 도적단 레벨에 맞추어 맵 난이도 결정
            if (StructureType == TileStructureType.Bandit && BanditLevel > 0)
            {
                targetLevel = BanditLevel;
            }

            int index = Mathf.Clamp(targetLevel - 1, 0, MapStages.Length - 1);
            return MapStages[index];
        }

        /// <summary>
        /// 해당 객체 타입의 생성 잠금 해제 여부를 반환하는 편의 메서드입니다.
        /// </summary>
        public bool IsUnlocked(BattleObjectType type)
        {
            return UnlockSettings != null && UnlockSettings.IsUnlocked(type);
        }
    }
}
