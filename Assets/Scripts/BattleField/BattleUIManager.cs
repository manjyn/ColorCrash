using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ColorCrash.Units;
using TMPro;

public class BattleUIManager : MonoBehaviour
{
    public static BattleUIManager Instance { get; private set; }

    public GameObject UIPanelBattleMenu;
    public TextMeshProUGUI textRemainTime;

    [Header("Spawn Settings")]
    [Tooltip("지면으로 감지할 레이어 마스크")]
    public LayerMask groundLayer;
    
    [Header("UI Feedback")]
    [Tooltip("배치 불가능할 때 마우스 위치에 표시될 십자선/X 아이콘 (UI 캔버스 또는 3D Sprite)")]
    public GameObject invalidPlacementIcon;

    [Header("Overlay Feedback")]
    [Tooltip("배치 영역을 하이라이트할 머티리얼. 비워두면 하이라이트 기능이 동작하지 않습니다.")]
    public Material highlightMaterial;

    [Header("Card List Menu blind")]
    [SerializeField] private GameObject blindCardListObj;
    [SerializeField] private Button cancelSpawnButton;

    private GameObject selectedPrefab;
    private ISpawnHandler currentSpawnHandler;
    private TeamColor spawnTeamColor;
    private int selectedCost = 0;
    private System.Action onSpawnSuccessCallback = null;

    private bool isSpawnModeActive = false;
    private bool isDragging = false;
    private GameObject previewDummy = null;

    // Overlay Quad 관련
    private GameObject highlightQuad;
    private Material quadMaterialInstance;
    private bool wasLastPositionValid = true;

    //객체 삭제 모드 활성
    private bool isRemoveModeActive = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (UIPanelBattleMenu != null) 
            UIPanelBattleMenu.SetActive(false);

        if (invalidPlacementIcon != null)
            invalidPlacementIcon.SetActive(false);

        if (cancelSpawnButton != null)
            cancelSpawnButton.onClick.AddListener(OnCancelSpawnClicked);

