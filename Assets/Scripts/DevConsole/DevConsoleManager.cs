using ColorCrash;
using ColorCrash.WorldMap;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;

public class DevConsoleManager : MonoBehaviour
{
    public enum ConsoleType
    {
        None = 0,
        World = 1,
        Battle = 2,
    }

    [Header("World Map")]
    [SerializeField] private Transform developmentPanel;
    [SerializeField] private TMP_InputField worldWidth_Input;
    [SerializeField] private TMP_InputField worldHeight_Input;

    [Header("Battle Field")]
    [SerializeField] private Transform battlePanel;


    public void OnShowHide(int consoleType)
    {
        if (ConsoleType.World == (ConsoleType)consoleType)
        {
            if (developmentPanel != null)
            {
                bool active = !developmentPanel.gameObject.activeSelf;
                developmentPanel.gameObject.SetActive(active);
            }
        }
        else if (ConsoleType.Battle == (ConsoleType)consoleType)
        {
            if (battlePanel != null)
            {
                bool active = !battlePanel.gameObject.activeSelf;
                battlePanel.gameObject.SetActive(active);
            }
        }
    }

    //===============================================================
    #region // WorldMap Scene 함수 정의
    public void OnWorld_ResetMap()
    {
        if (worldWidth_Input == null || string.IsNullOrEmpty(worldWidth_Input.text))
            return;

        if (worldHeight_Input == null || string.IsNullOrEmpty(worldHeight_Input.text))
            return;

        if (int.TryParse(worldWidth_Input.text, out int width) && int.TryParse(worldHeight_Input.text, out int height))
        {
            // 최소 크기 보장 및 최대 크기 제한 (Clamp)
            width = Mathf.Clamp(width, 1, WorldMapManager.MAX_MAP_WIDTH);
            height = Mathf.Clamp(height, 1, WorldMapManager.MAX_MAP_HEIGHT);

            // 실제 입력 필드 텍스트도 보정된 값으로 갱신해 주면 UX가 좋습니다.
            worldWidth_Input.text = width.ToString();
            worldHeight_Input.text = height.ToString();

            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.ResetMap(width, height);
                Debug.Log($"[Cheat] 맵이 {width}x{height} 크기로 재생성되었습니다.");
            }
        }
    }

    public void OnWorld_RemoveFog()
    {
        if (WorldMapManager.Instance != null)
        {
            WorldMapManager.Instance.RevealAllFog();
            Debug.Log("[Cheat] 맵의 모든 안개가 제거되었습니다!");
        }
    }
    #endregion

    //===============================================================
    #region // Battle Scene 함수 정의
    public void OnBattle_Victory()
    {
        BattleManager.Instance.ResolveBattle(true);
    }

    public void OnBattle_Defeat()
    {
        BattleManager.Instance.ResolveBattle(false);
    }

    public void OnBattle_ExitMenu()
    {
        if (CoreManager.Instance != null)
        {
            CoreManager.Instance.LoadSceneAsync(Define.Scene.Menu);
        }
    }

    public void OnBattle_PauseAI()
    {
        if (BattleManager.Instance.AIController != null)
            BattleManager.Instance.AIController.PauseAI();
    }
        
    public void OnBattle_ResumeAI()
    {
        if (BattleManager.Instance.AIController != null)
            BattleManager.Instance.AIController.ResumeAI();
    }
    #endregion
}
