#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ColorCrash.AI.Editor
{
    /// <summary>
    /// 유니티 에디터 메뉴를 통해 대표 배틀 AI 데이터 프로필 에셋 4종을 자동 생성하는 에디터 헬퍼 클래스입니다.
    /// </summary>
    public static class BattleAIDataAssetCreator
    {
        private const string TargetFolder = "Assets/Database/AIDatabase";

        [MenuItem("Tools/ColorCrash/Create All Battle AI Data Profiles")]
        public static void CreateAllAIDataProfiles()
        {
            if (!Directory.Exists(TargetFolder))
            {
                Directory.CreateDirectory(TargetFolder);
                AssetDatabase.Refresh();
            }

            CreateEasyAgroAsset();
            CreateNormalBunkerAsset();
            CreateHardAdaptiveAsset();
            CreateBossStageAsset();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ColorCrash] 배틀 AI 데이터 에셋 4종이 성공적으로 생성되었습니다: {TargetFolder}");
        }

        private static void CreateEasyAgroAsset()
        {
            string path = $"{TargetFolder}/BattleAIData_Easy_Agro.asset";
            BattleAIDataSO asset = AssetDatabase.LoadAssetAtPath<BattleAIDataSO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BattleAIDataSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.ProfileName = "Easy_Agro";
            asset.Archetype = AIArchetype.AgroPainter;
            asset.EvaluationInterval = 1.5f;
            asset.MinActionInterval = 0.4f;
            asset.InkMultiplier = 1.0f;
            asset.FeverTempoMultiplier = 1.2f;
            asset.CrisisTerritoryThreshold = 0.30f;
            asset.CounterChance = 0.3f;
            asset.RerollChance = 0.0f;
            asset.MinUnitBuildingRatio = 3.5f;
            asset.MaxUnitBuildingRatio = 5.5f;
            asset.PreferredUnitCards = new System.Collections.Generic.List<BattleObjectType> { BattleObjectType.Footman, BattleObjectType.Archer };
            asset.PreferredSpellCards = new System.Collections.Generic.List<BattleObjectType> { BattleObjectType.InkBomb };
            asset.TileTargetOffset = 2;

            EditorUtility.SetDirty(asset);
        }

        private static void CreateNormalBunkerAsset()
        {
            string path = $"{TargetFolder}/BattleAIData_Normal_Bunker.asset";
            BattleAIDataSO asset = AssetDatabase.LoadAssetAtPath<BattleAIDataSO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BattleAIDataSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.ProfileName = "Normal_Bunker";
            asset.Archetype = AIArchetype.BunkerFortress;
            asset.EvaluationInterval = 0.8f;
            asset.MinActionInterval = 0.2f;
            asset.InkMultiplier = 1.0f;
            asset.FeverTempoMultiplier = 1.5f;
            asset.CrisisTerritoryThreshold = 0.35f;
            asset.CounterChance = 0.6f;
            asset.RerollChance = 0.3f;
            asset.MinUnitBuildingRatio = 2.0f;
            asset.MaxUnitBuildingRatio = 3.5f;
            asset.PreferredUnitCards = new System.Collections.Generic.List<BattleObjectType> { BattleObjectType.Footman, BattleObjectType.EliteUnit, BattleObjectType.Archer };
            asset.PreferredSpellCards = new System.Collections.Generic.List<BattleObjectType> { BattleObjectType.InkBomb };
            asset.TileTargetOffset = 1;

            EditorUtility.SetDirty(asset);
        }

        private static void CreateHardAdaptiveAsset()
        {
            string path = $"{TargetFolder}/BattleAIData_Hard_Adaptive.asset";
            BattleAIDataSO asset = AssetDatabase.LoadAssetAtPath<BattleAIDataSO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BattleAIDataSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.ProfileName = "Hard_Adaptive";
            asset.Archetype = AIArchetype.AdaptiveCounter;
            asset.EvaluationInterval = 0.3f;
            asset.MinActionInterval = 0.1f;
            asset.InkMultiplier = 1.15f;
            asset.FeverTempoMultiplier = 1.5f;
            asset.CrisisTerritoryThreshold = 0.40f;
            asset.CounterChance = 0.95f;
            asset.RerollChance = 0.8f;
            asset.MinUnitBuildingRatio = 2.5f;
            asset.MaxUnitBuildingRatio = 4.5f;
            asset.PreferredUnitCards = new System.Collections.Generic.List<BattleObjectType> { BattleObjectType.Footman, BattleObjectType.EliteUnit, BattleObjectType.Warlord, BattleObjectType.Archer };
            asset.PreferredSpellCards = new System.Collections.Generic.List<BattleObjectType> { BattleObjectType.InkBomb };
            asset.TileTargetOffset = 0;

            EditorUtility.SetDirty(asset);
        }

        private static void CreateBossStageAsset()
        {
            string path = $"{TargetFolder}/BattleAIData_Boss_Stage1.asset";
            BattleAIDataSO asset = AssetDatabase.LoadAssetAtPath<BattleAIDataSO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BattleAIDataSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.ProfileName = "Boss_Stage1";
            asset.Archetype = AIArchetype.AdaptiveCounter;
            asset.EvaluationInterval = 0.3f;
            asset.MinActionInterval = 0.1f;
            asset.InkMultiplier = 1.25f;
            asset.FeverTempoMultiplier = 1.8f;
            asset.CrisisTerritoryThreshold = 0.45f;
            asset.CounterChance = 0.95f;
            asset.RerollChance = 0.9f;
            asset.MinUnitBuildingRatio = 2.0f;
            asset.MaxUnitBuildingRatio = 4.0f;
            asset.PreferredUnitCards = new System.Collections.Generic.List<BattleObjectType> { BattleObjectType.EliteUnit, BattleObjectType.Warlord, BattleObjectType.Archer };
            asset.PreferredSpellCards = new System.Collections.Generic.List<BattleObjectType> { BattleObjectType.InkBomb };
            asset.TileTargetOffset = 0;

            EditorUtility.SetDirty(asset);
        }
    }
}
#endif
