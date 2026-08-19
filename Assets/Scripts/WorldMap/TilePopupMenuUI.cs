using System;
using UnityEngine;
using UnityEngine.UI;
using ColorCrash.WorldMap;

namespace ColorCrash.UI
{
    public class TilePopupMenuUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private Button reconButton;
        [SerializeField] private Button attackButton;
        [SerializeField] private Button developButton;
        [SerializeField] private Button buildFortressButton;
        [SerializeField] private Button destroyFortressButton;
        [SerializeField] private Button redeployButton;

        [Header("Settings")]
        [SerializeField] private Vector3 offset = new Vector3(0, 80, 0); // 타일 위쪽으로 띄울 오프셋
        [SerializeField] private int reconAPCost = 1;
        //[SerializeField] private int attackAPCost = 3;
        [SerializeField] private int developAPCost = 1;
        [SerializeField] private int developGoldCost = 7;
        [SerializeField] private int buildFortressGoldCost = 15;

        private Camera mainCamera;
        private RectTransform popupRectTransform;
        private Canvas parentCanvas;
        private RectTransform parentCanvasRectTransform;
        
        private int currentTileX = -1;
        private int currentTileY = -1;
        private bool isShowing = false;

        // 다른 시스템(예: 전투 시스템)에서 구독할 이벤트
        public event Action<int, int> OnReconClicked;
        //public event Action<int, int> OnAttackClicked;

        private void Awake()
        {
            mainCamera = Camera.main;
            
            if (popupPanel != null)
            {
                popupRectTransform = popupPanel.GetComponent<RectTransform>();
            }

            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                parentCanvasRectTransform = parentCanvas.GetComponent<RectTransform>();
            }

            if (reconButton != null)
                reconButton.onClick.AddListener(HandleReconClick);
                
            if (attackButton != null)
                attackButton.onClick.AddListener(HandleAttackClick);

            if (developButton != null)
                developButton.onClick.AddListener(HandleDevelopClick);

            if (redeployButton != null)
                redeployButton.onClick.AddListener(HandleRedeployClick);

            if (buildFortressButton != null)
                buildFortressButton.onClick.AddListener(HandleBuildFortressClick);

            if (destroyFortressButton != null)
                destroyFortressButton.onClick.AddListener(HandleDestroyFortressClick);
            
