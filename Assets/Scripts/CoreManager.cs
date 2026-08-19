using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace ColorCrash
{
    public class CoreManager : MonoBehaviour
    {
        public static CoreManager Instance { get; private set; }

        public BattleContext CurrentBattleContext { get; set; }

        [Header("UI")]
        [SerializeField] private GameObject loadingPanel;

        // Audio Settings
        public float MasterVolume { get; private set; } = 1.0f;
        public float BGMVolume { get; private set; } = 1.0f;
        public float SFXVolume { get; private set; } = 1.0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSettings();
        }

        private void Start()
        {
            // InitScene에서 시작했을 때, 바로 MenuScene으로 넘어가도록 자동화
            if (SceneManager.GetActiveScene().name != Define.Scene.Menu && 
                SceneManager.GetActiveScene().name != Define.Scene.Battle)
            {
                LoadSceneAsync(Define.Scene.Menu);
            }
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat("MasterVolume", MasterVolume);
            PlayerPrefs.SetFloat("BGMVolume", BGMVolume);
            PlayerPrefs.SetFloat("SFXVolume", SFXVolume);
            
            PlayerPrefs.Save();
        }

        public void LoadSettings()
        {
            MasterVolume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
            BGMVolume = PlayerPrefs.GetFloat("BGMVolume", 1.0f);
            SFXVolume = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
        }

        public void LoadSceneAsync(string sceneName)
        {
            // 추후 로딩 화면 UI 호출 로직 추가 가능
            SceneManager.LoadSceneAsync(sceneName);
        }

        public void LoadBattleSceneAdditive(BattleContext context)
        {
            CurrentBattleContext = context;
            StartCoroutine(LoadBattleSceneRoutine());
        }

        private IEnumerator LoadBattleSceneRoutine()
        {
            if (loadingPanel != null) loadingPanel.SetActive(true);

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(Define.Scene.Battle, LoadSceneMode.Additive);
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            Scene battleScene = SceneManager.GetSceneByName(Define.Scene.Battle);
            if (battleScene.IsValid())
            {
                SceneManager.SetActiveScene(battleScene);
            }

            if (loadingPanel != null) loadingPanel.SetActive(false);
        }

        public void UnloadBattleScene()
        {
            StartCoroutine(UnloadBattleSceneRoutine());
        }

        private IEnumerator UnloadBattleSceneRoutine()
        {
            if (loadingPanel != null) loadingPanel.SetActive(true);

            AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(Define.Scene.Battle);
            while (asyncUnload != null && !asyncUnload.isDone)
            {
                yield return null;
            }

            yield return Resources.UnloadUnusedAssets();

            Scene worldMapScene = SceneManager.GetSceneByName(Define.Scene.WorldMap);
            if (worldMapScene.IsValid())
            {
                SceneManager.SetActiveScene(worldMapScene);
            }

            if (loadingPanel != null) loadingPanel.SetActive(false);

            if (ColorCrash.WorldMap.WorldMapManager.Instance != null)
            {
                ColorCrash.WorldMap.WorldMapManager.Instance.WakeUpAndApplyResult();
            }
        }
    }
}
