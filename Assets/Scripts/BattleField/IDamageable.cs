/// <summary>
/// 데미지를 입을 수 있는 대상(유닛, 건물 등)을 정의하는 공통 인터페이스
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// 대상의 진영 색상
    /// </summary>
    TeamColor TeamColor { get; }

    /// <summary>
    /// 대상의 사망 여부
    /// </summary>
    bool IsDead { get; }

    /// <summary>
    /// 대상의 최대 체력
    /// </summary>
    float MaxHp { get; }

    /// <summary>
    /// 대상의 현재 체력
    /// </summary>
    float CurrentHp { get; }

    /// <summary>
    /// 체력 변경 이벤트 (현재체력, 최대체력)
    /// </summary>
    event System.Action<float, float> OnHealthChanged;

    /// <summary>
    /// 데미지를 처리하는 함수
    /// </summary>
    /// <param name="amount">공격 강도(피해량)</param>
    void TakeDamage(float amount);
}
