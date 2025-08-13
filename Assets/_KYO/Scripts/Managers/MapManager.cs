using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 각 맵의 주요 설정을 관리하는 매니저 클래스입니다.
/// </summary>
public class MapManager : MonoBehaviour
{

    public static MapManager Instance { get; private set; }


    [Header("스폰 위치")]
    [Tooltip("플레이어가 스폰될 위치")]
    public Transform playerSpawnPoint;

    [Tooltip("적들이 스폰될 위치 목록")]
    public List<Transform> enemySpawnPoints;

    [Header("환경광 및 안개")]
    [Tooltip("환경광의 색상")]
    [ColorUsage(true, true)]
    public Color environmentLightColor = Color.white;

    [Tooltip("안개 효과 사용 여부")]
    public bool useFog = true;

    [Tooltip("안개 색상")]
    public Color fogColor = Color.gray;

    [Tooltip("안개 농도")]
    public float fogDensity = 0.01f;

    [Header("맵 이벤트")]
    [Tooltip("맵 시작 시 호출될 이벤트")]
    public UnityEvent onMapStart;

    [Header("플레이어 제어 이벤트")]
    [Tooltip("컷씬 시작 등 플레이어 조작을 비활성화할 때 호출할 이벤트")]
    public UnityEvent onPlayerControlDisable;

    [Tooltip("컷씬 종료 등 플레이어 조작을 다시 활성화할 때 호출할 이벤트")]
    public UnityEvent onPlayerControlEnable;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(this); 
            
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        ApplyMapSettings();
        onMapStart?.Invoke();
    }

    /// <summary>
    /// 인스펙터에 설정된 값들을 실제 씬에 적용합니다.
    /// </summary>
    public void ApplyMapSettings()
    {
        // 환경광 설정 적용
        RenderSettings.ambientLight = environmentLightColor;

        // 안개 설정 적용
        RenderSettings.fog = useFog;
        if (useFog)
        {
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
        }
    }

    /// <summary>
    /// 지정된 ID의 문을 여는 이벤트를 처리합니다.
    /// </summary>
    /// <param name="doorId">문의 고유 ID</param>
    public void OpenDoor(int doorId)
    {
        Debug.Log($"MapManager: {doorId}번 문을 엽니다.");
        // 여기에 실제 문을 여는 로직을 구현합니다.
        // 예: 애니메이션 실행, 콜라이더 비활성화 등
    }
}

