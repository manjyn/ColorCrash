using System.Collections;
using UnityEngine;
using ColorCrash.Effects;

namespace ColorCrash.Units
{
    /// <summary>
    /// 대규모 타일 강탈 디펜스 게임의 원거리 유닛 부모 클래스
    /// UnitBase를 상속받아 원거리 전용 이동, 타겟팅 및 공격 로직의 뼈대를 제공합니다.
    /// </summary>
    public abstract class RangedUnit : UnitBase
    {
        [Header("Ranged Settings")]
        public GameObject projectilePrefab;
        public Transform firePoint;

        protected override IEnumerator SearchTargetRoutine()
        {
            // 임시: 탐색 대기
            yield return searchTick;
        }

        protected override IEnumerator MoveToTargetRoutine()
        {
            // 임시: 이동 대기
            yield return null;
        }

        public override void OnHit()
        {
            base.OnHit();

            // 발사 이펙트 출력
            if (EffectManager.Instance != null && firePoint != null)
            {
                Color effectColor = (teamColor == TeamColor.Blue) ? Color.blue : Color.red;
                EffectManager.Instance.PlayEffect(EffectType.Muzzle, firePoint.position, effectColor);
            }

            Debug.Log($"{gameObject.name} ({teamColor}) 가 원거리 공격을 수행했습니다!");
        }

        protected override IEnumerator AttackRoutine()
        {
            while (currentState == State.Attack)
            {
                PlayAttackAnimation();

                yield return attackCooldownTick;
                
                if (currentState != State.Attack) yield break;

                if (!isTargetTile && (targetTransform == null || !targetTransform.gameObject.activeInHierarchy || targetDamageable == null || targetDamageable.IsDead))
                {
                    ChangeState(State.SearchTarget);
                    yield break;
                }

                if (isTargetTile && GridManager.Instance != null)
                {
                    int targetIdx = GridManager.Instance.WorldToIndex(targetPosition);
                    if (targetIdx != -1 && (GridManager.Instance.tileStates[targetIdx] == teamColor || GridManager.Instance.tileStates[targetIdx] == TeamColor.Obstacle))
                    {
                        ChangeState(State.SearchTarget);
                        yield break;
                    }
                }
            }
        }
    }
}
