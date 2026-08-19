using UnityEngine;
using ColorCrash.Units;
using ColorCrash.Effects;

namespace ColorCrash.Projectiles
{
    public class ArcherProjectile : ProjectileBase
    {
        [HideInInspector] public float speed = 15f;
        [HideInInspector] public float arcHeight = 3f;

        private Vector3 startPos;
        private Vector3 targetPos;
        private float progress;
        private float totalDistance;
        private IDamageable targetDamageable;

        public override void Initialize(UnitBase target, float damage, TeamColor teamColor)
        {
            Initialize((IDamageable)target, target.transform.position, damage, teamColor);
        }

        public void Initialize(IDamageable targetDamageable, Vector3 targetPosition, float damage, TeamColor teamColor)
        {
            this.damage = damage;
            this.teamColor = teamColor;
            this.targetDamageable = targetDamageable;

            if (ProjectileManager.Instance != null && ProjectileManager.Instance.ProjectileDatabase != null)
            {
                if (ProjectileManager.Instance.ProjectileDatabase.TryGetRecord(projectileId, out var record))
                {
                    speed = record.Speed;
                    arcHeight = record.ArcHeight;
                    // splashRadius는 사용하지 않음
                }
            }

            this.startPos = transform.position;
            this.targetPos = targetPosition;
            this.progress = 0f;
            this.totalDistance = Vector3.Distance(startPos, targetPos);

            TrailRenderer tr = GetComponentInChildren<TrailRenderer>();
            if (tr != null)
            {
                tr.Clear();
            }
        }

        private void Update()
        {
            if (totalDistance == 0)
            {
                Despawn();
                return;
            }

            // 타겟이 유닛/건물인 경우 위치를 실시간으로 갱신하여 유도탄처럼 날아가거나,
            // 쏘는 시점의 위치로 날아가려면 이 로직 생략. 
            // 여기서는 목표물의 현재 위치를 추적 (화살이 맞는 연출을 위해)
            if (targetDamageable != null && !targetDamageable.IsDead)
            {
                if (targetDamageable is MonoBehaviour targetMono)
                {
                    targetPos = targetMono.transform.position;
                }
            }

            progress += (speed * Time.deltaTime) / totalDistance;
            
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, progress);
            currentPos.y += Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI) * arcHeight;

            // 투사체가 날아가는 방향 바라보기
            if (progress > 0 && progress < 1f)
            {
                Vector3 nextPos = Vector3.Lerp(startPos, targetPos, progress + 0.01f);
                nextPos.y += Mathf.Sin(Mathf.Clamp01(progress + 0.01f) * Mathf.PI) * arcHeight;
                transform.rotation = Quaternion.LookRotation(nextPos - transform.position);
            }

            transform.position = currentPos;

            if (progress >= 1f)
            {
                HitTarget();
            }
        }

        private void HitTarget()
        {
            // 이펙트 재생
            if (EffectManager.Instance != null)
            {
                // 화살 피격 이펙트 (임시로 MortarExplosion 대신 Impact나 Hit 사용 권장, 없으면 기본 흰색 이펙트)
                EffectManager.Instance.PlayEffect(EffectType.Impact, transform.position, Color.white);
            }

            // 단일 타겟 데미지 처리
            if (targetDamageable != null && !targetDamageable.IsDead && targetDamageable.TeamColor != teamColor)
            {
                targetDamageable.TakeDamage(damage);
            }

            Despawn();
        }

        private void Despawn()
        {
            if (ProjectileManager.Instance != null)
            {
                ProjectileManager.Instance.DespawnProjectile(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
