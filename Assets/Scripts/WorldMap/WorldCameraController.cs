using UnityEngine;

namespace ColorCrash.WorldMap
{
    [RequireComponent(typeof(WorldMapInput))]
    public class WorldCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera cam;
        [SerializeField] private WorldMapManager mapManager;
        private WorldMapInput mapInput;

        [Header("Movement Settings")]
        [SerializeField] private float zoomSpeed = 2f;
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 20f;
        [SerializeField] private float smoothPanTime = 0.2f; // 패닝 도달 시간
        
        [Header("Inertia Settings")]
        [SerializeField] private float dragDamping = 5f;

        private Vector3 currentVelocity;

        private bool isAutoPanning;
        private Vector3 targetPanPosition;
        private Vector3 panVelocity;

        private Vector2 minBounds;
        private Vector2 maxBounds;
        private bool hasBounds = false;

        private void Awake()
        {
            mapInput = GetComponent<WorldMapInput>();
        }

        private void OnEnable()
        {
            if (mapInput != null)
            {
                mapInput.OnInputStarted += HandleInputStarted;
                mapInput.OnDragUpdate += HandleDrag;
                mapInput.OnZoomUpdate += HandleZoom;
            }
        }

        private void OnDisable()
        {
            if (mapInput != null)
            {
                mapInput.OnInputStarted -= HandleInputStarted;
                mapInput.OnDragUpdate -= HandleDrag;
                mapInput.OnZoomUpdate -= HandleZoom;
            }
        }

        private void Start()
        {
            if (cam == null) cam = Camera.main;

            if (WorldMapManager.Instance != null)
            {
                // 초기 바운더리 설정
                RecalculateBounds();

                // 타일 선택 시 카메라 패닝 연동
                WorldMapManager.Instance.OnTileSelected += HandleTileSelected;
                
                // 맵이 재생성될 때마다 바운더리와 카메라 위치 재설정
                WorldMapManager.Instance.OnMapGenerated += HandleMapGenerated;
            }

            // 시작 시 0번 인덱스 타일(원점 0,0,0)이 화면 중앙에 보이도록 카메라 이동
            CenterCameraOnPosition(Vector3.zero);
        }

        private void HandleMapGenerated(Vector3 focusPos)
        {
            RecalculateBounds();
            
            // 맵 생성이 완료되면 전달받은 아군 진영 위치로 카메라 즉시 이동
            CenterCameraOnPosition(focusPos);
            
            // 기존 관성 및 패닝 취소
            isAutoPanning = false;
            currentVelocity = Vector3.zero;
        }

        private void RecalculateBounds()
        {
            if (WorldMapManager.Instance != null)
            {
                Bounds mapBounds = WorldMapManager.Instance.CalculateMapBounds();
                minBounds = new Vector2(mapBounds.min.x, mapBounds.min.z);
                maxBounds = new Vector2(mapBounds.max.x, mapBounds.max.z);
                hasBounds = true;
            }
        }

        /// <summary>
        /// 화면 정중앙이 특정 월드 좌표(Y=0 평면)를 가리키도록 카메라를 이동시킵니다.
        /// </summary>
        public void CenterCameraOnPosition(Vector3 targetWorldPos)
        {
            if (cam == null) return;

            // 화면 정중앙 픽셀 좌표
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            
            // 현재 카메라가 바라보고 있는 화면 중앙의 월드 좌표(Y=0)
            Vector3 currentLookAtPos = GetWorldPositionOnPlane(screenCenter);

            // 타겟과의 차이만큼 카메라 위치를 직접 보정
            Vector3 difference = targetWorldPos - currentLookAtPos;
            transform.position += difference;
        }

        private void HandleTileSelected(int x, int y)
        {
            if (WorldMapManager.Instance != null)
            {
                Vector3 targetWorldPos = WorldMapManager.Instance.GetTileWorldPosition(x, y);
                PanToPosition(targetWorldPos);
            }
        }