            if (popupPanel != null)
                popupPanel.SetActive(false);
        }

        private void Start()
        {
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnTileSelected += HandleTileSelected;
                WorldMapManager.Instance.OnTileDeselected += HandleTileDeselected;
            }
        }

        private void OnDestroy()
        {
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnTileSelected -= HandleTileSelected;
                WorldMapManager.Instance.OnTileDeselected -= HandleTileDeselected;
            }
        }

        private void HandleTileSelected(int x, int y)
        {
            currentTileX = x;
            currentTileY = y;
            isShowing = true;
            
            if (popupPanel != null)
            {
                popupPanel.SetActive(true);
                UpdatePosition();
            }

            UpdateButtonVisibilities(x, y);
        }

        private void HandleTileDeselected()
        {
            isShowing = false;
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }

            // 모든 버튼 비활성화 초기화
            AllHideButton();

            currentTileX = -1;
            currentTileY = -1;
        }

        private void AllHideButton()
        {
            if (reconButton != null) reconButton.gameObject.SetActive(false);
            if (attackButton != null) attackButton.gameObject.SetActive(false);
            if (developButton != null) developButton.gameObject.SetActive(false);
            if (redeployButton != null) redeployButton.gameObject.SetActive(false);
            if (buildFortressButton != null) buildFortressButton.gameObject.SetActive(false);
            if (destroyFortressButton != null) destroyFortressButton.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (isShowing)
            {
                UpdatePosition();
            }
        }

        private void UpdatePosition()
        {
            if (WorldMapManager.Instance == null || mainCamera == null || popupRectTransform == null || parentCanvasRectTransform == null) 
                return;

            // 선택된 타일의 월드 좌표 가져오기
            Vector3 worldPos = WorldMapManager.Instance.GetTileWorldPosition(currentTileX, currentTileY);
            
            // 월드 좌표를 스크린 좌표로 변환
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            // 카메라 뒤에 타일이 있는 경우 팝업 숨기기
            if (screenPos.z < 0)
            {
                popupPanel.SetActive(false);
                return;
            }
            else
            {
                if (!popupPanel.activeSelf) popupPanel.SetActive(true);
            }

            // Canvas의 렌더 모드에 따라 이벤트를 처리할 카메라 결정
            Camera canvasCam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera;

            // 스크린 좌표를 캔버스의 로컬 좌표로 변환
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvasRectTransform, screenPos, canvasCam, out Vector2 localPoint))
            {
                // 변환된 좌표에 오프셋을 더하여 팝업 위치 갱신
                popupRectTransform.localPosition = localPoint + (Vector2)offset;
            }
        }

        private void HandleReconClick()
        {
            if (WorldMapManager.Instance == null) return;

            // 1. AP 차감 시도
            if (!WorldMapManager.Instance.ConsumeAP(reconAPCost))
            {
                Debug.LogWarning("[TilePopupMenuUI] 정찰을 수행하기 위한 AP가 부족합니다.");
                NoticePanelUI.Instance?.ShowNotice("Insufficient Action Point(AP)!", NoticeType.Warning);
                return;
            }

            OnReconClicked?.Invoke(currentTileX, currentTileY);
            
            WorldMapManager.Instance.RevealSurroundingFog(currentTileX, currentTileY);
            WorldMapManager.Instance.DeselectTile();

            if (WorldUIManager.Instance != null)
                WorldUIManager.Instance.UpdateResourceInfo();

            NoticePanelUI.Instance?.ShowNotice("Reconnaissance Success", NoticeType.Success);
        }

        /// <summary>
        /// 타일의 상태(소유권, 거점 종류, 자원 개발 여부)에 따라 팝업창 버튼들의 활성화 상태를 동적으로 제어합니다.
        /// </summary>
        private void UpdateButtonVisibilities(int x, int y)
        {
            var worldManager = WorldMapManager.Instance;
            if (worldManager == null) return;

            WorldMapTile tile = worldManager.GetTile(x, y);
            if (tile == null) return;

            // 타일 터치 메뉴 상태 초기화
            AllHideButton();

            // 소유권 상태 확인
            bool isBlueTile = worldManager.GetTileOwner(x, y) == TileOwnerState.Blue;
            bool isMainBase = tile.StructureType == TileStructureType.MainBase;
            int unitCount = worldManager.GetTileUnitCount(x, y);

            if (isBlueTile)
            {
                //정찰
                if (!isMainBase && reconButton != null)
                    reconButton.gameObject.SetActive(true);

                //유닛 재배치
                if (unitCount > 0 && redeployButton != null)
                    redeployButton.gameObject.SetActive(true);

                //자원 개발
                if (!isMainBase && developButton != null)
                {
                    // 자원이 있고 아직 개발 완료되지 않았다면 '자원 개발' 버튼 활성
                    bool canDevelop = tile.TileResourceType != TileResourceType.None &&
                                     tile.TileResourceType != TileResourceType.Mountain &&
                                     tile.DevelopmentType == TileDevelopmentType.None;

                    developButton.gameObject.SetActive(canDevelop);
                }

                /*if (tile.StructureType == TileStructureType.Fortress)
                {
                    // 이미 요새가 건설되어 있으면 '요새 파괴' 버튼만 노출
                    if (destroyFortressButton != null)
                        destroyFortressButton.gameObject.SetActive(true);
                }
                else // StructureType == None
                {
                    // 산악 타일이 아니라면 '요새 건설' 버튼 활성화
                    bool canBuildFortress = tile.TileResourceType != TileResourceType.Mountain;
                    if (buildFortressButton != null)
                        buildFortressButton.gameObject.SetActive(canBuildFortress);
                }*/
            }
            else
            {
                if (attackButton != null)
                    attackButton.gameObject.SetActive(true);
            }
        }

        private void HandleAttackClick()
        {
            if (WorldMapManager.Instance == null) return;

            int targetX = currentTileX;
            int targetY = currentTileY;

            WorldMapManager.Instance.DeselectTile();
            WorldMapManager.Instance.EnterAttackPreparationState(targetX, targetY);
        }

        /// <summary>
        /// 자원 개발 버튼 클릭 핸들러
        /// </summary>
        private void HandleDevelopClick()
        {
            var worldManager = WorldMapManager.Instance;
            if (worldManager == null) return;

            // 1. 개발 비용(AP 1, Gold 7) 소모 유효성 검사
            if (worldManager.AP < developAPCost)
            {
                NoticePanelUI.Instance?.ShowNotice("Insufficient Action Point(AP)!", NoticeType.Warning);
                return;
            }
            if (worldManager.Gold < developGoldCost)
            {
                NoticePanelUI.Instance?.ShowNotice("Insufficient Gold!", NoticeType.Warning);
                return;
            }

            // 2. 자원 개발 대상 획득
            WorldMapTile tile = worldManager.GetTile(currentTileX, currentTileY);
            if (tile == null) return;

            // 3. 자원 타일 종류에 따른 개발 형태 결정
            TileDevelopmentType devType = TileDevelopmentType.None;
            switch (tile.TileResourceType)
            {
                case TileResourceType.GoldMine: devType = TileDevelopmentType.Mine; break;
                case TileResourceType.Farmland: devType = TileDevelopmentType.Farm; break;
                case TileResourceType.Pasture: devType = TileDevelopmentType.Ranch; break;
            }

            if (devType == TileDevelopmentType.None)
            {
                NoticePanelUI.Instance?.ShowNotice("This tile cannot be developed.", NoticeType.Warning);
                return;
            }

            // 4. 재화 최종 차감
            worldManager.ConsumeAP(developAPCost);
            worldManager.ConsumeGold(developGoldCost);

            // 5. 개발 수행 (내부에서 검사 후 OnCompleteDevelopment 호출함)
            tile.DevelopTile(devType);

            // 6. UI 상태 업데이트 및 타일 선택 해제
            worldManager.DeselectTile();

            if (WorldUIManager.Instance != null)
                WorldUIManager.Instance.UpdateResourceInfo();
        }

        /// <summary>
        /// 유닛 재배치 버튼 클릭 핸들러
        /// </summary>
        private void HandleRedeployClick()
        {
            var worldManager = WorldMapManager.Instance;
            if (worldManager == null) return;

            // 병력 재배치 진입 AP 유효 체크 (최소 필요량인 기본 AP 소모량 기준)
            int minAPRequired = worldManager.RedeployBaseAPCost;
            if (worldManager.AP < minAPRequired)
            {
                NoticePanelUI.Instance?.ShowNotice("Insufficient Action Point(AP)!", NoticeType.Warning);
                return;
            }

            // 로컬 변수에 현재 좌표 안전하게 백업 (Deselect 시 -1로 초기화되는 문제 방지)
            int targetX = currentTileX;
            int targetY = currentTileY;

            worldManager.DeselectTile();
            worldManager.ActiveRedeployState(true, targetX, targetY);
        }

        /// <summary>
        /// 요새 건설 버튼 클릭 핸들러
        /// </summary>
        private void HandleBuildFortressClick()
        {
            var worldManager = WorldMapManager.Instance;
            if (worldManager == null) return;

            // 1. 골드 비용(15 Gold) 소모 유효성 검사
            if (worldManager.Gold < buildFortressGoldCost)
            {
                NoticePanelUI.Instance?.ShowNotice("Insufficient Gold for Fortress!", NoticeType.Warning);
                return;
            }

            // 2. 타일 획득 및 유효성 최종 검사
            WorldMapTile tile = worldManager.GetTile(currentTileX, currentTileY);
            if (tile == null) return;

            if (tile.StructureType != TileStructureType.None || tile.TileResourceType == TileResourceType.Mountain)
            {
                NoticePanelUI.Instance?.ShowNotice("You cannot build a fortress on this tile.", NoticeType.Warning);
                return;
            }

            // 3. 골드 차감
            worldManager.ConsumeGold(buildFortressGoldCost);

            // 4. 요새 건설 수행
            tile.BuildFortress();

            // 5. UI 해제 및 갱신
            worldManager.DeselectTile();

            if (WorldUIManager.Instance != null)
                WorldUIManager.Instance.UpdateResourceInfo();
        }

        /// <summary>
        /// 요새 파괴 버튼 클릭 핸들러
        /// </summary>
        private void HandleDestroyFortressClick()
        {
            var worldManager = WorldMapManager.Instance;
            if (worldManager == null) return;

            // 1. 타일 획득 및 유효성 검사
            WorldMapTile tile = worldManager.GetTile(currentTileX, currentTileY);
            if (tile == null) return;

            if (tile.StructureType != TileStructureType.Fortress)
            {
                NoticePanelUI.Instance?.ShowNotice("There is no fortress to destroy.", NoticeType.Warning);
                return;
            }

            // 2. 요새 파괴 수행
            tile.DestroyFortress();

            // 3. UI 해제 및 갱신
            worldManager.DeselectTile();

            if (WorldUIManager.Instance != null)
                WorldUIManager.Instance.UpdateResourceInfo();
        }
    }
}
