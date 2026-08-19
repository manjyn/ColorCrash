using System.Collections.Generic;
using UnityEngine;

namespace ColorCrash.AI
{
    /// <summary>
    /// 건물 배치 시 밀쳐내기(Push Out)가 필요한 유닛 정보 구조체입니다.
    /// </summary>
    public struct BuildingDisplacementInfo
    {
        public Vector2Int BuildingOriginTile;
        public Vector2Int BuildingSize;
        public List<Vector2Int> AffectedTiles;
    }

    /// <summary>
    /// 적 커맨더 AI의 유닛 스폰, 위험 길목 방어 건물 배치 및 마법 스펠 시전 타깃 좌표를 연산하는 클래스입니다.
    /// </summary>
    public class AITileTargeting : MonoBehaviour
    {
        [Header("전장 규격 설정")]
        [SerializeField] private int _mapWidth = 14;
        [SerializeField] private int _mapHeight = 24;

        // 충돌 분산 타일 탐색 시 사용하는 좌/우 인접 X 오프셋 순서
        private static readonly int[] XSpreadOffsets = new int[] { 0, 1, -1, 2, -2, 3, -3, 4, -4, 5, -5 };

        public void InitializeTargeting(int width, int height)
        {
            _mapWidth = Mathf.Max(1, width);
            _mapHeight = Mathf.Max(1, height);
        }

        #region Unit Spawn Targeting
        /// <summary>
        /// 유저 진격 유닛의 라인(X 좌표)에 대응하는 Red 팀 지정 스폰 존(Z=0, 1) 내부의 최적 X, Y 좌표를 연산합니다.
        /// </summary>
        public Vector2Int GetBestSpawnTile(int targetLineX, List<Vector2Int> occupiedSpawnTiles, int tileTargetOffset = 0)
        {
            int baseTargetX = targetLineX;
            if (tileTargetOffset > 0)
            {
                int randomOffset = Random.Range(-tileTargetOffset, tileTargetOffset + 1);
                baseTargetX += randomOffset;
            }
            baseTargetX = Mathf.Clamp(baseTargetX, 0, _mapWidth - 1);

            // Red팀 스폰 존: Z = 0, 1 (상단 영역)
            int primaryY = 0;
            int secondaryY = 1;

            for (int i = 0; i < XSpreadOffsets.Length; i++)
            {
                int candidateX = baseTargetX + XSpreadOffsets[i];
                if (candidateX >= 0 && candidateX < _mapWidth)
                {
                    Vector2Int candidateTile = new Vector2Int(candidateX, primaryY);
                    if (!IsTileOccupied(candidateTile, occupiedSpawnTiles))
                    {
                        return candidateTile;
                    }
                }
            }

            for (int i = 0; i < XSpreadOffsets.Length; i++)
            {
                int candidateX = baseTargetX + XSpreadOffsets[i];
                if (candidateX >= 0 && candidateX < _mapWidth)
                {
                    Vector2Int candidateTile = new Vector2Int(candidateX, secondaryY);
                    if (!IsTileOccupied(candidateTile, occupiedSpawnTiles))
                    {
                        return candidateTile;
                    }
                }
            }

            return new Vector2Int(baseTargetX, primaryY);
        }

        private bool IsTileOccupied(Vector2Int candidateTile, List<Vector2Int> occupiedSpawnTiles)
        {
            if (occupiedSpawnTiles == null || occupiedSpawnTiles.Count == 0) return false;
            return occupiedSpawnTiles.Contains(candidateTile);
        }
        #endregion

