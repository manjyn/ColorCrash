using UnityEngine;
using UnityEngine.UI;
using ColorCrash.Units;
using TMPro;

public class UnitSpawnButton : MonoBehaviour
{
    [Header("Unit Settings")]
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI textName;
    [SerializeField] private UnitBase unitPrefab;
    [SerializeField] private TeamColor teamColor;
    
    private void Start()
    {
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
        }
    }

    private void OnButtonClick()
    {
        if (unitPrefab == null) return;
        if (BattleUIManager.Instance != null)
        {
            ISpawnHandler handler = new UnitSpawnHandler(unitPrefab);
            BattleUIManager.Instance.SelectObjectToSpawn(unitPrefab.gameObject, handler, teamColor);
        }
    }
}
