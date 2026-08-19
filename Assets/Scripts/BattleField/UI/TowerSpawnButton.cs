using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class TowerSpawnButton : MonoBehaviour
{
    [Header("Tower Settings")]
    public TowerBase towerPrefab;
    public TeamColor teamColor;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void Start()
    {
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
        }
    }

    private void OnButtonClick()
    {
        if (towerPrefab == null) return;
        if (BattleUIManager.Instance != null)
        {
            ISpawnHandler handler = new TowerSpawnHandler(towerPrefab);
            BattleUIManager.Instance.SelectObjectToSpawn(towerPrefab.gameObject, handler, teamColor);
        }
    }
}
