using UnityEngine;

/// <summary>
/// 유닛, 타워 등 객체 생성(스폰) 로직을 규격화하는 인터페이스
/// </summary>
public interface ISpawnHandler
{
    /// <summary>
    /// 마우스 드래그 중 미리보기(더미) 오브젝트의 위치를 갱신하고 최종 좌표 반환.
    /// </summary>
    /// <param name="raycastHitPoint">마우스 레이캐스트가 지면과 닿은 월드 좌표</param>
    /// <param name="dummy">현재 이동 중인 미리보기 오브젝트 (null일 경우 좌표만 계산하여 반환)</param>
    /// <returns>오브젝트가 최종적으로 위치할 월드 좌표</returns>
    Vector3 UpdatePreviewPosition(Vector3 raycastHitPoint, GameObject dummy);

    /// <summary>
    /// 해당 위치(position)가 지정된 팀(team)의 영토 및 조건에 맞는 유효한 스폰 위치인지 검사.
    /// </summary>
    bool IsValidPosition(Vector3 position, TeamColor team);

    /// <summary>
    /// 최종 검증 완료 후 지정된 위치에 실제 객체를 생성(스폰)하고 맵 데이터 갱신.
    /// </summary>
    void ExecuteSpawn(Vector3 position, TeamColor team);

    /// <summary>
    /// 객체가 타일 위에서 차지하는 영역(가로, 세로 칸 수) 크기 정보 반환.
    /// </summary>
    /// <returns>차지하는 영역의 Vector2Int 크기</returns>
    Vector2Int GetOccupiedSize();
}
