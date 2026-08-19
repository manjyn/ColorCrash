using UnityEngine;

public enum BattleObjectType : byte
{
    None = 0, // 미지정 / 소환 불가

    // --- 유닛 ---
    Footman = 1,      // 보병
    Archer = 2,       // 궁수
    EliteUnit = 3,    // 엘리트 보병
    Warlord = 4,      // 워로드

    // --- 건물 ---
    Barricade = 10,   // 바리케이드
    Cannon = 11,      // 캐논 타워
    Mortar = 12,      // 박격포 타워

    // --- 마법 스펠 ---
    InkBomb = 20      // Ink Bomb (3x3 즉시 도색 스펠)
}