        /// <summary>
        /// 특정 월드 좌표를 화면 중앙에 오도록 부드럽게 패닝합니다.
        /// </summary>
        public void PanToPosition(Vector3 targetWorldPos)
        {
            if (cam == null) return;
            
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector3 currentLookAtPos = GetWorldPositionOnPlane(screenCenter);

            Vector3 difference = targetWorldPos - currentLookAtPos;
            targetPanPosition = transform.position + difference;
            
            isAutoPanning = true;
            currentVelocity = Vector3.zero; // 기존 관성 취소
        }

        private void Update()
        {
            if (isAutoPanning)
            {
                ApplyAutoPan();
            }
            else
            {
                ApplyInertia();
            }

            ClampPosition();
        }

        private void ApplyAutoPan()
        {
            if (!isAutoPanning) return;

            transform.position = Vector3.SmoothDamp(transform.position, targetPanPosition, ref panVelocity, smoothPanTime);

            // 도착 판정
            if (Vector3.Distance(transform.position, targetPanPosition) < 0.01f)
            {
                transform.position = targetPanPosition;
                isAutoPanning = false;
            }
        }

        private void HandleInputStarted()
        {
            isAutoPanning = false;
            currentVelocity = Vector3.zero;
        }

        private void HandleDrag(Vector3 difference, Vector3 velocity)
        {
            transform.position += difference;
            currentVelocity = velocity;
        }

        private void HandleZoom(float zoomDelta)
        {
            ApplyZoom(zoomDelta * zoomSpeed);
        }

        private void ApplyInertia()
        {
            if (mapInput != null && !mapInput.IsDragging && currentVelocity.magnitude > 0.01f)
            {
                transform.position += currentVelocity * Time.deltaTime;
                // 속도를 0으로 서서히 줄임 (감속, Damping)
                currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, dragDamping * Time.deltaTime);
            }
        }

        private void ApplyZoom(float zoomAmount)
        {
            if (cam.orthographic)
            {
                cam.orthographicSize -= zoomAmount * 0.2f;
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
            }
            else
            {
                // 원근(Perspective) 카메라의 경우 위치(Z축 또는 Y축 깊이)를 이동
                Vector3 pos = transform.position;
                pos.y -= zoomAmount; // WorldMap이 XZ 평면이므로 Y축이 줌 역할
                pos.y = Mathf.Clamp(pos.y, minZoom, maxZoom);
                transform.position = pos;
            }
        }

        private void ClampPosition()
        {
            if (!hasBounds) return;

            // 현재 카메라가 화면 중앙을 통해 바라보고 있는 월드 좌표 (Look Target) 계산
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector3 currentLookTarget = GetWorldPositionOnPlane(screenCenter);

            // 바라보는 타겟 위치를 맵 경계 내로 제한 (Clamp)
            Vector3 clampedTarget = currentLookTarget;
            clampedTarget.x = Mathf.Clamp(clampedTarget.x, minBounds.x, maxBounds.x);
            clampedTarget.z = Mathf.Clamp(clampedTarget.z, minBounds.y, maxBounds.y);
            
            // 제한된 타겟과 현재 타겟의 차이만큼 카메라 실제 위치를 이동하여 보정
            Vector3 difference = clampedTarget - currentLookTarget;
            if (difference.sqrMagnitude > 0.0001f)
            {
                transform.position += difference;
                currentVelocity = Vector3.zero; // 벽에 부딪히면 관성 제거
            }
        }

        /// <summary>
        /// 화면의 터치/마우스 좌표를 Y=0인 XZ 평면상의 3D 월드 좌표로 변환합니다.
        /// </summary>
        private Vector3 GetWorldPositionOnPlane(Vector2 screenPos)
        {
            if (cam == null) return Vector3.zero;
            
            Ray ray = cam.ScreenPointToRay(screenPos);
            Plane plane = new Plane(Vector3.up, Vector3.zero); // Y=0 평면 (WorldMap 기준)
            
            if (plane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }
            return Vector3.zero;
        }
    }
}