        #region Building Placement Targeting (Chokepoint Defense Line)
        /// <summary>
        /// Y축 5타일 이내 및 3x3 영역 내 적(Blue) 유닛 3기 이상 밀집 핫스팟 위치를 탐지합니다.
        /// </summary>
        public bool FindEnemyClusterChokepoint(
            List<Vector2Int> blueUnitPositions,
            int frontlineY,
            out Vector2Int clusterCenter)
        {
            clusterCenter = new Vector2Int(-1, -1);
            if (blueUnitPositions == null || blueUnitPositions.Count < 3) return false;

            int maxClusterCount = 0;
            Vector2Int bestCenter = new Vector2Int(-1, -1);

            for (int i = 0; i < blueUnitPositions.Count; i++)
            {
                Vector2Int candidate = blueUnitPositions[i];

                // Y축 거리가 Red전선(frontlineY) 기준 5타일 이내인 유닛만 대상
                if (Mathf.Abs(candidate.y - frontlineY) > 5) continue;

                int count = 0;
                for (int j = 0; j < blueUnitPositions.Count; j++)
                {
                    Vector2Int pos = blueUnitPositions[j];
                    if (Mathf.Abs(pos.x - candidate.x) <= 1 && Mathf.Abs(pos.y - candidate.y) <= 1)
                    {
                        count++;
                    }
                }

                if (count >= 3 && count > maxClusterCount)
                {
                    maxClusterCount = count;
                    bestCenter = candidate;
                }
            }

            if (maxClusterCount >= 3)
            {
                clusterCenter = bestCenter;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Red팀 영토(Z = 0 ~ frontlineY) 내부의 길목에 방어 요새(Barricade 4x1, Cannon 2x2, Mortar 2x2)를 안전하게 축조합니다.
        /// (Y축 역방향 탐색으로 최전방 전선 밀착 배치 및 Blue팀 영토 침범 금지 적용)
        /// </summary>
        public Vector2Int GetBestBuildingTile(
            BattleObjectType buildingType,
            int frontlineY,
            int targetLineX,
            System.Func<Vector2Int, TeamColor> getTileColor,
            System.Func<Vector2Int, bool> isObstacle,
            out BuildingDisplacementInfo displacementInfo)
        {
            displacementInfo = new BuildingDisplacementInfo
            {
                BuildingOriginTile = new Vector2Int(-1, -1),
                BuildingSize = Vector2Int.one,
                AffectedTiles = new List<Vector2Int>()
            };

            Vector2Int bSize = GetBuildingSize(buildingType);
            displacementInfo.BuildingSize = bSize;

            int startY = 0;
            int endY = 2;

            // Red팀 영토 내부 방향 Y 탐색 범위 (상단 Z=0 ~ frontlineY)
            switch (buildingType)
            {
                case BattleObjectType.Barricade: // 4x1: 최전방 전선 Y 밀착
                    startY = 0;
                    endY = Mathf.Clamp(frontlineY, 0, _mapHeight - 1);
                    break;
                case BattleObjectType.Cannon: // 2x2: 전선 바로 뒤 1칸 후방 지원
                    startY = 0;
                    endY = Mathf.Clamp(frontlineY - 1, 0, _mapHeight - 1);
                    break;
                case BattleObjectType.Mortar: // 2x2: 최후방 안전 포격 존 (Z = 0 ~ 2)
                    startY = 0;
                    endY = Mathf.Min(2, Mathf.Max(0, frontlineY));
                    break;
            }

            int targetChokepointX = Mathf.Clamp(targetLineX, 0, _mapWidth - bSize.x);
           
            // [핵심 개선] Y축 역방향 루프 (y = endY -> startY): 최전방 전선(하단) 타일을 최우선 탐색하여 밀착 배치
            for (int y = endY; y >= startY; y--)
            {
                for (int i = 0; i < XSpreadOffsets.Length; i++)
                {
                    int originX = targetChokepointX + XSpreadOffsets[i];
                    Vector2Int candidateOrigin = new Vector2Int(originX, y);

                    bool isValid = IsBuildingPlacementValid(candidateOrigin, bSize, getTileColor, isObstacle);
                    if (isValid)
                    {
                        displacementInfo.BuildingOriginTile = candidateOrigin;
                        displacementInfo.AffectedTiles = GetBuildingCoverageTiles(candidateOrigin, bSize);
                        return candidateOrigin;
                    }
                }
            }

            // 1순위 위험 라인 실패 시, 중앙 기준 역방향 서브 탐색
            int centerX = Mathf.Clamp((_mapWidth - bSize.x) / 2, 0, _mapWidth - 1);
            for (int y = endY; y >= startY; y--)
            {
                for (int i = 0; i < XSpreadOffsets.Length; i++)
                {
                    int originX = centerX + XSpreadOffsets[i];
                    Vector2Int candidateOrigin = new Vector2Int(originX, y);

                    if (IsBuildingPlacementValid(candidateOrigin, bSize, getTileColor, isObstacle))
                    {
                        displacementInfo.BuildingOriginTile = candidateOrigin;
                        displacementInfo.AffectedTiles = GetBuildingCoverageTiles(candidateOrigin, bSize);
                        return candidateOrigin;
                    }
                }
            }

            return new Vector2Int(-1, -1);
        }

        private bool IsBuildingPlacementValid(
            Vector2Int originTile,
            Vector2Int size,
            System.Func<Vector2Int, TeamColor> getTileColor,
            System.Func<Vector2Int, bool> isObstacle)
        {
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    Vector2Int checkTile = new Vector2Int(originTile.x + x, originTile.y + y);

                    // 1. 맵 경계 체크
                    if (checkTile.x < 0 || checkTile.x >= _mapWidth || checkTile.y < 0 || checkTile.y >= _mapHeight)
                    {
                        return false;
                    }

                    // 2. 맵 장애물 체크
                    if (isObstacle != null && isObstacle(checkTile))
                    {
                        return false;
                    }

                    // 3. 기존 건물 중복 겹침 체크 (이미 다른 건물/타워가 들어있는 타일 100% 거부)
                    if (GridManager.Instance != null && GridManager.Instance.gridBuildings != null)
                    {
                        int checkIndex = checkTile.y * _mapWidth + checkTile.x;
                        if (checkIndex >= 0 && checkIndex < GridManager.Instance.gridBuildings.Length)
                        {
                            if (GridManager.Instance.gridBuildings[checkIndex] != null)
                            {
                                return false; // 이미 기존 건물이 점유하고 있음!
                            }
                        }
                    }

                    // 4. Blue팀 타일 및 중립/상대 영토 완전 차단 (Red팀 스폰 존 Z=0,1 또는 100% Red팀 타일만 허용)
                    TeamColor tColor = getTileColor != null ? getTileColor(checkTile) : TeamColor.Neutral;

                    if (tColor == TeamColor.Blue)
                    {
                        return false; // 파란색 Blue 팀 타일에는 100% 절대로 건설 불가!
                    }

                    bool isRedSpawnZone = (checkTile.y < 2); // Z = 0, 1 (상단 Red 스폰 존)
                    if (!isRedSpawnZone && tColor != TeamColor.Red)
                    {
                        return false; // Red팀 소유 타일이 아니면 절대로 건설 불가!
                    }
                }
            }
            return true;
        }

