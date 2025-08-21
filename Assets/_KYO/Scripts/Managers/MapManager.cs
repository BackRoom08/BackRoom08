using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class MapManager : MonoBehaviour
{

    public static MapManager Instance { get; private set; }

    [Tooltip("플레이어가 죽을 위치")]
    public Transform playerDeadRoomPoint;
    [Tooltip("적이플레이어를 죽일 위치")]
    public Transform enemyDeadRoomPoints;

    private Volume volume; //페이드인,아웃 효과용
    private Vignette vignette;

    private bool isRestarted = false; //게임 리스타트 여부


    private void Awake()
    {
        Debug.Log($"Awake() called on MapManager instance ID: {GetInstanceID()}", gameObject);
        if (Instance == null)
        {            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log($"MapManager singleton instance created: {GetInstanceID()}", gameObject);
        }
        else if (Instance != this)
        {
            Debug.LogWarning($"Duplicate MapManager (ID: {GetInstanceID()}) found. Original is {Instance.GetInstanceID()}. Destroying duplicate.", gameObject);
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        FindAndAssignDeadRoomPoint();
        FindGlobalVolume();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(DelayedSceneSetup());
    }

    private IEnumerator DelayedSceneSetup()
    {
        // 씬의 모든 오브젝트가 초기화되도록 한 프레임 기다립니다.
        yield return new WaitForEndOfFrame();

        FindAndAssignDeadRoomPoint();
        FindGlobalVolume();
    }

    void FindAndAssignDeadRoomPoint() //DeadRoom 찾아서 이동포인트 등록(데드씬용)
    {
        Debug.Log("FindAndAssignDeadRoomPoint: 탐색 시작...");
        GameObject deadRoomObject = GameObject.Find("DeadRoom");
        if (deadRoomObject != null)
        {
            Debug.Log($"FindAndAssignDeadRoomPoint: 'DeadRoom' 오브젝트를 찾았습니다. 이름: {deadRoomObject.name}, 인스턴스 ID: {deadRoomObject.GetInstanceID()}, 활성 상태: {deadRoomObject.activeInHierarchy}");

            Transform playerPointTransform = deadRoomObject.transform.Find("PlayerPoint");
            if (playerPointTransform != null)
            {
                Debug.Log($"FindAndAssignDeadRoomPoint: 'PlayerPoint' 자식을 찾았습니다. 활성 상태: {playerPointTransform.gameObject.activeInHierarchy}");
                playerDeadRoomPoint = playerPointTransform;
            }
            else
            {
                Debug.LogError("FindAndAssignDeadRoomPoint: 'DeadRoom'의 자식 'PlayerPoint'를 찾지 못했습니다. 'DeadRoom'의 자식 오브젝트들을 확인해주세요.");
                // For further debugging, let's list all children
                foreach (Transform child in deadRoomObject.transform)
                {
                    Debug.LogWarning($"'DeadRoom'의 자식: {child.name}, 활성 상태: {child.gameObject.activeSelf}");
                }
            }

            Transform enemyPointTransform = deadRoomObject.transform.Find("EnemyPoint");
            if (enemyPointTransform != null)
            {
                Debug.Log($"FindAndAssignDeadRoomPoint: 'EnemyPoint' 자식을 찾았습니다. 활성 상태: {enemyPointTransform.gameObject.activeInHierarchy}");
                enemyDeadRoomPoints = enemyPointTransform;
            }
            else
            {
                Debug.LogError("FindAndAssignDeadRoomPoint: 'DeadRoom'의 자식 'EnemyPoint'를 찾지 못했습니다.");
            }

            if (playerDeadRoomPoint == null || enemyDeadRoomPoints == null)
            {
                Debug.LogError("DeadRoom포인트 최종 할당 실패.");
            }
            else
            {
                Debug.Log("DeadRoom포인트 최종 할당 성공!");
            }
        }
        else
        {
            Debug.LogWarning("DeadRoom 을 씬에 추가해주세요");
        }
    }
    void FindGlobalVolume() //GlobalVolume 찾아서 컴포넌트등록 (데드씬용)
    {
        GameObject globalvolumeObject = GameObject.Find("Global Volume");
        if (globalvolumeObject != null)
        {
            volume = globalvolumeObject.GetComponent<Volume>();
            if (volume != null && volume.profile.TryGet(out vignette)) { }
            else
            {
                Debug.LogWarning("MapManager: Volume 또는 Volume Profile에 Vignette가 없습니다.");
            }
        }
        else
        {
            Debug.LogWarning("씬에 'Global Volume' 오브젝트가 없습니다.");
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

    public bool IsRegame()
    {
        return isRestarted;
    }
    public void ReGame()
    {
        isRestarted = true;
    }
    public void NewGame()
    {
        isRestarted = false;
    }
}