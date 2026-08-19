using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모바일 환경에 최적화된 경량 A* 길찾기 알고리즘
/// 가비지 컬렉터(GC) 발생을 최소화하기 위해 노드와 리스트를 캐싱하여 재사용합니다.
/// </summary>
public class Pathfinder : MonoBehaviour
{
    public static Pathfinder Instance { get; private set; }

    /// <summary>
    /// A* 탐색을 위한 개별 타일 노드
    /// </summary>
    public class Node
    {
        public int x;
        public int z;
        public int index;

        public int gCost;
        public int hCost;
        public int fCost => gCost + hCost;
        public Node parent;

        // GC 최적화: 매번 new Node()나 List.Clear() 후 재할당을 피하기 위해
        // 세대(Generation) 값을 이용해 O(1) 지연 초기화를 수행합니다.
        public int searchGeneration;
        public bool isInOpenList;
        public bool isClosed; // ClosedList 컬렉션을 대체하여 O(1) 검사 수행 및 GC 완전 제거
    }

    private Node[] nodes;
    
    // OpenList는 List의 Clear()를 사용하여 메모리 재사용 (최대 크기 할당)
    private List<Node> openList;
    
    // 탐색 세대 (이 값이 노드의 searchGeneration과 다르면 이전 길찾기 데이터가 남아있는 것이므로 초기화)
    private int currentSearchGeneration = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 912개(24x38)의 노드를 게임 시작 시 단 한 번만 생성하여 캐싱
    /// </summary>
    public void InitializeNodes(int width, int height)
    {
        int totalTiles = width * height;
        nodes = new Node[totalTiles];
        openList = new List<Node>(totalTiles); // 맵의 전체 크기만큼 미리 Capacity 할당 (동적 할당 방지)

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = z * width + x;
                nodes[index] = new Node
                {
                    x = x,
                    z = z,
                    index = index
                };
            }
        }
    }

    /// <summary>
    /// 시작 위치에서 목표 위치까지의 경로를 계산합니다. (Y축 무시, X/Z 평면 기준)
    /// 목적지가 완전히 막혀있을 경우, 도달 가능한 가장 가까운 타일(장애물 앞)까지의 임시 경로를 반환합니다.
    /// </summary>
    public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        GridManager grid = GridManager.Instance;
        
        // Y축을 무시하고 평면 좌표계를 기준으로 1차원 인덱스 변환
        int startIndex = grid.WorldToIndex(startPos);
        int targetIndex = grid.WorldToIndex(targetPos);

        // 맵 범위를 벗어난 경우 빈 경로 반환
        if (startIndex == -1 || targetIndex == -1) 
            return new List<Vector3>();

        // 시작과 끝이 같은 타일일 경우 현재 위치 반환
        if (startIndex == targetIndex)
            return new List<Vector3> { grid.IndexToWorld(startIndex) };

        // 새로운 길찾기 시작: 세대 값 증가 및 OpenList 초기화
        currentSearchGeneration++;
        openList.Clear();

        Node startNode = nodes[startIndex];
        Node targetNode = nodes[targetIndex];

        PrepareNode(startNode);
        startNode.gCost = 0;
        startNode.hCost = GetDistance(startNode, targetNode);
        
        openList.Add(startNode);
        startNode.isInOpenList = true;

        Node closestNode = startNode;

        while (openList.Count > 0)
        {
            // 가장 fCost가 낮은 노드 찾기 (fCost가 같다면 hCost가 낮은 노드 우선)
            Node currentNode = openList[0];
            for (int i = 1; i < openList.Count; i++)
            {
                if (openList[i].fCost < currentNode.fCost || 
                   (openList[i].fCost == currentNode.fCost && openList[i].hCost < currentNode.hCost))
                {
                    currentNode = openList[i];
                }
            }

            // 현재 노드를 OpenList에서 제거하고 Closed 처리
            openList.Remove(currentNode);
            currentNode.isInOpenList = false;
            currentNode.isClosed = true;

            // [정상 경로 탐색 완료] 목적지에 도달한 경우
            if (currentNode == targetNode)
            {
                return RetracePath(startNode, targetNode, grid);
            }

            // [길 막힘 예외 처리] H Cost가 가장 낮은(목적지에 가장 가까운) 노드 갱신
            if (currentNode.hCost < closestNode.hCost)
            {
                closestNode = currentNode;
            }

            // 8방향 이웃 타일 탐색
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && z == 0) continue;

                    int checkX = currentNode.x + x;
                    int checkZ = currentNode.z + z;

                    // 맵 경계 체크
                    if (checkX >= 0 && checkX < grid.width && checkZ >= 0 && checkZ < grid.height)
                    {
                        int neighborIndex = checkZ * grid.width + checkX;
                        Node neighbor = nodes[neighborIndex];
                        
                        PrepareNode(neighbor);

                        // 이미 평가가 완료된 노드면 패스
                        if (neighbor.isClosed) continue;

                        // [제약 조건 1] 장애물(건물) 및 Obstacle 타일 체크
                        if (grid.gridBuildings[neighborIndex] != null || grid.tileStates[neighborIndex] == TeamColor.Obstacle)
                        {
                            neighbor.isClosed = true; 
                            continue;
                        }

                        // 대각선 이동 시 코너 막힘 체크 (건물/장애물 사이로 가로지르기 불가)
                        if (Mathf.Abs(x) == 1 && Mathf.Abs(z) == 1)
                        {
                            int cornerIndex1 = currentNode.z * grid.width + checkX;
                            int cornerIndex2 = checkZ * grid.width + currentNode.x;

                            bool corner1Blocked = grid.gridBuildings[cornerIndex1] != null || grid.tileStates[cornerIndex1] == TeamColor.Obstacle;
                            bool corner2Blocked = grid.gridBuildings[cornerIndex2] != null || grid.tileStates[cornerIndex2] == TeamColor.Obstacle;

                            if (corner1Blocked || corner2Blocked)
                            {
                                continue;
                            }
                        }

                        // 이동 비용 계산 (직선은 10, 대각선은 14)
                        int moveCost = (Mathf.Abs(x) == 1 && Mathf.Abs(z) == 1) ? 14 : 10;
                        int newMovementCostToNeighbor = currentNode.gCost + moveCost;

                        if (newMovementCostToNeighbor < neighbor.gCost || !neighbor.isInOpenList)
                        {
                            neighbor.gCost = newMovementCostToNeighbor;
                            neighbor.hCost = GetDistance(neighbor, targetNode);
                            neighbor.parent = currentNode;

                            if (!neighbor.isInOpenList)
                            {
                                openList.Add(neighbor);
                                neighbor.isInOpenList = true;
                            }
                        }
                    }
                }
            }
        }

        // [핵심 기획: 완전 밀봉 예외 처리] 
        // OpenList가 고갈되어 100% 막힌 상태라면 실패(null)시키지 않고
        // 탐색한 노드들 중 목적지와 가장 가까웠던 노드까지의 임시 경로를 반환 (공성 유도)
        if (closestNode != startNode)
        {
            return RetracePath(startNode, closestNode, grid);
        }

        // 시작 타일마저 사방이 완전히 감금된 극단적 상황에는 시작 위치만 반환하여 멈춤 방지
        return new List<Vector3> { grid.IndexToWorld(startNode.index) };
    }

    /// <summary>
    /// 노드를 현재 탐색 세대에 맞게 초기화합니다.
    /// (전체 배열 순회 및 초기화를 피하기 위한 O(1) 지연 초기화 기법)
    /// </summary>
    private void PrepareNode(Node node)
    {
        if (node.searchGeneration != currentSearchGeneration)
        {
            node.searchGeneration = currentSearchGeneration;
            node.gCost = int.MaxValue;
            node.hCost = 0;
            node.parent = null;
            node.isInOpenList = false;
            node.isClosed = false;
        }
    }

    /// <summary>
    /// 두 노드 사이의 휴리스틱 거리(H Cost)를 계산합니다.
    /// </summary>
    private int GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.x - nodeB.x);
        int dstZ = Mathf.Abs(nodeA.z - nodeB.z);

        if (dstX > dstZ)
            return 14 * dstZ + 10 * (dstX - dstZ);
        return 14 * dstX + 10 * (dstZ - dstX);
    }

    /// <summary>
    /// 목표 노드부터 부모 노드를 역추적하여 최종 경로의 월드 좌표 리스트를 생성합니다.
    /// </summary>
    private List<Vector3> RetracePath(Node startNode, Node endNode, GridManager grid)
    {
        List<Vector3> path = new List<Vector3>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(grid.IndexToWorld(currentNode.index));
            currentNode = currentNode.parent;
        }
        
        // 경로가 도착지점부터 시작지점까지 역순으로 담겼으므로 뒤집어줍니다.
        path.Reverse();
        return path;
    }
}
