using System.Collections.Generic;
using UnityEngine;

namespace ColorCrash.AI
{
    /// <summary>
    /// 전장 상태(점령 비율, 최전방 전선, 위협도, 인구수, 건물 종류별 수량, Blue팀 조합)를 실시간 평가하고 캐싱하는 컴포넌트입니다.
    /// </summary>
    public class BattlefieldEvaluator : MonoBehaviour
    {
        [Header("전장 규격 및 밸런스 설정")]
        [SerializeField] private int _mapWidth = 14;
        [SerializeField] private int _mapHeight = 24;
        [SerializeField] private int _unitScaleParam = 5; // N 인자 (기본 5)

        [Header("실시간 캐싱 측정 지표")]
        [SerializeField] private int _redTileCount = 0;
        [SerializeField] private int _blueTileCount = 0;
        [SerializeField] private int _neutralTileCount = 0;
        [SerializeField] private int _totalTileCount = 0;
        [SerializeField] private int _frontlineY = 0;
        [SerializeField] private float _territoryRatio = 0f;
        [SerializeField] private float _threatLevel = 0f;
        [SerializeField] private int _currentRedUnitCount = 0;
        [SerializeField] private int _maxPopCap = 15;
        [SerializeField] private int _redStructureCount = 0;

        [Header("Red팀 건물 종류별 세부 설치 지표")]
        [SerializeField] private int _redBarricadeCount = 0;
        [SerializeField] private int _redCannonCount = 0;
        [SerializeField] private int _redMortarCount = 0;

        [Header("Blue팀 유닛 조합 및 진격 라인 스캔 지표")]
        [SerializeField] private int _mostDangerousLineX = 7;
        [SerializeField] private int _blueFootmanCount = 0;
        [SerializeField] private int _blueArcherCount = 0;
        [SerializeField] private int _blueEliteCount = 0;

        // Y축 라인별 Red 팀 도색 타일 카운터 배열 (최전방 Y O(1)~O(Height) 순회 연산용)
        private int[] _redTilesPerY;
        private int[] _blueUnitsPerLineX;

        // Zero GC Alloc용 쿼리 재사용 버퍼
        private List<Vector2Int> _validTileBuffer = new List<Vector2Int>(64);
        private List<GameObject> _unitSearchBuffer = new List<GameObject>(40);

        #region Public Properties
        public int RedTileCount => _redTileCount;
        public int BlueTileCount => _blueTileCount;
        public int NeutralTileCount => _neutralTileCount;
        public int TotalTileCount => _totalTileCount;

        /// <summary>
        /// AI (Red 팀) 타일 점령 비율 (%)
        /// </summary>
        public float TerritoryRatio => _territoryRatio;

        /// <summary>
        /// Red 팀 기준 최전방 전선 Y 좌표 (전장 상단 스폰 존 기준 가장 아래로 전진한 Red 타일 Y)
        /// </summary>
        public int FrontlineY => _frontlineY;

        /// <summary>
        /// 전선 근처(Aggro Range 3칸) 이내의 유저(Blue) 유닛 위협도 수치
        /// </summary>
        public float ThreatLevel => _threatLevel;

        /// <summary>
        /// 필드 내 Red 팀 유닛 수 (UnitManager activeRedUnits 실시간 연동)
        /// </summary>
        public int CurrentRedUnitCount
        {
            get
            {
                if (UnitManager.Instance != null && UnitManager.Instance.activeRedUnits != null)
                {
                    _currentRedUnitCount = UnitManager.Instance.activeRedUnits.Count;
                }
                return _currentRedUnitCount;
            }
        }

        /// <summary>
        /// 최대 인구수 한도 (Max Pop Cap = 10 + N)
        /// </summary>
        public int MaxPopCap => _maxPopCap;

        /// <summary>
        /// 인구수 한도 도달 여부
        /// </summary>
        public bool IsPopCapReached => _currentRedUnitCount >= _maxPopCap;

        /// <summary>
        /// Red 팀 영역 내 설치된 전체 건물 수
        /// </summary>
        public int RedStructureCount => _redStructureCount;

        public int RedBarricadeCount => _redBarricadeCount;
        public int RedCannonCount => _redCannonCount;
        public int RedMortarCount => _redMortarCount;

        /// <summary>
        /// 유저 유닛이 가장 많이 전진해 오고 있는 위험 라인 X 좌표
        /// </summary>
        public int MostDangerousLineX => _mostDangerousLineX;

        public int BlueFootmanCount => _blueFootmanCount;
        public int BlueArcherCount => _blueArcherCount;
        public int BlueEliteCount => _blueEliteCount;
        #endregion

        private void Awake()
        {
            InitializeMapGrid(_mapWidth, _mapHeight, _unitScaleParam);
        }

