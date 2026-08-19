using UnityEngine;
using System.Collections.Generic;
using ColorCrash.Units;
using ColorCrash.Effects;

public class CannonProjectile : ProjectileBase
{
    [HideInInspector] public float speed = 15f;
    [HideInInspector] public float splashRadius = 1.5f;
    [HideInInspector] public float knockbackForce = 5f;
    [HideInInspector] public float knockbackDuration = 0.2f;

    private Vector3 startPos;
    private Vector3 targetPos;
    private Vector3 moveDirection;
    private float maxDistance;
    private float traveledDistance;

    private float detectionRadius = 0.5f;

    public override void Initialize(UnitBase target, float damage, TeamColor teamColor)
    {
        Initialize(target.transform.position, damage, teamColor);
    }

    public void Initialize(Vector3 targetPosition, float damage, TeamColor teamColor)
    {
        this.damage = damage;
        this.teamColor = teamColor;

        if (ProjectileManager.Instance != null && ProjectileManager.Instance.ProjectileDatabase != null)
        {
            if (ProjectileManager.Instance.ProjectileDatabase.TryGetRecord(projectileId, out var record))
            {
                speed = record.Speed;
                splashRadius = record.SplashRadius > 0 ? record.SplashRadius : 1.5f; 
            }
        }

        this.startPos = transform.position;
        this.targetPos = targetPosition;
        
        this.moveDirection = (targetPos - startPos).normalized;
        this.maxDistance = Vector3.Distance(startPos, targetPos);
        this.traveledDistance = 0f;
        
        TrailRenderer tr = GetComponentInChildren<TrailRenderer>();
        if (tr != null)
        {
            tr.Clear();
        }
    }

    private void Update()
    {
        if (maxDistance <= 0)
        {
            Despawn();
            return;
        }

        float moveStep = speed * Time.deltaTime;
        transform.position += moveDirection * moveStep;
        traveledDistance += moveStep;

        if (CheckImpact())
        {
            Explode();
            return;
        }

        if (traveledDistance >= maxDistance)
        {
            Explode();
        }
    }

    private bool CheckImpact()
    {
        // 1. 유닛, 건물 충돌 확인
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius);
        foreach (var col in colliders)
        {
            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.TeamColor != teamColor)
            {
                return true; // 적 유닛이나 건물을 만나면 즉시 충돌 판정
            }
        }

        return false;
    }

    private void Explode()
    {
        if (EffectManager.Instance != null)
        {
            // 투사체의 실제 진행 방향(moveDirection)을 회전값(Quaternion)으로 변환하여 전달
            Quaternion explosionRot = Quaternion.identity;
            if (moveDirection != Vector3.zero)
            {
                explosionRot = Quaternion.LookRotation(moveDirection);
            }
            EffectManager.Instance.PlayEffect(EffectType.CannonExplosion, transform.position, explosionRot, Color.white);
        }

        // 광역 데미지 및 넉백
        Collider[] colliders = Physics.OverlapSphere(transform.position, splashRadius);
        foreach (var col in colliders)
        {
            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.TeamColor != teamColor)
            {
                damageable.TakeDamage(damage);
                
                // 넉백 적용
                if (damageable is UnitBase unit)
                {
                    // 폭발 중심점(포탄 위치)에서 바깥쪽으로 밀려나도록 방향 계산
                    Vector3 kbDir = (unit.transform.position - transform.position);
                    kbDir.y = 0;
                    if (kbDir.sqrMagnitude < 0.01f) kbDir = moveDirection; // 겹쳐있을 경우 진행방향으로 넉백
                    unit.ApplyKnockback(kbDir.normalized, knockbackForce, knockbackDuration);
                }
            }
        }

        // 광역 타일 점령
        if (GridManager.Instance != null)
        {
            float tileSize = GridManager.tileSize;
            int radiusTiles = Mathf.CeilToInt(splashRadius / tileSize);
            
            int centerIndex = GridManager.Instance.WorldToIndex(transform.position);
            if (centerIndex != -1)
            {
                int centerX = centerIndex % GridManager.Instance.width;
                int centerZ = centerIndex / GridManager.Instance.width;

                for (int x = centerX - radiusTiles; x <= centerX + radiusTiles; x++)
                {
                    for (int z = centerZ - radiusTiles; z <= centerZ + radiusTiles; z++)
                    {
                        if (x >= 0 && x < GridManager.Instance.width && z >= 0 && z < GridManager.Instance.height)
                        {
                            int index = z * GridManager.Instance.width + x;
                            Vector3 tileWorldPos = GridManager.Instance.IndexToWorld(index);
                            
                            if (Vector3.Distance(tileWorldPos, transform.position) <= splashRadius)
                            {
                                TeamColor currentTileColor = GridManager.Instance.tileStates[index];
                                if (currentTileColor != teamColor)
                                {
                                    GridManager.Instance.PaintTile(tileWorldPos, teamColor);
                                }
                            }
                        }
                    }
                }
            }
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
