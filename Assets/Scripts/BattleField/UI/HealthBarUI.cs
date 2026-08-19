using UnityEngine;
using UnityEngine.UI;

namespace ColorCrash.UI
{
    /// <summary>
    /// 모든 유닛과 건물에 범용적으로 사용할 수 있는 HP 게이지 바 클래스입니다.
    /// 체력 변경 이벤트를 구독하여 게이지 UI를 갱신합니다.
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image fillImage;
        [SerializeField] private Canvas canvas;

        [Header("Settings")]
        [Tooltip("월드 스페이스 캔버스일 경우 카메라를 바라볼지 여부")]
        [SerializeField] private bool lookAtCamera = true;
        [Tooltip("true일 경우 대상의 위치(offset 포함)를 매 프레임 따라갑니다. 자식 객체가 아닐 때 유용합니다.")]
        [SerializeField] private bool followTargetPosition = false;
        [SerializeField] private Vector3 offset = new Vector3(0, 2f, 0);
        
        [Header("Team Colors")]
        [SerializeField] private Color blueTeamColor = new Color(0.2f, 0.6f, 1f, 1f); // 파란색 기본값
        [SerializeField] private Color redTeamColor = new Color(1f, 0.2f, 0.2f, 1f); // 빨간색 기본값
        
        private IDamageable targetDamageable;
        private Camera mainCamera;
        private Transform targetTransform;

        private void Start()
        {
            mainCamera = Camera.main;
            
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                canvas.worldCamera = mainCamera;
            }

            if (targetDamageable != null)
            {
                // 체력 변경 이벤트 구독
                targetDamageable.OnHealthChanged += UpdateHealthBar;
                UpdateHealthBar(targetDamageable.CurrentHp, targetDamageable.MaxHp);
            }
        }

        /// <summary>
        /// 외부에서 동적으로 대상(IDamageable)을 설정할 때 사용합니다.
        /// </summary>
        public void Setup(IDamageable target, Transform targetTf)
        {
            if (targetDamageable != null)
            {
                targetDamageable.OnHealthChanged -= UpdateHealthBar;
            }

            targetDamageable = target;
            targetTransform = targetTf;

            if (targetDamageable != null)
            {
                // 진영에 따른 체력바 색상 적용
                if (fillImage != null)
                {
                    fillImage.color = targetDamageable.TeamColor == TeamColor.Blue ? blueTeamColor : redTeamColor;
                }

                targetDamageable.OnHealthChanged += UpdateHealthBar;
                UpdateHealthBar(targetDamageable.CurrentHp, targetDamageable.MaxHp);
            }
        }

        private void OnDestroy()
        {
            if (targetDamageable != null)
            {
                targetDamageable.OnHealthChanged -= UpdateHealthBar;
            }
        }

        private void UpdateHealthBar(float currentHp, float maxHp)
        {
            if (fillImage != null)
            {
                fillImage.fillAmount = maxHp > 0 ? currentHp / maxHp : 0;
            }
        }

        private void LateUpdate()
        {
            if (followTargetPosition && targetTransform != null)
            {
                transform.position = targetTransform.position + offset;
            }

            if (lookAtCamera && mainCamera != null)
            {
                // 카메라를 바라보도록 회전
                transform.rotation = mainCamera.transform.rotation;
            }
        }
    }
}
