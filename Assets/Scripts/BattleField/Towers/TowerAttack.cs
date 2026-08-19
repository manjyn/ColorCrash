using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ColorCrash.Units;

public abstract class TowerAttack : TowerBase
{
    [Header("Attack Settings")]
    [HideInInspector] public float attackRange = 5f;
    [HideInInspector] public float attackCooldown = 1.5f;
    [HideInInspector] public float damage = 20f;
    [SerializeField] protected ProjectileBase projectilePrefab;
    [SerializeField] protected Transform firePoint;
    
    [Header("Rotation Settings")]
    public float rotationSpeed = 10f;

    protected UnitBase currentTarget;
    protected TowerBase currentTargetBuilding;
    protected Vector3? currentTargetPosition;
    
    protected bool isAttacking = false;
    private Coroutine attackCoroutine;

    protected virtual void Start()
    {
        if (isInitialized && attackCoroutine == null)
        {
            attackCoroutine = StartCoroutine(SearchAndAttackRoutine());
        }
    }

    public override void Initialize(TeamColor team)
    {
        // 부모(TowerBase)의 Initialize가 먼저 호출되어 TowerDatabase 데이터를 읽어옵니다.
        base.Initialize(team);

        // 추가 공격 스탯 덮어쓰기 (DB 로드)
        if (TowerManager.Instance != null && TowerManager.Instance.TowerDatabase != null)
        {
            if (TowerManager.Instance.TowerDatabase.TryGetRecord(towerId, out var record))
            {
                attackRange = record.AttackRange;
                attackCooldown = record.AttackCooldown;
                damage = record.AttackDamage;
            }
        }

        if (attackCoroutine == null)
        {
            attackCoroutine = StartCoroutine(SearchAndAttackRoutine());
        }
    }

    protected virtual IEnumerator SearchAndAttackRoutine()
    {
        WaitForSeconds searchWait = new WaitForSeconds(0.2f);
        WaitForSeconds cooldownWait = new WaitForSeconds(attackCooldown);

        while (!IsDead)
        {
            if (!HasValidTarget())
            {
                FindTarget();
                if (!HasValidTarget())
                {
                    yield return searchWait;
                    continue;
                }
            }

            Vector3 targetPos = GetCurrentTargetPosition();
            
            // 조준 회전 완료 후 공격
            yield return StartCoroutine(RotateTowardsTargetRoutine(targetPos));

            if (IsDead) break;

            FireProjectile();
            yield return cooldownWait;
        }
    }

    protected virtual bool HasValidTarget()
    {
        if (currentTarget != null && !currentTarget.IsDead && IsTargetInRange(currentTarget.transform.position)) return true;
        if (currentTargetBuilding != null && !currentTargetBuilding.IsDead && IsTargetInRange(currentTargetBuilding.transform.position)) return true;
        if (currentTargetPosition.HasValue && IsTargetInRange(currentTargetPosition.Value)) return true;
        return false;
    }

    protected virtual void FindTarget()
    {
        currentTarget = null;
        float minDistance = attackRange * attackRange;
        
        List<UnitBase> enemies = (TeamColor == TeamColor.Blue) ? UnitManager.Instance.activeRedUnits : UnitManager.Instance.activeBlueUnits;

        for (int i = 0; i < enemies.Count; i++)
        {
            UnitBase enemy = enemies[i];
            if (enemy == null || enemy.IsDead) continue;

            float sqrDist = (transform.position - enemy.transform.position).sqrMagnitude;
            if (sqrDist <= minDistance)
            {
                minDistance = sqrDist;
                currentTarget = enemy;
            }
        }
    }

    protected virtual bool IsTargetInRange(Vector3 targetPos)
    {
        return (transform.position - targetPos).sqrMagnitude <= (attackRange * attackRange);
    }

    protected virtual void ClearTarget()
    {
        currentTarget = null;
        currentTargetBuilding = null;
        currentTargetPosition = null;
    }

    protected virtual Vector3 GetCurrentTargetPosition()
    {
        if (currentTarget != null) return currentTarget.transform.position;
        if (currentTargetBuilding != null) return currentTargetBuilding.transform.position;
        if (currentTargetPosition.HasValue) return currentTargetPosition.Value;
        return transform.position + transform.forward;
    }

    protected virtual IEnumerator RotateTowardsTargetRoutine(Vector3 targetPos)
    {
        Vector3 direction = (targetPos - transform.position);
        direction.y = 0; // 수평 회전만 허용

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            
            // 타겟 각도와의 차이가 1도 이내가 될 때까지 부드럽게 회전
            while (Quaternion.Angle(transform.rotation, targetRotation) > 1f)
            {
                if (IsDead) yield break;

                // 타겟 위치가 유동적인 유닛일 경우 목표 회전값 갱신
                if (currentTarget != null)
                {
                    Vector3 dynamicDir = (currentTarget.transform.position - transform.position);
                    dynamicDir.y = 0;
                    if (dynamicDir.sqrMagnitude > 0.001f)
                    {
                        targetRotation = Quaternion.LookRotation(dynamicDir);
                    }
                }

                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
                yield return null;
            }
            
            // 최종 각도 맞춤
            transform.rotation = targetRotation;
        }
    }

    protected abstract void FireProjectile();
    
    public override void OnRemoved()
    {
        Debug.Log($"{buildingName}가 파괴되었습니다");
        if (GridManager.Instance != null)
        {
            GridManager.Instance.RemoveBuilding(transform.position, SizeX, SizeZ);
        }

        // 재사용을 위해 코루틴 참조 초기화
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        // 풀링 시스템을 통해 보관함으로 반환 (Destroy 대체)
        if (TowerManager.Instance != null)
        {
            TowerManager.Instance.DespawnTower(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
