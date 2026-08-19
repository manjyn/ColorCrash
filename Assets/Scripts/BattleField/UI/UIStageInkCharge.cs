using UnityEngine;
using UnityEngine.UI;
using ColorCrash;

public class UIStageInkCharge : MonoBehaviour
{
    [SerializeField] private TeamColor targetTeam = TeamColor.Blue;
    [SerializeField] private Image[] ImageGauge;
    [SerializeField] private Color ColorOn = Color.cyan;
    [SerializeField] private Color ColorOff = new Color(0.2f, 0.2f, 0.2f, 0.5f);

    private int maxInkGauge = 10;
    private int currentInkGauge = -1;

    private bool isSubscribed = false;

    private void OnEnable()
    {
        SubscribeEvent();
        if (BattleInkManager.Instance != null)
        {
            UpdateInkUI(BattleInkManager.Instance.GetCurrentInk(targetTeam), BattleInkManager.MAX_INK);
        }
    }

    private void OnDisable()
    {
        UnsubscribeEvent();
    }

    private void Start()
    {
        SubscribeEvent();
        if (BattleInkManager.Instance != null)
        {
            Initialize(Mathf.RoundToInt(BattleInkManager.MAX_INK));
            UpdateInkUI(BattleInkManager.Instance.GetCurrentInk(targetTeam), BattleInkManager.MAX_INK);
        }
    }

    private void SubscribeEvent()
    {
        if (!isSubscribed && BattleInkManager.Instance != null)
        {
            BattleInkManager.Instance.OnInkChanged += OnInkChanged;
            isSubscribed = true;
        }
    }

    private void UnsubscribeEvent()
    {
        if (isSubscribed && BattleInkManager.Instance != null)
        {
            BattleInkManager.Instance.OnInkChanged -= OnInkChanged;
            isSubscribed = false;
        }
    }

    private void OnInkChanged(TeamColor team, float currentInk, float maxInk)
    {
        if (team == targetTeam)
        {
            UpdateInkUI(currentInk, maxInk);
        }
    }

    public void Initialize(int maxGauge)
    {
        maxInkGauge = maxGauge;

        if (ImageGauge != null)
        {
            for (int i = 0; i < ImageGauge.Length; i++)
            {
                if (ImageGauge[i] != null)
                {
                    ImageGauge[i].gameObject.SetActive(i < maxInkGauge);
                }
            }
        }
    }

    public void UpdateInkUI(float currentInk, float maxInk)
    {
        if (ImageGauge == null) return;

        int fullInk = Mathf.FloorToInt(currentInk);

        if (currentInkGauge != fullInk)
        {
            currentInk = fullInk;
            for (int i = 0; i < ImageGauge.Length; i++)
            {
                if (ImageGauge[i] != null)
                {
                    ImageGauge[i].color = (i < fullInk) ? ColorOn : ColorOff;
                }
            }
        }
    }
}
