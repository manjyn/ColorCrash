using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 전투 화면 하단 카드 슬롯(4슬롯) 및 동적 덱 순환, Ink Reroll, 고정 스펠 카드를 총괄 관리하는 UI 매니저 클래스
/// </summary>
public class UIBattleObjCardList : MonoBehaviour
{
    [Header("Hand Slots (4 Slots)")]
    [SerializeField] private UIBattleSpawnCard[] uiBattleSpawnCards;

    [Header("Fixed Active Cards (Spell / Reroll)")]
    [SerializeField] private UIBattleSpawnCard inkBombCard;
    [SerializeField] private Button rerollButton;
    [SerializeField] private TextMeshProUGUI textRerollCooldown;

    [Header("Settings")]
    [SerializeField] private TeamColor teamColor = TeamColor.Blue;
    [SerializeField] private float rerollCooldownDuration = 2.0f;

    private List<BattleObjectType> unlockedCards = new List<BattleObjectType>();
    private List<BattleObjectType> drawDeck = new List<BattleObjectType>();
    private List<BattleObjectType> discardDeck = new List<BattleObjectType>();

    private float rerollCooldownTimer = 0f;
    private bool isDynamicDeckSystem = false;

    private void Start()
    {
        InitializeCards();

        if (rerollButton != null)
            rerollButton.onClick.AddListener(OnRerollButtonClicked);

        UpdateRerollButtonState();
    }

    private void OnEnable()
    {
        if (uiBattleSpawnCards != null)
        {
            foreach (var card in uiBattleSpawnCards)
            {
                if (card != null)
                {
                    card.OnCardClicked += OnHandCardClicked;
                }
            }
        }

        if (inkBombCard != null)
            inkBombCard.OnCardClicked += OnHandCardClicked;

        if (BattleInkManager.Instance != null)
        {
            BattleInkManager.Instance.OnInkChanged += OnInkChanged;
        }
    }

    private void OnDisable()
    {
        if (uiBattleSpawnCards != null)
        {
            foreach (var card in uiBattleSpawnCards)
            {
                if (card != null)
                {
                    card.OnCardClicked -= OnHandCardClicked;
                }
            }
        }

        if (BattleInkManager.Instance != null)
        {
            BattleInkManager.Instance.OnInkChanged -= OnInkChanged;
        }
    }

    private void Update()
    {
        UpdateRerollCooldown();
    }

    /// <summary>
    /// BattleContext의 UnlockSettings를 기반으로 해금된 7종 유닛/건물 카드를 수집하고,
    /// 손패 슬롯(4개) 및 대기 덱을 초기화합니다.
    /// </summary>
    public void InitializeCards()
    {
        unlockedCards.Clear();

        // 7종 유닛/건물 대상
        BattleObjectType[] candidates = new BattleObjectType[]
        {
            BattleObjectType.Footman,
            BattleObjectType.Archer,
            BattleObjectType.EliteUnit,
            BattleObjectType.Warlord,
            BattleObjectType.Barricade,
            BattleObjectType.Cannon,
            BattleObjectType.Mortar
        };

        var context = ColorCrash.CoreManager.Instance != null ? ColorCrash.CoreManager.Instance.CurrentBattleContext : null;

        foreach (var type in candidates)
        {
            if (context == null || context.IsUnlocked(type))
            {
                unlockedCards.Add(type);
            }
        }

        // Ink Bomb 고정 마법 스펠 카드 세팅
        if (inkBombCard != null)
        {
            inkBombCard.SetCardType(BattleObjectType.InkBomb);
        }

        if (uiBattleSpawnCards == null || uiBattleSpawnCards.Length == 0) return;

        // 해금 카드가 4개 이하인 경우: 고정 노출 (동적 덱 순환 비활성화)
        if (unlockedCards.Count <= 4)
        {
            isDynamicDeckSystem = false;
            for (int i = 0; i < uiBattleSpawnCards.Length; i++)
            {
                if (uiBattleSpawnCards[i] == null) continue;

                if (i < unlockedCards.Count)
                {
                    uiBattleSpawnCards[i].SetCardType(unlockedCards[i]);
                }
                else
                {
                    uiBattleSpawnCards[i].SetCardType(BattleObjectType.None);
                }
            }
        }
        // 해금 카드가 5개 이상인 경우: 동적 덱 순환 시스템 활성화 (무작위 4개 손패 배치 + 순환 드로우)
        else
        {
            isDynamicDeckSystem = true;
            BuildAndShuffleDeck();
            DealInitialHand();
        }
    }

    private void BuildAndShuffleDeck()
    {
        drawDeck.Clear();
        discardDeck.Clear();
        drawDeck.AddRange(unlockedCards);
        ShuffleList(drawDeck);
    }

