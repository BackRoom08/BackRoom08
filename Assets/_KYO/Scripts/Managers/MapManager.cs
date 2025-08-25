using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class MapManager : MonoBehaviour
{
    [Tooltip("플레이어가 죽을 위치")]
    public Transform playerDeadRoomPoint;
    [Tooltip("적이플레이어를 죽일 위치")]
    public Transform enemyDeadRoomPoints;

    public Volume volume; //페이드인,아웃 효과용
    private Vignette vignette;

    // 씬이 바뀌어도 유지되어야 하는 값은 static으로 변경
    public static bool isRestarted = false;

    private AudioSource bgmAudioSource;
    [Header("BGM Settings")]
    public AudioClip sceneBGM; // BGM clip for the current scene

    private void Awake()
    {
        bgmAudioSource = gameObject.AddComponent<AudioSource>();
        bgmAudioSource.loop = true;
        bgmAudioSource.playOnAwake = false;
        bgmAudioSource.volume = 0.2f;

        if (UIManager.Instance != null && UIManager.Instance.bgmGroup != null)
        {
            bgmAudioSource.outputAudioMixerGroup = UIManager.Instance.bgmGroup;
        }
        else
        {
            Debug.LogWarning("UIManager에서 BGM 오디오 믹서 그룹을 찾을 수 없습니다. UIManager가 초기화되었고 bgmGroup이 설정되었는지 확인하세요.");
        }
    }

    void Start()
    {
        if (sceneBGM != null)
        {
            bgmAudioSource.clip = sceneBGM;
            bgmAudioSource.Play();
        }
        else
        {
            Debug.LogWarning($"MapManager의 {gameObject.name}에 BGM 클립이 할당되지 않았습니다.");
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

        vignette.intensity.Override(targetIntensity);
    }

    // static으로 변경하여 어느 스크립트에서든 MapManager.IsRegame() 형태로 호출 가능
    public static bool IsRegame()
    {
        return isRestarted;
    }
    public static void ReGame()
    {
        isRestarted = true;
    }
    public static void NewGame()
    {
        isRestarted = false;
    }
}