        public Vector2Int GetBuildingSize(BattleObjectType type)
        {
            switch (type)
            {
                case BattleObjectType.Barricade: return new Vector2Int(4, 1);
                case BattleObjectType.Cannon: return new Vector2Int(2, 2);
                case BattleObjectType.Mortar: return new Vector2Int(2, 2);
                default: return Vector2Int.one;
            }
        }

        private List<Vector2Int> GetBuildingCoverageTiles(Vector2Int origin, Vector2Int size)
        {
            List<Vector2Int> tiles = new List<Vector2Int>(size.x * size.y);
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    tiles.Add(new Vector2Int(origin.x + x, origin.y + y));
                }
            }
            return tiles;
        }
        #endregion

        #region Ink Bomb Spell Targeting
        public Vector2Int GetBestInkBombTile(
            List<Vector2Int> blueUnitPositions,
            int tileTargetOffset,
            System.Func<Vector2Int, bool> isObstacle)
        {
            List<Vector2Int> targetCandidates = new List<Vector2Int>();
            if (blueUnitPositions != null && blueUnitPositions.Count > 0)
            {
                targetCandidates.AddRange(blueUnitPositions);
            }

            // Blue팀 건물 타일도 탐색 대상에 포함
            if (GridManager.Instance != null && GridManager.Instance.gridBuildings != null)
            {
                int totalTiles = GridManager.Instance.gridBuildings.Length;
                int width = GridManager.Instance.width;
                for (int i = 0; i < totalTiles; i++)
                {
                    TowerBase building = GridManager.Instance.gridBuildings[i];
                    if (building != null && !building.IsDead && building.TeamColor == TeamColor.Blue)
                    {
                        Vector2Int buildingTile = new Vector2Int(i % width, i / width);
                        if (!targetCandidates.Contains(buildingTile))
                        {
                            targetCandidates.Add(buildingTile);
                        }
                    }
                }
            }

            if (targetCandidates.Count == 0)
            {
                return new Vector2Int(_mapWidth / 2, _mapHeight / 2);
            }

            Vector2Int bestCenter = targetCandidates[0];
            float maxScore = -1f;

            for (int i = 0; i < targetCandidates.Count; i++)
            {
                Vector2Int candidateCenter = targetCandidates[i];
                float score = 0f;

                // 유닛 카운트
                if (blueUnitPositions != null)
                {
                    for (int j = 0; j < blueUnitPositions.Count; j++)
                    {
                        Vector2Int pos = blueUnitPositions[j];
                        if (Mathf.Abs(pos.x - candidateCenter.x) <= 1 && Mathf.Abs(pos.y - candidateCenter.y) <= 1)
                        {
                            score += 1.0f;
                        }
                    }
                }

                // 건물 카운트 (가중치 1.5)
                if (GridManager.Instance != null && GridManager.Instance.gridBuildings != null)
                {
                    int width = GridManager.Instance.width;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int cx = candidateCenter.x + dx;
                            int cy = candidateCenter.y + dy;
                            if (cx >= 0 && cx < _mapWidth && cy >= 0 && cy < _mapHeight)
                            {
                                int idx = cy * width + cx;
                                if (idx >= 0 && idx < GridManager.Instance.gridBuildings.Length)
                                {
                                    TowerBase b = GridManager.Instance.gridBuildings[idx];
                                    if (b != null && !b.IsDead && b.TeamColor == TeamColor.Blue)
                                    {
                                        score += 1.5f;
                                    }
                                }
                            }
                        }
                    }
                }

                if (score > maxScore)
                {
                    maxScore = score;
                    bestCenter = candidateCenter;
                }
            }

            // 오프셋 적용 시 빗나감 방지
            Vector2Int finalTarget = bestCenter;
            if (tileTargetOffset > 0)
            {
                int offsetX = Random.Range(-tileTargetOffset, tileTargetOffset + 1);
                int offsetY = Random.Range(-tileTargetOffset, tileTargetOffset + 1);
                Vector2Int shiftedTarget = new Vector2Int(
                    Mathf.Clamp(bestCenter.x + offsetX, 0, _mapWidth - 1),
                    Mathf.Clamp(bestCenter.y + offsetY, 0, _mapHeight - 1)
                );

                // 오프셋 적용 후에도 3x3 범위 내 유닛/건물이 최소 1개 이상 들어오는지 검증
                bool hasTargetInShifted = false;
                for (int i = 0; i < targetCandidates.Count; i++)
                {
                    Vector2Int pos = targetCandidates[i];
                    if (Mathf.Abs(pos.x - shiftedTarget.x) <= 1 && Mathf.Abs(pos.y - shiftedTarget.y) <= 1)
                    {
                        hasTargetInShifted = true;
                        break;
                    }
                }

                if (hasTargetInShifted)
                {
                    finalTarget = shiftedTarget;
                }
            }

            finalTarget.x = Mathf.Clamp(finalTarget.x, 0, _mapWidth - 1);
            finalTarget.y = Mathf.Clamp(finalTarget.y, 0, _mapHeight - 1);

            if (isObstacle != null && isObstacle(finalTarget))
            {
                for (int i = 0; i < XSpreadOffsets.Length; i++)
                {
                    Vector2Int shiftedTile = new Vector2Int(
                        Mathf.Clamp(finalTarget.x + XSpreadOffsets[i], 0, _mapWidth - 1),
                        finalTarget.y);

                    if (!isObstacle(shiftedTile))
                    {
                        finalTarget = shiftedTile;
                        break;
                    }
                }
            }

            return finalTarget;
        }
        #endregion
    }
}
