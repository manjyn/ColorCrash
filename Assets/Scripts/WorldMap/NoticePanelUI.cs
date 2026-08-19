using System.Collections.Generic;
using UnityEngine;

namespace ColorCrash.UI
{
    public class NoticePanelUI : MonoBehaviour
    {
        public static NoticePanelUI Instance { get; private set; }

        [Header("Prefabs & Containers")]
        [SerializeField] private NoticeItemUI noticeItemPrefab;
        [SerializeField] private Transform itemContainer;

        [Header("Settings")]
        [SerializeField] private bool showDebugNotices = true;
        //[SerializeField] private float duplicateCooldown = 3f; // 중복 메시지 무시 쿨타임 (초)

        [Header("Type Colors")]
        [SerializeField] private Color infoColor = Color.white;
        [SerializeField] private Color successColor = Color.white;
        [SerializeField] private Color warningColor = Color.red;
        [SerializeField] private Color debugColor = new Color(0.7f, 0.7f, 0.7f); // 연회색

        // 중복 방지 캐시: <메시지 내용, 마지막 노출 시간>
        private readonly Dictionary<string, float> lastNoticeTimes = new Dictionary<string, float>();

        public bool ShowDebugNotices
        {
            get => showDebugNotices;
            set => showDebugNotices = value;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (itemContainer == null)
            {
                itemContainer = transform;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 화면 상단에 알림 메시지를 출력합니다.
        /// </summary>
        public void ShowNotice(string message, NoticeType type)
        {
            if (string.IsNullOrEmpty(message)) return;

            // 1. 디버그 모드가 꺼져있다면 디버그 알림 무시
            if (type == NoticeType.Debug && !showDebugNotices) return;

            // 2. 중복 방지 체크 (동일 메시지 3초 쿨타임 무시)
            float currentTime = Time.time;
            /*if (lastNoticeTimes.TryGetValue(message, out float lastTime))
            {
                if (currentTime - lastTime < duplicateCooldown)
                    return;
            }*/

            // 마지막 노출 시각 갱신
            lastNoticeTimes[message] = currentTime;

            // 3. 메시지 색상 매핑
            Color textColor = infoColor;
            switch (type)
            {
                case NoticeType.Success:
                    textColor = successColor;
                    break;
                case NoticeType.Warning:
                    textColor = warningColor;
                    break;
                case NoticeType.Debug:
                    textColor = debugColor;
                    break;
            }

            // 4. 알림 아이템 생성 및 초기화
            if (noticeItemPrefab != null)
            {
                NoticeItemUI newItem = Instantiate(noticeItemPrefab, itemContainer);
                newItem.Setup(message, textColor);
            }
            else
            {
                Debug.LogWarning($"[NoticePanelUI] noticeItemPrefab이 할당되지 않았습니다. 메시지: {message}");
            }
        }

        /// <summary>
        /// itemContainer의 모든 자식 알림 아이템을 삭제합니다.
        /// </summary>
        public void Clear()
        {
            if (itemContainer == null) return;

            foreach (Transform child in itemContainer)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
