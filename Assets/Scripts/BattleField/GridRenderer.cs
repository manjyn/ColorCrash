using ColorCrash.Effects;
using UnityEngine;

/// <summary>
/// 전투맵의 3D Plane에 데이터 텍스처를 생성하여 넘겨주는 렌더러
/// </summary>
public class GridRenderer : MonoBehaviour
{
    [Header("Renderer Settings")]
    [SerializeField] private MeshRenderer planeRenderer;
    [SerializeField] private string texturePropertyName = "_DataTex"; // 머티리얼에서 받을 텍스처 프로퍼티 이름

    [Header("Rendering Performance")]
    [Tooltip("한 프레임에 최소 몇 개의 타일을 시각적으로 업데이트할 것인가")]
    public int minTilesPerFrame = 1;
    [Tooltip("큐가 가득 찼을 때 최대 지연 허용 시간(초) - 이를 넘지 않게 처리 속도 다이나믹 조절")]
    public float maxQueueDelaySeconds = 0.5f;

    private Texture2D dataTexture;
    private Color32[] pixelData;

    // 렌더링 배칭용 큐 시스템
    private System.Collections.Generic.Queue<int> tileRenderQueue = new System.Collections.Generic.Queue<int>();
    private bool[] inQueue;

    private int width;
    private int height;

    // 셰이더 프로퍼티 ID 캐싱
    private static readonly int _GridSizeProperty = Shader.PropertyToID("_GridSize");
    private static readonly int _BlueBoundaryColorProperty = Shader.PropertyToID("_BlueBoundaryColor");
    private static readonly int _BlueBoundaryWidthProperty = Shader.PropertyToID("_BlueBoundaryWidth");
    private static readonly int _BlueBoundaryIntensityProperty = Shader.PropertyToID("_BlueBoundaryIntensity");
    private static readonly int _BlueBoundaryPulseSpeedProperty = Shader.PropertyToID("_BlueBoundaryPulseSpeed");

    /// <summary>
    /// 게임 시작 시 한 번만 호출, GridManager의 데이터와 연동하여 텍스처를 생성
    /// </summary>
    public void InitializeRenderer(int gridWidth, int gridHeight, TeamColor[] initialStates)
    {
        width = gridWidth;
        height = gridHeight;
        
        // 픽셀이 뭉개지지 않도록 Point 필터 사용 및 데이터 왜곡을 막기 위해 Linear(true) 사용
        dataTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        dataTexture.filterMode = FilterMode.Point;
        dataTexture.wrapMode = TextureWrapMode.Clamp; // 텍스처가 타일링/반복되지 않도록 방지

        int totalPixels = width * height;
        pixelData = new Color32[totalPixels];
        inQueue = new bool[totalPixels];
        tileRenderQueue.Clear();

        // 데이터 텍스처 픽셀 초기화
        for (int i = 0; i < totalPixels; i++)
        {
            TeamColor teamColor = (initialStates != null && i < initialStates.Length) ? initialStates[i] : TeamColor.Neutral;
            
            // 0, 1, 2 중 랜덤 무늬 인덱스
            byte randomPattern = (byte)Random.Range(0, 3); 

            // R: 팀 색상, G: 랜덤 무늬, B: 미사용(0), A: 255
            pixelData[i] = new Color32((byte)teamColor, randomPattern, 0, 255);
        }

        // 텍스처에 배열 반영
        dataTexture.SetPixels32(pixelData);
        dataTexture.Apply();

        // MeshRenderer의 머티리얼에 텍스처 할당
        if (planeRenderer != null)
        {
            planeRenderer.material.SetTexture(texturePropertyName, dataTexture);
            planeRenderer.material.SetVector(_GridSizeProperty, new Vector4(width, height, 0, 0));
            planeRenderer.transform.localScale = new Vector3(width, 1, height);
        }
        else
        {
            Debug.LogWarning("GridRenderer: MeshRenderer가 할당되지 않았습니다.");
        }
    }

    /// <summary>
    /// Blue 팀 영역 최외곽 경계선 스포트라이트 밝기 동적 조절
    /// </summary>
    public void SetBlueBoundaryIntensity(float intensity)
    {
        if (planeRenderer != null && planeRenderer.material != null)
        {
            planeRenderer.material.SetFloat(_BlueBoundaryIntensityProperty, intensity);
        }
    }