        if (blindCardListObj != null)
            blindCardListObj.SetActive(false);
    }

    private void Update()
    {
        if (isSpawnModeActive)
        {
            HandleSpawnInput();
        }
        else if (isRemoveModeActive)
        {
            HandleRemoveInput();
        }
    }

    public void OnUpdateTimer(int remainSec)
    {
        textRemainTime.text = remainSec.ToString();
    }

    public void ToggleRemoveMode(bool isActive)
    {
        isRemoveModeActive = isActive;
        if (isRemoveModeActive)
        {
            CancelSpawnMode();
            Debug.Log("[BattleUIManager] 삭제 모드 활성화");
        }
        else
        {
            Debug.Log("[BattleUIManager] 삭제 모드 비활성화");
        }
    }

    public void SelectObjectToSpawn(GameObject prefab, ISpawnHandler handler, TeamColor teamColor, int cost = 0, System.Action onSpawnSuccess = null)
    {
        isRemoveModeActive = false; // 스폰 모드 진입 시 삭제 모드 해제
        selectedPrefab = prefab;
        currentSpawnHandler = handler;
        spawnTeamColor = teamColor;
        selectedCost = cost;
        onSpawnSuccessCallback = onSpawnSuccess;
        isSpawnModeActive = (selectedPrefab != null && currentSpawnHandler != null);
        Debug.Log($"[BattleUIManager] 생성 모드 활성화: {selectedPrefab?.name}, 비용: {selectedCost}");

        if (isSpawnModeActive && blindCardListObj != null)
            blindCardListObj.SetActive(true);

        // handler가 건물(TowerSpawnHandler)일 때만 스포트라이트를 켬 (InkBomb 등 스펠은 제외)
        if (isSpawnModeActive && handler is TowerSpawnHandler && GridManager.Instance != null && GridManager.Instance.gridRenderer != null)
        {
            GridManager.Instance.gridRenderer.SetBlueBoundaryIntensity(2.5f);
        }
    }

    public void CancelSpawnMode()
    {
        selectedPrefab = null;
        currentSpawnHandler = null;
        onSpawnSuccessCallback = null;
        isSpawnModeActive = false;

        if (GridManager.Instance != null && GridManager.Instance.gridRenderer != null)
        {
            GridManager.Instance.gridRenderer.SetBlueBoundaryIntensity(0.0f);
        }

        if (isDragging)
            EndDrag();

        if (blindCardListObj != null)
            blindCardListObj.SetActive(false);
    }

    private void HandleSpawnInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (mouse.leftButton.wasPressedThisFrame && !pointerOverUI)
        {
            StartDrag();
        }
        else if (mouse.leftButton.isPressed && isDragging)
        {
            UpdateDrag();
        }
        else if (mouse.leftButton.wasReleasedThisFrame && isDragging)
        {
            ReleaseDrag();
        }
    }

    private void HandleRemoveInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (mouse.leftButton.wasPressedThisFrame && !pointerOverUI)
        {
            Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                // 건물 체크
                TowerBase tower = hit.collider.GetComponentInParent<TowerBase>();
                if (tower != null)
                {
                    if (GridManager.Instance != null)
                    {
                        GridManager.Instance.RemoveBuilding(tower.transform.position, tower.SizeX, tower.SizeZ);
                    }
                    Destroy(tower.gameObject);
                    Debug.Log($"[BattleUIManager] 건물 삭제: {tower.name}");
                    return;
                }

                // 유닛 체크
                UnitBase unit = hit.collider.GetComponentInParent<UnitBase>();
                if (unit != null)
                {
                    unit.TakeDamage(99999f); // 강제 즉사 처리
                    Debug.Log($"[BattleUIManager] 유닛 삭제: {unit.name}");
                    return;
                }
            }
        }
    }

    private void StartDrag()
    {
        isDragging = true;

        if (selectedPrefab != null)
        {
            // 더미 생성
            previewDummy = Instantiate(selectedPrefab);
            previewDummy.name = selectedPrefab.name + "_Preview";

            // 스크립트 비활성화
            var scripts = previewDummy.GetComponentsInChildren<MonoBehaviour>();
            foreach (var script in scripts)
            {
                script.enabled = false;
            }

            // 물리 충돌체 비활성화
            var colliders = previewDummy.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }
        }

        // 하이라이트 쿼드 생성
        if (highlightMaterial != null)
        {
            highlightQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            highlightQuad.name = "HighlightOverlay";
            
            // 물리 연산 배제
            Collider quadCol = highlightQuad.GetComponent<Collider>();
            if (quadCol != null) Destroy(quadCol);

            // 바닥에 눕힘
            highlightQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // 크기 설정
            Vector2Int size = currentSpawnHandler.GetOccupiedSize();
            // GridManager.tileSize 사용 (현재 const float로 10.0f)
            float tileSize = 10.0f; 
            highlightQuad.transform.localScale = new Vector3(size.x * tileSize, size.y * tileSize, 1f);

            // 머티리얼 인스턴스화 (색상 변경용)
            quadMaterialInstance = new Material(highlightMaterial);
            MeshRenderer quadRenderer = highlightQuad.GetComponent<MeshRenderer>();
            quadRenderer.material = quadMaterialInstance;
            
            // 초기 상태 업데이트 유도를 위해 강제 변경
            wasLastPositionValid = !wasLastPositionValid; 
        }

        UpdateDrag();
    }

    private void UpdateDrag()
    {
        Mouse mouse = Mouse.current;
        Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
        RaycastHit hit;

        bool hasHit = groundLayer != 0 
            ? Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer) 
            : Physics.Raycast(ray, out hit);

        if (hasHit)
        {
            Vector3 updatedPos = currentSpawnHandler.UpdatePreviewPosition(hit.point, previewDummy);
            bool isValid = currentSpawnHandler.IsValidPosition(updatedPos, spawnTeamColor);

            if (invalidPlacementIcon != null)
            {
                invalidPlacementIcon.SetActive(!isValid);
                if (!isValid)
                {
                    invalidPlacementIcon.transform.position = mouse.position.ReadValue();
                }
            }

            // 하이라이트 쿼드 위치 및 색상 업데이트
            if (highlightQuad != null)
            {
                // Y축을 바닥보다 살짝 높게 설정 (Z-Fighting 회피)
                highlightQuad.transform.position = new Vector3(updatedPos.x, updatedPos.y + 0.05f, updatedPos.z);

                if (isValid != wasLastPositionValid)
                {
                    wasLastPositionValid = isValid;
                    if (quadMaterialInstance != null)
                    {
                        // 배치 가능시 기존 색상 유지(흰색 반투명 등), 배치 불가시 검붉은색 반투명
                        quadMaterialInstance.color = isValid ? highlightMaterial.color : new Color(0.5f, 0f, 0f, 0.5f);
                    }
                }
            }
        }
    }

    private void ReleaseDrag()
    {
        Mouse mouse = Mouse.current;
        Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
        RaycastHit hit;

        bool hasHit = groundLayer != 0 
            ? Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer) 
            : Physics.Raycast(ray, out hit);

        if (hasHit)
        {
            Vector3 finalPos = currentSpawnHandler.UpdatePreviewPosition(hit.point, null);

            if (currentSpawnHandler.IsValidPosition(finalPos, spawnTeamColor))
            {
                currentSpawnHandler.ExecuteSpawn(finalPos, spawnTeamColor);

                if (BattleInkManager.Instance != null && selectedCost > 0)
                {
                    BattleInkManager.Instance.TrySpendInk(spawnTeamColor, selectedCost);
                }

                Debug.Log($"[BattleUIManager] 스폰 완료. 위치: {finalPos}, 차감 잉크: {selectedCost}");
                var callback = onSpawnSuccessCallback;
                CancelSpawnMode();
                callback?.Invoke();
            }
            else
            {
                Debug.LogWarning("[BattleUIManager] 유효하지 않은 위치입니다. 스폰 취소.");
            }
        }

        EndDrag();
    }

    private void EndDrag()
    {
        isDragging = false;
        
        if (previewDummy != null)
        {
            Destroy(previewDummy);
        }
        
        if (invalidPlacementIcon != null)
        {
            invalidPlacementIcon.SetActive(false);
        }

        if (highlightQuad != null)
        {
            Destroy(highlightQuad);
        }

        if (quadMaterialInstance != null)
        {
            Destroy(quadMaterialInstance);
        }
    }

    public void OnPanelBattleMenuShow()
    {
        if (UIPanelBattleMenu != null) UIPanelBattleMenu.SetActive(true);
    }
    public void OnPanelBattleMenuHide()
    {
        if (UIPanelBattleMenu != null) UIPanelBattleMenu.SetActive(false);
    }

    public void OnEndBattleButtonClicked()
    {
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.EndBattle();
        }
    }

    private void OnCancelSpawnClicked()
    {
        CancelSpawnMode();
        Debug.Log($"[BattleUIManager] 스폰 취소.");
    }
}
