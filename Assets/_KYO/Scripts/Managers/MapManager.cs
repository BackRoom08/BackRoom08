using System.Collections; // 코루틴을 위해 추가
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // Vignette를 위해 추가

public class MapManager : MonoBehaviour
{

    public static MapManager Instance { get; private set; }


    [Header("스폰 위치")]
    [Tooltip("플레이어가 스폰될 위치")]
    public Transform playerSpawnPoint;
    public Transform playerDeadRoomPoint;

    [Tooltip("적들이 스폰될 위치 목록")]
    public List<Transform> enemySpawnPoints;
    public Transform enemyDeadRoomPoints;

    [Tooltip("페이드 효과에 사용할 Global Volume")]
    public Volume volume;
    private Vignette vignette;


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
        
        // Volume 프로파일에서 Vignette 컴포넌트를 찾아서 미리 저장해둡니다.
        if (volume != null && volume.profile.TryGet(out vignette))
        {
            // 성공
        }
        else
        {
            Debug.LogWarning("MapManager: Volume 또는 Volume Profile에 Vignette가 없습니다.");
        }
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
    /// 화면을 검게 만듭니다.
    /// </summary>
    /// <param name="duration">페이드 아웃에 걸리는 시간(초)</param>
    public void FadeOut(float duration)
    {
        if (vignette != null)
        {
            StartCoroutine(Co_Fade(1f, duration));
        }
    }

    /// <summary>
    /// 검은 화면에서 다시 밝게 만듭니다.
    /// </summary>
    /// <param name="duration">페이드 인에 걸리는 시간(초)</param>
    public void FadeIn(float duration)
    {
        if (vignette != null)
        {
            StartCoroutine(Co_Fade(0f, duration));
        }
    }

    private IEnumerator Co_Fade(float targetIntensity, float duration)
    {
        float startIntensity = vignette.intensity.value;
        float startSmoothness = vignette.smoothness.value;
        float time = 0;

        while (time < duration)
        {
            time += Time.deltaTime;
            float newIntensity = Mathf.Lerp(startIntensity, targetIntensity, time / duration);
            float newSmoothness = Mathf.Lerp(startSmoothness, targetIntensity, time / duration);

            vignette.intensity.Override(newIntensity);
            vignette.smoothness.Override(newSmoothness);
            yield return null;
        }

        // 정확한 목표값으로 설정 완료
        vignette.intensity.Override(targetIntensity);
    }
}

