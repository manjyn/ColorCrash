namespace ColorCrash.WorldMap
{
    public enum TileOwnerState
    {
        Neutral,
        Blue,
        Red
    }

    public enum TileFogState
    {
        Revealed,
        Blind
    }

    public enum TileResourceType
    {
        None,      // 빈 땅
        Mountain,  // 산맥(이동불가)
        GoldMine,  // 금광
        Farmland,  // 농지
        Pasture,   // 목초지
    }

    public enum TileDevelopmentType
    {
        None,       // 개발 안 됨
        Mine,       // 금광 개발 완료
        Farm,       // 농장 개발 완료
        Ranch       // 목장 개발 완료
    }

    public enum TileStructureType
    {
        None,       // 구조물 없음
        MainBase,   // 본진 기지 (고정)
        Fortress,   // 요새 (건설/파괴 가능)
        Bandit      // 도적단
    }
}