    private void DealInitialHand()
    {
        for (int i = 0; i < uiBattleSpawnCards.Length; i++)
        {
            if (uiBattleSpawnCards[i] == null) continue;

            if (drawDeck.Count > 0)
            {
                BattleObjectType drawn = DrawCardFromDeck();
                uiBattleSpawnCards[i].SetCardType(drawn);
            }
            else
            {
                uiBattleSpawnCards[i].SetCardType(BattleObjectType.None);
            }
        }
    }

    private BattleObjectType DrawCardFromDeck()
    {
        if (drawDeck.Count == 0)
        {
            if (discardDeck.Count > 0)
            {
                drawDeck.AddRange(discardDeck);
                discardDeck.Clear();
                ShuffleList(drawDeck);
            }
            else
            {
                return BattleObjectType.None;
            }
        }

        BattleObjectType drawn = drawDeck[0];
        drawDeck.RemoveAt(0);
        return drawn;
    }

    private void OnHandCardClicked(UIBattleSpawnCard cardSlot)
    {
        if (!isDynamicDeckSystem) return;

        BattleObjectType usedType = cardSlot.CurrentObjectType;
        if (usedType == BattleObjectType.None)
            return;

        if (usedType == BattleObjectType.InkBomb)
            return;

        // 소환 사용된 카드는 버린 카드 덱으로 이동
        discardDeck.Add(usedType);

        // 해당 슬롯에 덱의 다음 카드를 순환 드로우하여 배치
        BattleObjectType nextCard = DrawCardFromDeck();
        cardSlot.SetCardType(nextCard);
    }

    private void OnRerollButtonClicked()
    {
        if (rerollCooldownTimer > 0f) return;

        // 1 Ink 소모 검사 및 차감
        if (BattleInkManager.Instance != null)
        {
            if (!BattleInkManager.Instance.TrySpendInk(teamColor, 1))
            {
                Debug.LogWarning("[UIBattleObjCardList] Ink Reroll 실패: 잉크가 부족합니다.");
                return;
            }
        }

        // 2초 쿨타임 적용
        rerollCooldownTimer = rerollCooldownDuration;
        UpdateRerollButtonState();

        // 5개 이상 동적 덱인 경우: 현재 손패 4장을 버린 카드에 넣고 전체 덱 재구축 후 무작위 4장 재배치
        if (isDynamicDeckSystem)
        {
            for (int i = 0; i < uiBattleSpawnCards.Length; i++)
            {
                if (uiBattleSpawnCards[i] != null && uiBattleSpawnCards[i].CurrentObjectType != BattleObjectType.None)
                {
                    discardDeck.Add(uiBattleSpawnCards[i].CurrentObjectType);
                }
            }

            drawDeck.AddRange(discardDeck);
            discardDeck.Clear();
            ShuffleList(drawDeck);

            DealInitialHand();
        }
        else
        {
            // 4개 이하 고정 손패의 경우 무작위 셔플 표시
            ShuffleList(unlockedCards);
            for (int i = 0; i < uiBattleSpawnCards.Length && i < unlockedCards.Count; i++)
            {
                if (uiBattleSpawnCards[i] != null)
                {
                    uiBattleSpawnCards[i].SetCardType(unlockedCards[i]);
                }
            }
        }

        Debug.Log("[UIBattleObjCardList] Ink Reroll 손패 새로고침 완료!");
    }

    private void UpdateRerollCooldown()
    {
        if (rerollCooldownTimer > 0f)
        {
            rerollCooldownTimer -= Time.deltaTime;
            if (rerollCooldownTimer <= 0f)
            {
                rerollCooldownTimer = 0f;
            }
            UpdateRerollButtonState();
        }
    }

    private void OnInkChanged(TeamColor team, float currentInk, float maxInk)
    {
        if (team == teamColor)
        {
            UpdateRerollButtonState();
        }
    }

    private void UpdateRerollButtonState()
    {
        if (rerollButton == null) return;

        bool hasInk = BattleInkManager.Instance == null || BattleInkManager.Instance.CanSpendInk(teamColor, 1);
        bool isCooldownReady = (rerollCooldownTimer <= 0f);

        rerollButton.interactable = hasInk && isCooldownReady;

        if (textRerollCooldown != null)
        {
            if (rerollCooldownTimer > 0f)
            {
                textRerollCooldown.gameObject.SetActive(true);
                textRerollCooldown.text = $"{rerollCooldownTimer:F1}s";
            }
            else
            {
                textRerollCooldown.gameObject.SetActive(false);
            }
        }
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randIndex = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[randIndex];
            list[randIndex] = temp;
        }
    }
}
