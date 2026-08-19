using TMPro;
using UnityEngine;
using ColorCrash.UI;
using System.Collections.Generic;
using UnityEngine.UI;

namespace ColorCrash.WorldMap
{
    public class WorldMapTile : MonoBehaviour
    {
        // 런타임에 Property ID 파싱 비용을 줄이기 위해 정적 캐싱 (문자열 해싱 최적화)
        // HDRP/URP 환경(_BaseColor) 및 빌트인 환경(_Color) 모두 호환되도록 프로퍼티 캐싱
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        // 모든 타일이 공유하여 사용하는 단일 MaterialPropertyBlock 인스턴스 (GC Alloc 방지)
        private static MaterialPropertyBlock sharedMaterialPropertyBlock;

        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Transform cloudObject;
        [SerializeField] private Transform AttackTileArrow;

        [Header("Tile Colors")]
        [SerializeField] private Color neutralColor = new Color(0.2f, 0.4f, 0.2f, 1f);
        [SerializeField] private Color blueColor = new Color(0.2f, 0.3f, 0.5f, 1f);
        [SerializeField] private Color redColor = new Color(0.5f, 0.2f, 0.2f, 1f);

        [Header("Fog Overlay")]
        [SerializeField] private Color fogColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("Unit")]
        [SerializeField] private Transform unitVisuals;
        [SerializeField] private TextMeshPro unitText;

        [Header("Development")]
        [SerializeField] private Transform developVisuals;
        [SerializeField] private TextMeshPro developText;
        [SerializeField] private Image developImage;

        [Header("Resource")]
        [SerializeField] private Transform resourceVisuals;
        [SerializeField] private TextMeshPro resourceText;

        // 그리드 내 자신의 논리적 좌표 저장 (캐싱)
        private int gridX;
        private int gridY;
        private int unitCount = 0;
        
        // 현재 적용된 기본 색상 캐싱 (안개 처리를 위해)
        private Color currentBaseColor;
        private TileFogState currentFogState = TileFogState.Revealed;
        private bool isHighlighted = false;

        public int GridX => gridX;
        public int GridY => gridY;
        public int UnitCount => unitCount;
        public TileResourceType TileResourceType => tileResourceType;
        public TileDevelopmentType DevelopmentType => developmentType;
        public TileStructureType StructureType => structureType;
        public int BanditLevel => banditLevel;
        public bool IsAlly => WorldMapManager.Instance != null && WorldMapManager.Instance.IsAllyTile(gridX, gridY);
        public bool IsEnemy => WorldMapManager.Instance != null && WorldMapManager.Instance.IsEnemyTile(gridX, gridY);


        private TileResourceType tileResourceType;
        private TileDevelopmentType developmentType = TileDevelopmentType.None;
        private TileStructureType structureType = TileStructureType.None;
        private int banditLevel = 0;

        /// <summary>
        /// 타일 생성 시 최초로 호출되어 좌표와 렌더러를 캐싱합니다.
        /// </summary>
        public void Init(int x, int y)
        {
            gridX = x;
            gridY = y;
            
            // 공유 블록이 없다면 최초 1회 생성
            if (sharedMaterialPropertyBlock == null)
            {
                sharedMaterialPropertyBlock = new MaterialPropertyBlock();
            }

            // 초기 상태는 중립(Neutral)으로 가정
            currentBaseColor = neutralColor;
            developmentType = TileDevelopmentType.None;
            structureType = TileStructureType.None;

            if (resourceVisuals != null)
                resourceVisuals.gameObject.SetActive(false);

            if (developVisuals != null)
                developVisuals.gameObject.SetActive(false);

            if (unitVisuals != null)
                unitVisuals.gameObject.SetActive(false);

            ApplyColor(currentBaseColor);
        }

        /// <summary>
        /// 타일에 자원 지정
        /// </summary>
        public void SetTileResource(TileResourceType type)
        {
            tileResourceType = type;

            switch (tileResourceType)
            {
                case TileResourceType.None:
                    if (resourceText != null)
                        resourceText.text = "";
                    break;
                case TileResourceType.Mountain:
                    if (resourceText != null)
                    {
                        resourceText.text = "Mountain";
                        resourceText.color = new Color(0.8f, 0.8f, 0.8f);
                    }
                    break;
                case TileResourceType.GoldMine:
                    if (resourceText != null)
                    {
                        resourceText.text = "GoldMine";
                        resourceText.color = Color.gold;
                    }
                    break;
                case TileResourceType.Farmland:
                    if (resourceText != null)
                    {
                        resourceText.text = "Farmland";
                        resourceText.color = new Color(0.6f, 0.7f, 1.0f);
                    }
                    break;
                case TileResourceType.Pasture:
                    if (resourceText != null)
                    {
                        resourceText.text = "Pasture";
                        resourceText.color = new Color(0.8f, 1.0f, 0.7f);
                    }
                    break;
            }
        }

