using UnityEngine;

namespace ColorCrash.Effects
{
    /// <summary>
    /// 글로벌 이펙트 시스템에서 사용할 이펙트의 종류
    /// </summary>
    public enum EffectType
    {
        Impact,  // 타격/피격 시 발생하는 파편 이펙트
        Muzzle,  // 공격/발사 시 발생하는 섬광 이펙트
        UnitDestroy, // 유닛 파괴될 때 발생하는 폭발 이펙트
        TowerDestroy, // 타워 파괴될 때 발생하는 폭발 이펙트
        TileChange, // 타일 진영 변경 시 번쩍이는 이펙트
        MeleeSlash, // 근접 유닛 공격 이펙트 (검기)
        CannonFire, // 대포 발포 이펙트 (섬광 + 연기)
        CannonExplosion, // 대포 직사 폭발 이펙트 (빠른 파편)
        MortarExplosion, // 박격포 곡사 폭발 이펙트 (충격파 + 거대 먼지)
        InkBombExplosion // 잉크 폭탄 착탄 이펙트 (진영 색상 잉크 폭발 + 파티클)
    }
}
