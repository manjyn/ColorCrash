using UnityEngine;
using ColorCrash.UI;
using ColorCrash.Effects;

/// <summary>
/// 모든 건물 객체의 최상위 부모 클래스.
/// 대규모 전투 게임의 최적화를 위해 공통 데이터와 필수 인터페이스를 정의합니다.
/// </summary>
public abstract class TowerBase : MonoBehaviour, IDamageable
{
    [Header("Tower Database Settings")]
    [SerializeField] protected string towerId;
    public string TowerId => towerId;

    [Header("Building Base Settings")]
    [SerializeField] protected string buildingName;
    [HideInInspector] public int sizeX = 1;
    [HideInInspector] public int sizeZ = 1;
    public int SizeX => sizeX;
    public int SizeZ => sizeZ;
    [SerializeField] protected TeamColor teamColor;
    
    [Header("UI Reference")]
    [SerializeField] protected HealthBarUI healthBarUI;
    
    [Header("Building Health Settings")]
    [HideInInspector] public float maxHp = 500f;
    protected float currentHp;
    [HideInInspector] public float defense = 5f;
    
    public TeamColor TeamColor => teamColor;
    public bool IsDead => currentHp <= 0f;
    public float MaxHp => maxHp;
    public float CurrentHp => currentHp;
    public event System.Action<float, float> OnHealthChanged;
    
    // 건물의 상태를 나타내는 기본 변수들
    protected bool isInitialized = false;

    /// <summary>
    /// 건물이 생성될 때 초기화 로직
    /// </summary>
    public virtual void Initialize(TeamColor team)
    {
        // 1. TowerDatabase 데이터 로드
        if (TowerManager.Instance != null && TowerManager.Instance.TowerDatabase != null)
        {
            if (TowerManager.Instance.TowerDatabase.TryGetRecord(towerId, out var record))
            {
                buildingName = record.TowerName;
                sizeX = record.SizeX;
                sizeZ = record.SizeZ;
                maxHp = record.MaxHp;
                defense = record.Defense;
            }
            else
            {
                Debug.LogWarning($"[TowerBase] TowerDatabase에서 ID '{towerId}'를 찾을 수 없습니다. 기본값을 사용합니다. (오브젝트: {gameObject.name})");
            }
        }

        teamColor = team;
        currentHp = maxHp;
        OnHealthChanged?.Invoke(currentHp, maxHp);
        
        if (healthBarUI != null)
        {
            healthBarUI.Setup(this, transform);
        }
        
        isInitialized = true;
    }

    /// <summary>
    /// 건물이 파괴되거나 제거될 때 호출
    /// </summary>
    public abstract void OnRemoved();

    public virtual void TakeDamage(float amount)
    {
        if (IsDead) return;

        float actualDamage = Mathf.Max(1f, amount - defense);
        currentHp -= actualDamage;

        if (currentHp <= 0f)
        {
            currentHp = 0f;
        }

        OnHealthChanged?.Invoke(currentHp, maxHp);

        if (currentHp <= 0f)
        {
            if (EffectManager.Instance != null)
            {
                // 건물 폭발 이펙트 재생 (색상은 기본 하얀색으로 지정)
                EffectManager.Instance.PlayEffect(EffectType.TowerDestroy, transform.position, Color.white);
            }
            OnRemoved();
        }
    }
}