        /// <summary>
        /// 자원 개발을 수행합니다. (내부 조건 체크 및 예외처리)
        /// </summary>
        public void DevelopTile(TileDevelopmentType type)
        {
            // 1. 소유권 상태 체크 (아군 진영인지 확인)
            if (!IsAlly)
            {
                NoticePanelUI.Instance?.ShowNotice("You can only develop tiles in your own territory.", NoticeType.Warning);
                return;
            }

            // 2. 중복 개발 방지 검사
            if (developmentType != TileDevelopmentType.None)
            {
                NoticePanelUI.Instance?.ShowNotice("This tile has already been developed.", NoticeType.Warning);
                return;
            }

            // 3. 자원 타입 적합성 검사
            bool isValidResource = false;
            if (type == TileDevelopmentType.Mine && tileResourceType == TileResourceType.GoldMine) isValidResource = true;
            if (type == TileDevelopmentType.Farm && tileResourceType == TileResourceType.Farmland) isValidResource = true;
            if (type == TileDevelopmentType.Ranch && tileResourceType == TileResourceType.Pasture) isValidResource = true;

            if (!isValidResource)
            {
                NoticePanelUI.Instance?.ShowNotice("This development type does not match the resource of this tile.", NoticeType.Warning);
                return;
            }

            // 4. 모든 조건 통과 시 최종 개발 완료 함수 호출
            OnCompleteDevelopment(type);
        }

        /// <summary>
        /// 자원 개발이 최종 성공했을 때 호출되어 실제 데이터 상태 및 비주얼을 갱신합니다.
        /// </summary>
        public void OnCompleteDevelopment(TileDevelopmentType type)
        {
            developmentType = type;

            if (developText != null)
            {
                if (developVisuals != null)
                    developVisuals.gameObject.SetActive(true);

                /*string devName = "";
                switch (developmentType)
                {
                    case TileDevelopmentType.Mine: devName = "Dev Mine"; break;
                    case TileDevelopmentType.Farm: devName = "Dev Farm"; break;
                    case TileDevelopmentType.Ranch: devName = "Dev Ranch"; break;
                }
                developText.text = devName;*/
            }

            UpdateResourceVisuals();

            NoticePanelUI.Instance?.ShowNotice($"Development Completed: {developmentType}", NoticeType.Success);
        }