        public void InitializeMapGrid(int width, int height, int unitScaleN)
        {
            _mapWidth = Mathf.Max(1, width);
            _mapHeight = Mathf.Max(1, height);
            _unitScaleParam = unitScaleN;

            _totalTileCount = _mapWidth * _mapHeight;
            _maxPopCap = 10 + _unitScaleParam;

            _redTilesPerY = new int[_mapHeight];
            _blueUnitsPerLineX = new int[_mapWidth];
            _frontlineY = _mapHeight - 1;

            ResetTileCounts();
        }

        public void ResetTileCounts()
        {
            _redTileCount = 0;
            _blueTileCount = 0;
            _neutralTileCount = _totalTileCount;
            _territoryRatio = 0f;
            _frontlineY = _mapHeight - 1;

            if (_redTilesPerY != null) System.Array.Clear(_redTilesPerY, 0, _redTilesPerY.Length);
            if (_blueUnitsPerLineX != null) System.Array.Clear(_blueUnitsPerLineX, 0, _blueUnitsPerLineX.Length);
        }

        public void OnTilePainted(int x, int y, TeamColor newColor, TeamColor oldColor)
        {
            if (newColor == oldColor) return;

            switch (oldColor)
            {
                case TeamColor.Red:
                    _redTileCount = Mathf.Max(0, _redTileCount - 1);
                    if (y >= 0 && y < _mapHeight)
                    {
                        _redTilesPerY[y] = Mathf.Max(0, _redTilesPerY[y] - 1);
                    }
                    break;
                case TeamColor.Blue:
                    _blueTileCount = Mathf.Max(0, _blueTileCount - 1);
                    break;
                case TeamColor.Neutral:
                    _neutralTileCount = Mathf.Max(0, _neutralTileCount - 1);
                    break;
            }

            switch (newColor)
            {
                case TeamColor.Red:
                    _redTileCount++;
                    if (y >= 0 && y < _mapHeight)
                    {
                        _redTilesPerY[y]++;
                    }
                    break;
                case TeamColor.Blue:
                    _blueTileCount++;
                    break;
                case TeamColor.Neutral:
                    _neutralTileCount++;
                    break;
            }

            RecalculateTerritoryRatio();
            UpdateFrontlineY();
        }

        public void RecalculateTerritoryRatio()
        {
            if (_totalTileCount <= 0)
            {
                _territoryRatio = 0f;
                return;
            }
            _territoryRatio = (_redTileCount / (float)_totalTileCount) * 100f;
        }

        public void UpdateFrontlineY()
        {
            if (_redTileCount <= 0)
            {
                _frontlineY = _mapHeight - 1;
                return;
            }

            for (int y = 0; y < _mapHeight; y++)
            {
                if (_redTilesPerY[y] > 0)
                {
                    _frontlineY = y;
                    return;
                }
            }

            _frontlineY = _mapHeight - 1;
        }

        public void UpdateUnitAndStructureCounts(int redUnitCount, int redStructureCount)
        {
            _currentRedUnitCount = redUnitCount;
            _redStructureCount = redStructureCount;
        }

        /// <summary>
        /// Red팀 건물 종류별 세부 설치 카운트를 외부(BattleManager)에서 실시간 갱신합니다.
        /// </summary>
        public void UpdateRedBuildingDetails(int barricadeCount, int cannonCount, int mortarCount)
        {
            _redBarricadeCount = barricadeCount;
            _redCannonCount = cannonCount;
            _redMortarCount = mortarCount;
            _redStructureCount = barricadeCount + cannonCount + mortarCount;
        }

        public void EvaluateThreatLevel(List<Vector2Int> blueUnitTilePositions)
        {
            if (blueUnitTilePositions == null || blueUnitTilePositions.Count == 0)
            {
                _threatLevel = 0f;
                _mostDangerousLineX = _mapWidth / 2;
                return;
            }

            if (_blueUnitsPerLineX != null) System.Array.Clear(_blueUnitsPerLineX, 0, _blueUnitsPerLineX.Length);

            int threatCount = 0;
            int maxLineCount = 0;
            int dangerX = _mapWidth / 2;
            int frontlineThreshold = _frontlineY + 3;

            for (int i = 0; i < blueUnitTilePositions.Count; i++)
            {
                Vector2Int pos = blueUnitTilePositions[i];
                if (pos.x >= 0 && pos.x < _mapWidth)
                {
                    _blueUnitsPerLineX[pos.x]++;
                    if (_blueUnitsPerLineX[pos.x] > maxLineCount)
                    {
                        maxLineCount = _blueUnitsPerLineX[pos.x];
                        dangerX = pos.x;
                    }
                }

                if (pos.y >= _frontlineY - 3 && pos.y <= frontlineThreshold)
                {
                    threatCount++;
                }
            }

            _threatLevel = threatCount;
            _mostDangerousLineX = dangerX;
        }

        public void UpdateBlueComposition(int footmanCount, int archerCount, int eliteCount)
        {
            _blueFootmanCount = footmanCount;
            _blueArcherCount = archerCount;
            _blueEliteCount = eliteCount;
        }

        public void ClearBuffers()
        {
            _validTileBuffer.Clear();
            _unitSearchBuffer.Clear();
        }
    }
}
