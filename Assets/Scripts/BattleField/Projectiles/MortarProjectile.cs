using UnityEngine;
using System.Collections.Generic;
using ColorCrash.Units;
using ColorCrash.Effects;

public class MortarProjectile : ProjectileBase
{
    [HideInInspector] public float speed = 10f;
    [HideInInspector] public float splashRadius = 3f;
    [HideInInspector] public float arcHeight = 5f;

    private Vector3 startPos;
    private Vector3 targetPos;
    private float progress;
    private float totalDistance;

    // 자체 풀링 로직은 ProjectileManager로 이관되어 제거되었습니다.

    public override void Initialize(UnitBase target, float damage, TeamColor teamColor)
    {
        Initialize(target.transform.position, damage, teamColor);
    }

    public void Initialize(Vector3 targetPosition, float damage, TeamColor teamColor)
    {
        this.damage = damage;
        this.teamColor = teamColor;

        // DB에서 스탯 로드
        if (ProjectileManager.Instance != null && ProjectileManager.Instance.ProjectileDatabase != null)
        {
            if (ProjectileManager.Instance.ProjectileDatabase.TryGetRecord(projectileId, out var record))
            {
                speed = record.Speed;
                splashRadius = record.SplashRadius;
                arcHeight = record.ArcHeight;
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
            if (ProjectileManager.Instance != null)
                ProjectileManager.Instance.DespawnProjectile(this);
            else
                Destroy(gameObject);
            return;
        }

        progress += (speed * Time.deltaTime) / totalDistance;
        
        Vector3 currentPos = Vector3.Lerp(startPos, targetPos, progress);
        currentPos.y += Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI) * arcHeight;

        transform.position = currentPos;

        if (progress >= 1f)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (EffectManager.Instance != null)
        {
            // 진영 색상 무시하고 이펙트 원본 색상 그대로 사용
            EffectManager.Instance.PlayEffect(EffectType.MortarExplosion, transform.position, Color.white);
        }

        // 1. 물리 데미지 (유닛, 건물 타격)
        Collider[] colliders = Physics.OverlapSphere(transform.position, splashRadius);
        foreach (var col in colliders)
        {
            // 콜라이더가 자식 오브젝트에 있는 경우를 대비해 InParent로 탐색합니다.
            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.TeamColor != teamColor)
            {
                damageable.TakeDamage(damage);
            }
        }

        // 2. 바닥 타일 점령 (물리 충돌 대신 그리드 인덱스 수학적 계산)
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
