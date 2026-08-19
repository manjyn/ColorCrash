using UnityEngine;
using ColorCrash.Effects;

namespace ColorCrash.Projectiles
{
    /// <summary>
    /// 잉크 폭탄 전용 포물선 비행 및 폭발 연출 투사체.
    /// 팀별 단 1개의 전용 인스턴스로 관리 및 재활용(Zero Instantiation)됩니다.
    /// </summary>
    public class InkBombProjectile : MonoBehaviour
    {
        [Header("Projectile Flight Settings")]
        public float flyDuration = 0.55f; // 비행 시간 (초)
        public float arcHeight = 12f;      // 포물선 최대 높이

        private Vector3 startPos;
        private Vector3 targetPos;
        private Vector2Int targetCenterTile;
        private TeamColor teamColor;
        private float spellDamage;
        private int spellRadius;
        private float elapsedTime;
        private bool isFlying = false;

        private TrailRenderer trailRenderer;

        private void Awake()
        {
            trailRenderer = GetComponentInChildren<TrailRenderer>();
        }

        /// <summary>
        /// 잉크 폭탄 발사를 개시합니다.
        /// </summary>
        public void Launch(Vector3 startPosition, Vector3 targetPosition, Vector2Int centerTile, TeamColor team, float damage = 50f, int radius = 1)
        {
            this.startPos = startPosition;
            this.targetPos = targetPosition;
            this.targetCenterTile = centerTile;
            this.teamColor = team;
            this.spellDamage = damage;
            this.spellRadius = radius;
            this.elapsedTime = 0f;

            transform.position = startPos;

            if (trailRenderer != null)
            {
                trailRenderer.Clear();
                // 팀 색상 틴트 설정 (Blue: 파랑계열, Red: 빨강계열)
                Color tintColor = (team == TeamColor.Blue) ? new Color(0.2f, 0.6f, 1f) : new Color(1f, 0.25f, 0.25f);
                trailRenderer.startColor = tintColor;
                trailRenderer.endColor = new Color(tintColor.r, tintColor.g, tintColor.b, 0f);
            }

            gameObject.SetActive(true);
            isFlying = true;
        }

        private void Update()
        {
            if (!isFlying) return;

            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / flyDuration);

            // 포물선 궤적 계산 (Vector3.Lerp + Sine 높이)
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, progress);
            currentPos.y += Mathf.Sin(progress * Mathf.PI) * arcHeight;

            transform.position = currentPos;

            if (progress >= 1.0f)
            {
                OnImpact();
            }
        }

        /// <summary>
        /// 타겟 타일 착탄 시 폭발 이펙트 재생 및 3x3 도색/데미지 실행
        /// </summary>
        private void OnImpact()
        {
            isFlying = false;

            // 1. 진영별 색상 폭발 VFX 재생
            if (EffectManager.Instance != null)
            {
                Color explosionColor = (teamColor == TeamColor.Blue) ? new Color(0.15f, 0.55f, 1f) : new Color(1f, 0.2f, 0.2f);
                EffectManager.Instance.PlayEffect(EffectType.InkBombExplosion, targetPos, explosionColor);
            }

            // 2. 3x3 타일 도색 및 유닛/건물 50 데미지 실행
            SpellSpawnHandler.ExecuteSpellAtTile(targetCenterTile, teamColor, spellDamage, spellRadius);

            // 3. 비활성화 (재사용 준비)
            gameObject.SetActive(false);
        }
    }
}
