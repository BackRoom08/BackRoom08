using System.Collections; 
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MapManager : MonoBehaviour
{

    public static MapManager Instance { get; private set; }

    [Tooltip("플레이어가 죽을 위치")]
    public Transform playerDeadRoomPoint;
    [Tooltip("적이플레이어를 죽일 위치")]
    public Transform enemyDeadRoomPoints;

    [Tooltip("페이드 효과에 사용할 Global Volume")]
    public Volume volume;
    private Vignette vignette;


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
        // Volume 프로파일에서 Vignette 컴포넌트를 찾아서 미리 저장. (데스씬용)
        if (volume != null && volume.profile.TryGet(out vignette)) {  }
        else
        {
            Debug.LogWarning("MapManager: Volume 또는 Volume Profile에 Vignette가 없습니다.");
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

