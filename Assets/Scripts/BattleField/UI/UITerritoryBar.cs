using UnityEngine;
using TMPro;
using ColorCrash;

public class UITerritoryBar : MonoBehaviour
{
    [Header("Bar RectTransforms")]
    [SerializeField] private RectTransform blueBarRect;
    [SerializeField] private RectTransform neutralBarRect;
    [SerializeField] private RectTransform redBarRect;
    [SerializeField] private RectTransform winCutLineRect;

    [Header("Text Indicators")]
    [SerializeField] private TextMeshProUGUI bluePercentText;
    //[SerializeField] private TextMeshProUGUI redPercentText;

    [Header("Animation Settings")]
    [SerializeField] private float lerpSpeed = 6.0f;
    [SerializeField] private float snapEpsilon = 0.0005f; // 보간 정지 임계값

    // 비율 캐시
    private float targetBlueRatio = 0.15f;
    private float targetNeutralRatio = 0.70f;
    private float targetRedRatio = 0.15f;

    private float currentBlueRatio = 0.15f;
    private float currentNeutralRatio = 0.70f;
    private float currentRedRatio = 0.15f;

    // 텍스트 갱신 중복 방지용 이전 값 캐시
    private int cachedBluePercent = -1;
    //private int cachedRedPercent = -1;

    // 애니메이션 활성화 플래그
    private bool isAnimating = false;

    // 0% ~ 100% 문자열 캐시 GC Alloc 방지
    private static readonly string[] PercentStrings = new string[101];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeStringCache()
    {
        for (int i = 0; i <= 100; i++)
        {
            PercentStrings[i] = $"{i}%";
        }
    }

    private void Awake()
    {
        // 초기 비율 즉시 적용 (애니메이션 없이)
        ApplyRatiosImmediately(targetBlueRatio, targetNeutralRatio, targetRedRatio);
    }

    private void OnEnable()
    {
        // GridManager의 타일 변경 이벤트 구독
        GridManager.OnTerritoryChanged += UpdateTerritoryCount;

        var context = CoreManager.Instance != null ? CoreManager.Instance.CurrentBattleContext : null;
        if (context != null && winCutLineRect != null)
        {
            winCutLineRect.anchorMin = new Vector2(context.DecisionVictoryRatio, 0f);
            winCutLineRect.anchorMax = new Vector2(context.DecisionVictoryRatio + 0.02f, 1f);
            winCutLineRect.offsetMin = Vector2.zero;
            winCutLineRect.offsetMax = Vector2.zero;
        }
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제
        GridManager.OnTerritoryChanged -= UpdateTerritoryCount;
    }

    /// <summary>
    /// 외부(GridManager 등)에서 점령 타일 수 변경 시 호출
    /// </summary>
    public void UpdateTerritoryCount(int blueCount, int neutralCount, int redCount)
    {
        int totalTiles = blueCount + neutralCount + redCount;
        if (totalTiles <= 0) return;

        targetBlueRatio = (float)blueCount / totalTiles;
        targetNeutralRatio = (float)neutralCount / totalTiles;
        targetRedRatio = (float)redCount / totalTiles;

        // 변동이 생겼을 때만 Update 애니메이션 활성화
        isAnimating = true;
    }

    private void Update()
    {
        if (!isAnimating) return;

        // 1. 비율 부드럽게 보간
        currentBlueRatio = Mathf.Lerp(currentBlueRatio, targetBlueRatio, Time.deltaTime * lerpSpeed);
        currentNeutralRatio = Mathf.Lerp(currentNeutralRatio, targetNeutralRatio, Time.deltaTime * lerpSpeed);
        currentRedRatio = Mathf.Lerp(currentRedRatio, targetRedRatio, Time.deltaTime * lerpSpeed);

        // 2. 목표치 도착 검사 및 스냅(Snap)
        if (Mathf.Abs(currentBlueRatio - targetBlueRatio) < snapEpsilon &&
            Mathf.Abs(currentRedRatio - targetRedRatio) < snapEpsilon)
        {
            currentBlueRatio = targetBlueRatio;
            currentNeutralRatio = targetNeutralRatio;
            currentRedRatio = targetRedRatio;
            isAnimating = false; // 보간 완료 시 Update 연산 중지
        }

        // 3. Anchor 조정을 통한 영역 갱신 (Layout Rebuild 없음)
        ApplyBarAnchors(currentBlueRatio, currentNeutralRatio);

        // 4. 텍스트 갱신 (값 변경 시에만 캐싱된 문자열 적용)
        UpdateTextUI(targetBlueRatio, targetRedRatio);
    }

    private void ApplyBarAnchors(float blueRatio, float neutralRatio)
    {
        // 1. Blue Bar: [0.0 ~ blueRatio]
        if (blueBarRect != null)
        {
            blueBarRect.anchorMin = new Vector2(0f, 0f);
            blueBarRect.anchorMax = new Vector2(blueRatio, 1f);

            // Offset(Left, Bottom, Right, Top)을 0으로 리셋해야 Anchor 구역에 꽉 참
            blueBarRect.offsetMin = Vector2.zero;
            blueBarRect.offsetMax = Vector2.zero;
        }
        // 2. Neutral Bar: [blueRatio ~ (blueRatio + neutralRatio)]
        if (neutralBarRect != null)
        {
            float neutralEnd = blueRatio + neutralRatio;
            neutralBarRect.anchorMin = new Vector2(blueRatio, 0f);
            neutralBarRect.anchorMax = new Vector2(neutralEnd, 1f);

            neutralBarRect.offsetMin = Vector2.zero;
            neutralBarRect.offsetMax = Vector2.zero;
        }
        // 3. Red Bar: [(blueRatio + neutralRatio) ~ 1.0]
        if (redBarRect != null)
        {
            float redStart = blueRatio + neutralRatio;
            redBarRect.anchorMin = new Vector2(redStart, 0f);
            redBarRect.anchorMax = new Vector2(1f, 1f);

            redBarRect.offsetMin = Vector2.zero;
            redBarRect.offsetMax = Vector2.zero;
        }
    }

    private void UpdateTextUI(float blueRatio, float redRatio)
    {
        int newBluePercent = Mathf.Clamp(Mathf.RoundToInt(blueRatio * 100f), 0, 100);
        int newRedPercent = Mathf.Clamp(Mathf.RoundToInt(redRatio * 100f), 0, 100);

        if (bluePercentText != null && newBluePercent != cachedBluePercent)
        {
            cachedBluePercent = newBluePercent;
            bluePercentText.text = PercentStrings[newBluePercent];
        }

        /*if (redPercentText != null && newRedPercent != cachedRedPercent)
        {
            cachedRedPercent = newRedPercent;
            redPercentText.text = PercentStrings[newRedPercent];   // 캐시된 문자열 사용
        }*/
    }

    public void ApplyRatiosImmediately(float blue, float neutral, float red)
    {
        targetBlueRatio = currentBlueRatio = blue;
        targetNeutralRatio = currentNeutralRatio = neutral;
        targetRedRatio = currentRedRatio = red;

        ApplyBarAnchors(currentBlueRatio, currentNeutralRatio);
        UpdateTextUI(targetBlueRatio, targetRedRatio);
        isAnimating = false;
    }
}