        public void UpdateResourceVisuals()
        {
            if (developVisuals == null || developImage == null)
                return;

            // 안개에 가려진 타일이면 이미지를 끈다.
            if (currentFogState == TileFogState.Blind)
            {
                developImage.gameObject.SetActive(false);
                return;
            }

            var config = WorldMapManager.Instance?.ResourceVisualConfig;
            if (config == null) return;
            Sprite resourceSprite = config.GetIcon(tileResourceType);

            if (structureType == TileStructureType.MainBase)
            {
                developImage.sprite = WorldMapManager.Instance?.ResourceVisualConfig?.GetMainBase();
                developImage.gameObject.SetActive(true);
                developVisuals.gameObject.SetActive(true);
            }
            else if (structureType == TileStructureType.Bandit)
            {
                Sprite banditSprite = WorldMapManager.Instance?.ResourceVisualConfig?.GetBanditIcon(banditLevel);
                if (banditSprite != null)
                {
                    developImage.sprite = banditSprite;
                    developImage.gameObject.SetActive(true);
                }
                else
                {
                    developImage.gameObject.SetActive(false);
                }

                if (developText != null)
                {
                    developText.text = $"Bandit Lv.{banditLevel}";
                }
                developVisuals.gameObject.SetActive(true);
            }
            // 거대산(Mountain)은 개발 여부 상관없이 이미지가 존재하면 디폴트로 출력
            else if (tileResourceType == TileResourceType.Mountain)
            {
                if (resourceSprite != null)
                {
                    developVisuals.gameObject.SetActive(true);
                    developImage.sprite = resourceSprite;
                    developImage.gameObject.SetActive(true);
                }
                else
                {
                    developImage.gameObject.SetActive(false);
                }
            }
            // 그 외 자원은 개발이 완료(DevelopmentType != None)되었을 때만 이미지 출력
            else
            {
                bool isDeveloped = developmentType != TileDevelopmentType.None;
                if (isDeveloped && resourceSprite != null)
                {
                    developImage.sprite = resourceSprite;
                    developImage.gameObject.SetActive(true);
                }
                else
                {
                    developImage.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 타일의 기본 구조물 타입 설정
        /// </summary>
        public void SetStructure(TileStructureType type)
        {
            structureType = type;
            if (type == TileStructureType.None)
            {
                banditLevel = 0;
            }
        }

        /// <summary>
        /// 타일에 도적단(Bandit)을 배치하고 레벨을 설정합니다.
        /// </summary>
        public void SetBandit(int level)
        {
            structureType = TileStructureType.Bandit;
            banditLevel = level;
            
            if (developText != null)
            {
                developText.text = $"Bandit Lv.{level}";
            }
            UpdateResourceVisuals();
        }

        /// <summary>
        /// 타일에 배치된 유닛 수를 갱신하고 비주얼을 업데이트합니다.
        /// </summary>
        public void UpdateUnitCount(int count)
        {
            unitCount = count;
            if (unitVisuals != null)
            {
                unitVisuals.gameObject.SetActive(count > 0);
            }
            if (unitText != null)
            {
                unitText.text = $"Unit {count}";
            }
        }

        /// <summary>
        /// 아군 타일에 요새(Fortress)를 건설합니다.
        /// </summary>
        public void BuildFortress()
        {
            structureType = TileStructureType.Fortress;

            if (developText != null)
            {
                developText.text = "Fortress";
            }

            if (developVisuals != null)
                developVisuals.gameObject.SetActive(true);

            NoticePanelUI.Instance?.ShowNotice("Fortress construction completed.", NoticeType.Success);
        }

        /// <summary>
        /// 요새를 파괴하고 원래 상태로 되돌립니다.
        /// </summary>
        public void DestroyFortress()
        {
            structureType = TileStructureType.None;

            if (developVisuals != null)
                developVisuals.gameObject.SetActive(developmentType != TileDevelopmentType.None);
            
            if (developText != null)
            {
                if (developmentType != TileDevelopmentType.None)
                {
                    string devName = "";
                    switch (developmentType)
                    {
                        case TileDevelopmentType.Mine: devName = "Dev Mine"; break;
                        case TileDevelopmentType.Farm: devName = "Dev Farm"; break;
                        case TileDevelopmentType.Ranch: devName = "Dev Ranch"; break;
                    }
                    developText.text = devName;
                }
                else
                {
                    developText.text = "";
                }
            }

            NoticePanelUI.Instance?.ShowNotice("Fortress has been destroyed.", NoticeType.Success);
        }

        /// <summary>
        /// 타일의 소유권(Owner)이 변경될 때 호출되어 색상을 업데이트합니다.
        /// </summary>
        public void UpdateOwnerVisual(TileOwnerState state)
        {
            switch (state)
            {
                case TileOwnerState.Neutral:
                    currentBaseColor = neutralColor;
                    break;
                case TileOwnerState.Blue:
                    currentBaseColor = blueColor;
                    break;
                case TileOwnerState.Red:
                    currentBaseColor = redColor;
                    break;
            }

            ApplyColor(currentBaseColor);
        }

        /// <summary>
        /// 타일의 안개 상태(Fog)가 변경될 때 호출되어 시각적으로 반영합니다.
        /// </summary>
        public void UpdateFogVisual(TileFogState state)
        {
            bool isBlind = state == TileFogState.Blind;

            if (cloudObject != null)
                cloudObject.gameObject.SetActive(isBlind);
            
            if (resourceVisuals != null)
                resourceVisuals.gameObject.SetActive(!isBlind && tileResourceType != TileResourceType.None);

            currentFogState = state;
            ApplyColor(currentBaseColor);

            UpdateResourceVisuals();
        }

        /// <summary>
        /// 타일의 소유권이나 안개 상태에 맞는 색상을 렌더러에 적용합니다.
        /// (안개 덮임, 타일 선택 하이라이트 등의 연산을 수행합니다)
        /// </summary>
        private void ApplyColor(Color baseColor)
        {
            if (meshRenderer == null) return;

            Color finalColor = baseColor;
            
            // 안개(Blind) 상태이면 회색조로 덮어씌움
            if (currentFogState == TileFogState.Blind)
            {
                finalColor = fogColor;
            }

            // 타일이 선택된 하이라이트 상태이면 밝기를 증폭
            if (isHighlighted)
            {
                finalColor = new Color(
                    Mathf.Min(finalColor.r * 1.5f, 1f),
                    Mathf.Min(finalColor.g * 1.5f, 1f),
                    Mathf.Min(finalColor.b * 1.5f, 1f),
                    finalColor.a
                );
            }

            // MaterialPropertyBlock을 통해 인스턴싱 호환 색상 변경
            meshRenderer.GetPropertyBlock(sharedMaterialPropertyBlock);
            sharedMaterialPropertyBlock.SetColor(BaseColorPropertyId, finalColor);
            sharedMaterialPropertyBlock.SetColor(ColorPropertyId, finalColor);

            meshRenderer.SetPropertyBlock(sharedMaterialPropertyBlock);
        }

        public void SetHighlight(bool highlight)
        {
            isHighlighted = highlight;
            // 현재 상태의 기본 색상을 다시 적용하여 하이라이트 증폭 계산이 반영되게 함
            ApplyColor(currentBaseColor);
        }        

        /// <summary>
        /// 아군 타일 위의 포위 공격 방향 화살표를 활성화하고, 대상 타일 방향으로 회전시킵니다.
        /// </summary>
        public void ActivateAttackArrow(Vector3 targetWorldPosition)
        {
            if (AttackTileArrow != null)
            {
                Vector3 direction = targetWorldPosition - transform.position;
                direction.y = 0; // 수평 회전만 반영
                
                // 방향 계산이 유효할 때만 회전 적용
                if (direction.sqrMagnitude > 0.001f)
                {
                    AttackTileArrow.localRotation = Quaternion.LookRotation(direction);
                }
                
                AttackTileArrow.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 포위 공격 화살표를 비활성화합니다.
        /// </summary>
        public void DeactivateAttackArrow()
        {
            if (AttackTileArrow != null)
            {
                AttackTileArrow.gameObject.SetActive(false);
            }
        }
    }
}
