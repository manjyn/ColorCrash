using UnityEngine;
using UnityEngine.UI;
using ColorCrash;
using TMPro;

public class MenuUIManager : MonoBehaviour
{
    [Header("WorldMap Scene")]
    [SerializeField] private Button startWorldButton;

    [Header("Battle Scene")]
    [SerializeField] private Slider mapStageSlider;
    [SerializeField] private Slider maxUnitsSlider;
    [SerializeField] private Slider victoryRateSlider;

    [SerializeField] private TextMeshProUGUI mapStageText;    
    [SerializeField] private TextMeshProUGUI maxUnitsText;
    [SerializeField] private TextMeshProUGUI victoryRateText;

    [SerializeField] private Toggle[] battleObjectActs;
    [SerializeField] private Button startBattleButton;

    private void Start()
    {
        if (startWorldButton != null)
            startWorldButton.onClick.AddListener(OnStartWorldMapClicked);

        // BattleScene 에서 기존 설정 불러오기
        if (CoreManager.Instance != null)
        {
            if (CoreManager.Instance.CurrentBattleContext == null)
            {
                CoreManager.Instance.CurrentBattleContext = new BattleContext();
            }

            var context = CoreManager.Instance.CurrentBattleContext;
            
            if (mapStageSlider != null)
                mapStageSlider.value = context.StageGridLevel;
            if (maxUnitsSlider != null) 
                maxUnitsSlider.value = context.StageUnitScale;
            if (victoryRateSlider != null)
                victoryRateSlider.value = context.DecisionVictoryRatio;
        }

        if (mapStageSlider != null)
        {
            mapStageSlider.onValueChanged.AddListener(OnMapStageChanged);
            OnMapStageChanged(mapStageSlider.value);
        }

        if (maxUnitsSlider != null)
        {
            maxUnitsSlider.onValueChanged.AddListener(OnMaxUnitsChanged);
            OnMaxUnitsChanged(maxUnitsSlider.value);
        }

        if (victoryRateSlider != null)
        {
            victoryRateSlider.onValueChanged.AddListener(OnVictoryRateChanged);
            OnVictoryRateChanged(victoryRateSlider.value);
        }

        if (startBattleButton != null)
            startBattleButton.onClick.AddListener(OnStartBattleClicked);
    }

    private void OnStartWorldMapClicked()
    {
        if (CoreManager.Instance != null)
        {
            CoreManager.Instance.LoadSceneAsync(Define.Scene.WorldMap);
        }
        else
        {
            Debug.LogError("[MenuUIManager] CoreManager 인스턴스를 찾을 수 없습니다.");
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(Define.Scene.WorldMap);
        }
    }

    private void OnMapStageChanged(float value)
    {
        if (mapStageText != null)
        {
            mapStageText.text = $"Stage Grid Level: {value}";
        }
    }

    private void OnMaxUnitsChanged(float value)
    {
        if (maxUnitsText != null)
        {
            maxUnitsText.text = $"Stage Unit Scale: {value}";
        }
    }

    private void OnVictoryRateChanged(float value)
    {
        if (victoryRateText != null)
        {
            victoryRateText.text = $"Decision Victory Ratio: {value * 100f:F0}%";
        }
    }

    private void OnStartBattleClicked()
    {
        if (CoreManager.Instance != null)
        {
            if (CoreManager.Instance.CurrentBattleContext == null)
            {
                CoreManager.Instance.CurrentBattleContext = new BattleContext();
            }

            var context = CoreManager.Instance.CurrentBattleContext;

            // 현재 UI 값을 바탕으로 설정 저장
            if (mapStageSlider != null) 
                context.StageGridLevel = (int)mapStageSlider.value;
                
            if (maxUnitsSlider != null)
                context.StageUnitScale = (int)maxUnitsSlider.value;

            if (victoryRateSlider != null)
                context.DecisionVictoryRatio = victoryRateSlider.value;

            context.UnlockSettings.UnlockFootman = battleObjectActs[0].isOn;
            context.UnlockSettings.UnlockElite = battleObjectActs[1].isOn;
            context.UnlockSettings.UnlockWarlord = battleObjectActs[2].isOn;
            context.UnlockSettings.UnlockArcher = battleObjectActs[3].isOn;
            context.UnlockSettings.UnlockBarricade = battleObjectActs[4].isOn;
            context.UnlockSettings.UnlockCannon = battleObjectActs[5].isOn;
            context.UnlockSettings.UnlockMortar = battleObjectActs[6].isOn;

            CoreManager.Instance.SaveSettings();
            CoreManager.Instance.LoadSceneAsync(Define.Scene.Battle);
        }
        else
        {
            Debug.LogError("[MenuUIManager] CoreManager 인스턴스를 찾을 수 없습니다.");
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(Define.Scene.Battle);
        }
    }
}