    /// <summary>
    /// Blue 팀 영역 최외곽 경계선 스포트라이트 색상 동적 조절
    /// </summary>
    public void SetBlueBoundaryColor(Color color)
    {
        if (planeRenderer != null && planeRenderer.material != null)
        {
            planeRenderer.material.SetColor(_BlueBoundaryColorProperty, color);
        }
    }

    /// <summary>
    /// Blue 팀 영역 최외곽 경계선 두께 동적 조절
    /// </summary>
    public void SetBlueBoundaryWidth(float width)
    {
        if (planeRenderer != null && planeRenderer.material != null)
        {
            planeRenderer.material.SetFloat(_BlueBoundaryWidthProperty, width);
        }
    }

    /// <summary>
    /// 특정 타일의 소유권(R 채널)이 변경되었을 때 갱신
    /// </summary>
    public void UpdateTileGraphic(int x, int z, TeamColor teamColor, bool autoApply = true)
    {
        if (x < 0 || x >= width || z < 0 || z >= height) return;

        int index = z * width + x;
        Color32 pixel = pixelData[index];

        // 이미 같은 색상이면 연산 스킵 (최적화)
        if (pixel.r == (byte)teamColor) return;

        pixel.r = (byte)teamColor;
        pixelData[index] = pixel;
        dataTexture.SetPixel(x, z, pixel);

        if (autoApply)
        {
            dataTexture.Apply();
        }
    }

    /// <summary>
    /// 타일의 논리적 색상 변경은 완료되었고, 시각적 업데이트를 큐에 등록
    /// </summary>
    public void QueueTileUpdate(int x, int z)
    {
        if (x < 0 || x >= width || z < 0 || z >= height) return;
        
        int index = z * width + x;
        if (!inQueue[index])
        {
            inQueue[index] = true;
            tileRenderQueue.Enqueue(index);
        }
    }

    private void LateUpdate()
    {
        if (tileRenderQueue.Count == 0) return;

        // 큐 크기와 최대 지연 시간에 맞춰 프레임당 처리 개수 유동적 변경
        int targetTilesPerFrame = Mathf.CeilToInt((tileRenderQueue.Count * Time.deltaTime) / maxQueueDelaySeconds);
        int processCount = Mathf.Max(minTilesPerFrame, targetTilesPerFrame);
        processCount = Mathf.Min(processCount, tileRenderQueue.Count);

        bool needApply = false;

        for (int i = 0; i < processCount; i++)
        {
            int index = tileRenderQueue.Dequeue();
            inQueue[index] = false;

            // 항상 GridManager의 최신 상태를 가져옴 (과거 색상 덮어쓰기 방지)
            TeamColor latestColor = GridManager.Instance.tileStates[index];
            Color32 pixel = pixelData[index];

            if (pixel.r != (byte)latestColor)
            {
                pixel.r = (byte)latestColor;
                pixelData[index] = pixel;
                int x = index % width;
                int z = index / width;
                dataTexture.SetPixel(x, z, pixel);
                needApply = true;

                // 시각적 업데이트가 일어나는 시점에 맞춰 타일 이펙트 재생
                if (EffectManager.Instance != null)
                {
                    Vector3 tileWorldPos = GridManager.Instance.IndexToWorld(index);
                    Color effectColor = (latestColor == TeamColor.Blue) ? Color.blue : Color.red;
                    // Y축을 약간 띄워(예: 0.1f) 파티클이 바닥에 파묻히지 않도록 보정
                    tileWorldPos.y += 0.1f; 
                    EffectManager.Instance.PlayEffect(EffectType.TileChange, tileWorldPos, effectColor);
                }
            }
        }

        // 프레임 마지막에 단 1번만 텍스처 GPU 적용
        if (needApply)
        {
            dataTexture.Apply();
        }
    }

    private void OnDestroy()
    {
        // 동적으로 생성한 텍스처 메모리 해제
        if (dataTexture != null)
        {
            Destroy(dataTexture);
        }

        // 복제된 머티리얼 메모리 해제
        if (planeRenderer != null && planeRenderer.material != null)
        {
            Destroy(planeRenderer.material);
        }
    }
}
