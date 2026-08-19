using ColorCrash.WorldMap;
using NUnit.Framework.Internal;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldUIManager : MonoBehaviour
{
    public static WorldUIManager Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI resourceInfo;
    [SerializeField] private Transform endTurnPanel;
    [SerializeField] private Transform redeployTabPanel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        WorldMapManager.Instance.OnGoldChanged += OnGoldChanged;
        WorldMapManager.Instance.OnAPChanged += OnAPChanged;
    }

    private void OnGoldChanged(int gold)
    {
        UpdateResourceInfo();
    }

    private void OnAPChanged(int ap)
    {
        UpdateResourceInfo();
    }

    public void ShowRedeployTabPanel(bool show)
    {
        if (redeployTabPanel != null)
            redeployTabPanel.gameObject.SetActive(show);
    }

    public void UpdateResourceInfo()
    {
        var worldManager = WorldMapManager.Instance;
        if (worldManager == null)
            return;
                
        string strUnit = $"{worldManager.Unit}(+{worldManager.unitsPerTurn})/{worldManager.UnitLimit + worldManager.UnitAddLimit}";
        string strGold = $"{worldManager.Gold}(+{worldManager.GoldPerTurn})";
        string strAP = $"{worldManager.AP}(+{worldManager.APPerTurn})/{worldManager.MaxAP}";

        resourceInfo.text = $"Unit {strUnit}  |  Gold {strGold}  |  AP {strAP}";
    }

    public void OnEndTurn()
    {
        if (WorldMapManager.Instance != null)
        {
            WorldMapManager.Instance.EndPlayerTurn();
        }
    }
}
