using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using UnityEngine.EventSystems;

namespace ColorCrash.WorldMap
{
    public class WorldMapInput : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera cam;

        [Header("Input Settings")]
        [SerializeField] private float clickThreshold = 30f;

        public event Action OnInputStarted;
        public event Action<Vector3, Vector3> OnDragUpdate;
        public event Action<float> OnZoomUpdate;

        private Vector3 dragOrigin;
        private Vector2 pressScreenPosition;
        private bool isDragging;

        public bool IsDragging => isDragging;

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }

        private void Start()
        {
            if (cam == null) cam = Camera.main;
        }

        private void Update()
        {
            HandlePointerInput();
            HandleZoom();
        }

        private void HandlePointerInput()
        {
            if (Pointer.current == null) return;

            // 아군 턴이 아니면 모든 월드 인터랙션 차단
            if (WorldMapManager.Instance != null && WorldMapManager.Instance.CurrentTurnState != TurnState.PlayerTurn)
            {
                isDragging = false;
                return;
            }

            if (Pointer.current.press.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                Vector2 screenPos = Pointer.current.position.ReadValue();
                dragOrigin = GetWorldPositionOnPlane(screenPos);
                pressScreenPosition = screenPos;
                isDragging = true;
                
                OnInputStarted?.Invoke();
            }

            if (Pointer.current.press.isPressed && isDragging)
            {
                Vector2 screenPos = Pointer.current.position.ReadValue();
                Vector3 currentPos = GetWorldPositionOnPlane(screenPos);
                Vector3 difference = dragOrigin - currentPos;

                if (difference.magnitude > 0.001f)
                {
                    Vector3 velocity = difference / Time.deltaTime;
                    OnDragUpdate?.Invoke(difference, velocity);
                    
                    // Update origin after camera movement to calculate delta properly next frame
                    dragOrigin = GetWorldPositionOnPlane(screenPos);
                }
            }

            if (Pointer.current.press.wasReleasedThisFrame)
            {
                if (isDragging)
                {
                    Vector2 releaseScreenPosition = Pointer.current.position.ReadValue();
                    if (Vector2.Distance(pressScreenPosition, releaseScreenPosition) <= clickThreshold)
                    {
                        HandleClick(releaseScreenPosition);
                    }
                }
                isDragging = false;
            }
        }

        private void HandleClick(Vector2 screenPos)
        {
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(screenPos);
            
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {                
                // 공격 화살표가 클릭되었는지 먼저 감지
                AttackArrow arrow = hit.collider.GetComponent<AttackArrow>();
                if (arrow == null)
                {
                    arrow = hit.collider.GetComponentInParent<AttackArrow>();
                }
                if (arrow != null && WorldMapManager.Instance != null)
                {
                    if (Keyboard.current != null)
                    {
                        if (Keyboard.current.shiftKey.isPressed)
                        {
                            arrow.OnArrowClickVictory(); // todo manjyn
                            return;
                        }
                        if (Keyboard.current.ctrlKey.isPressed)
                        {
                            arrow.OnArrowClickDefeat(); // todo manjyn
                            return;
                        }
                    }

                    arrow.OnArrowClicked();
                    return;
                }

                WorldMapTile clickedTile = hit.collider.GetComponentInParent<WorldMapTile>();
                if (clickedTile != null && WorldMapManager.Instance != null)
                {
                    WorldMapManager.Instance.CancelAttackPreparationState();
                    WorldMapManager.Instance.SelectTile(clickedTile.GridX, clickedTile.GridY);
                    return; 
                }
            }
            
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.CancelAttackPreparationState();
                WorldMapManager.Instance.DeselectTile();
            }
        }

        private void HandleZoom()
        {
            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    OnZoomUpdate?.Invoke(scroll * 0.01f);
                }
            }

            if (Touch.activeTouches.Count == 2)
            {
                var touchZero = Touch.activeTouches[0];
                var touchOne = Touch.activeTouches[1];

                Vector2 touchZeroPrevPos = touchZero.screenPosition - touchZero.delta;
                Vector2 touchOnePrevPos = touchOne.screenPosition - touchOne.delta;

                float prevMagnitude = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                float currentMagnitude = (touchZero.screenPosition - touchOne.screenPosition).magnitude;

                float difference = currentMagnitude - prevMagnitude;

                OnZoomUpdate?.Invoke(difference * 0.05f);
            }
        }

        private Vector3 GetWorldPositionOnPlane(Vector2 screenPos)
        {
            if (cam == null) return Vector3.zero;
            
            Ray ray = cam.ScreenPointToRay(screenPos);
            Plane plane = new Plane(Vector3.up, Vector3.zero); 
            
            if (plane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }
            return Vector3.zero;
        }
    }
}
