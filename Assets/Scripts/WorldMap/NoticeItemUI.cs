using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorCrash.UI
{
    [RequireComponent(typeof(CanvasGroup), typeof(LayoutElement))]
    public class NoticeItemUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI noticeText;
        [SerializeField] private Transform visualContainer; // Layout Group 충돌 방지용 자식 비주얼 컨테이너

        [Header("Animation Settings")]
        [SerializeField] private float displayDuration = 3f; // 노출 대기 시간
        [SerializeField] private float fadeDuration = 1f;    // 페이드아웃 시간
        [SerializeField] private float moveUpAmount = 30f;   // 페이드 도중 위로 이동할 Y 오프셋

        private CanvasGroup canvasGroup;
        private LayoutElement layoutElement;
        private RectTransform rectTransform;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            layoutElement = GetComponent<LayoutElement>();
            rectTransform = GetComponent<RectTransform>();
        }

        public void Setup(string text, Color textColor)
        {
            if (noticeText != null)
            {
                noticeText.text = text;
                noticeText.color = textColor;
            }

            // 시작 상태 설정
            canvasGroup.alpha = 1f;
            StartCoroutine(NoticeLifecycleRoutine());
        }

        private IEnumerator NoticeLifecycleRoutine()
        {
            // 3초간 머물기
            yield return new WaitForSeconds(displayDuration);

            // 1초간 페이드 아웃 + 위로 서서히 이동 + 높이 감소를 통한 부드러운 스크롤 업
            float elapsed = 0f;
            
            // Layout Group의 강제 위치 정렬을 우회하기 위해 자식 비주얼 컨테이너만 위로 이동시킵니다.
            Transform targetTransform = visualContainer != null ? visualContainer : transform;
            Vector3 startPos = targetTransform.localPosition;
            Vector3 targetPos = startPos + new Vector3(0f, moveUpAmount, 0f);

            // 최초 높이 계산 (preferredHeight가 지정되지 않았다면 RectTransform 높이 활용)
            float startHeight = layoutElement.preferredHeight;
            if (startHeight <= 0)
            {
                startHeight = rectTransform.rect.height;
            }

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = elapsed / fadeDuration;

                // 투명도 조절
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, normalizedTime);

                // Y축 위쪽으로 부드럽게 이동 (개별 텍스트가 붕 뜨는 연출)
                targetTransform.localPosition = Vector3.Lerp(startPos, targetPos, normalizedTime);

                // 높이를 부드럽게 0으로 수축하여 아래에 대기 중이던 알림들을 서서히 위로 끌어올림
                layoutElement.preferredHeight = Mathf.Lerp(startHeight, 0f, normalizedTime);

                yield return null;
            }

            // 라이프사이클 종료 후 오브젝트 파괴
            Destroy(gameObject);
        }
    }
}
