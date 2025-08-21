using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FlashLight : MonoBehaviour
{ //플레이어에게 붙임
    public Transform flashLightHolPoint; //손전등을 들 위치
    public GameObject equippedFlashLight; //장착할 손전등을 저장하는 변수
    private bool isFlashLightOn = false; //손전등 on off 여부

    // 흔들림 관련 변수
    public float bobAmount = 0.02f; // 흔들림 크기
    public float bobSpeed = 6f;     // 흔들림 속도
    private Vector3 initialLocalPos;
    private float bobTimer = 0f;
    private Light flashlightLight; // Light 컴포넌트 참조
    private Vector3 initialLightLocalPos;

    // 기절 관련 변수
    public float flashRange = 15f; // 후레쉬 거리
    public LayerMask enemyLayer;   // 적 레이어

    //후레쉬 소리
    public AudioClip flashSound;
    private AudioSource audioSource;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(InitializeFlashPoint());
    }

    IEnumerator InitializeFlashPoint()
    {
        // Player 오브젝트가 준비될 때까지 대기
        yield return new WaitUntil(() => GameObject.Find("Player") != null);

        GameObject player = GameObject.Find("Player");
        Transform lookTransform = player.transform.Find("Look");
        if (lookTransform == null)
            yield break;

        Transform flashPoint = lookTransform.Find("FlashPoint");
        if (flashPoint == null)
            yield break;

        flashLightHolPoint = flashPoint;
    }

    public void EquipFlashLight(GameObject flashlightPrefab)
    {
        if (equippedFlashLight != null)
        { //이미 손전등이 있다면
            Destroy(equippedFlashLight); // 기존 손전등 제거
        }

        equippedFlashLight = Instantiate(flashlightPrefab, flashLightHolPoint);
        equippedFlashLight.transform.localPosition = Vector3.zero;
        equippedFlashLight.transform.localRotation = Quaternion.identity;
        //생성 , 및 위치, 회전 저장
        flashlightLight = equippedFlashLight.GetComponentInChildren<Light>(); //light 컴포넌트 찾아서 저장
        if (flashlightLight != null)
        {
            initialLightLocalPos = flashlightLight.transform.localPosition; //흔들림효과를 위한 위치 저장
            flashlightLight.enabled = false; //꺼진상태로 시작
        }

        SetFlashlight(false); //손전등을 꺼진상태로 설정
    }


    void Update()
    {
        if (equippedFlashLight != null && Input.GetKeyDown(KeyCode.F))
        {//손전등이 장착되어 있고 f키를 눌렀으면
            isFlashLightOn = !isFlashLightOn;
            SetFlashlight(isFlashLightOn); //키거나 끄기
        }
        ApplyLightBobEffect(); // 빛 흔들림 적용

        if (isFlashLightOn) //손전등이 켜져있다면
        {
            CheckEnemyInLight(); // 적 감지
        }

    }

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void ApplyLightBobEffect()
    { //빛의 흔들림 효과를 적용하는 함수
        if (flashlightLight == null) return;
        //light없으면 함수 종료
        float moveX = Input.GetAxis("Horizontal");
        float moveY = Input.GetAxis("Vertical");
        bool isMoving = Mathf.Abs(moveX) > 0.1f || Mathf.Abs(moveY) > 0.1f;
        //플레이어가 움직이는지 확인하고 움직이고 있다면 true
        if (isMoving)
        {//bobtimer는 시간에 따라 증가해서 sin함수에 넣을 값이 됨
            bobTimer += Time.deltaTime * bobSpeed;
            float bobOffset = Mathf.Sin(bobTimer) * bobAmount; //위아래로 흔들리는 효과
            flashlightLight.transform.localPosition = initialLightLocalPos + new Vector3(0f, bobOffset, 0f); //y축만 적용
        }
        else
        { //움직이지 않을때는 lerp를 사용해 원래위치로 부드럽게 되돌림
            flashlightLight.transform.localPosition = Vector3.Lerp(flashlightLight.transform.localPosition, initialLightLocalPos, Time.deltaTime * bobSpeed);
            bobTimer = 0f; //0으로 초기화해서 다음 움직임때 흔들림이 처음부터 시작되게함
        }
    }

    private void SetFlashlight(bool state)
    {
    Light light = equippedFlashLight.GetComponentInChildren<Light>();
    //손전등 오브젝트에서 light 찾고
        if (light != null) 
        {// 있으면 값에따라 온오프
            light.enabled = state;
            // 🔊 소리 재생
            if (audioSource != null)
            {
                if (state && flashSound != null)
                {
                    audioSource.PlayOneShot(flashSound);
                }
                else if (!state && flashSound != null)
                {
                    audioSource.PlayOneShot(flashSound);
                }
            }

        }
    }
    private void CheckEnemyInLight()
    { //레이를 전방으로 쏨
        Ray ray = new Ray(equippedFlashLight.transform.position, equippedFlashLight.transform.forward);
        Debug.DrawRay(ray.origin, ray.direction * flashRange, Color.red);
        if (Physics.Raycast(ray, out RaycastHit hit, flashRange, enemyLayer))
        { //전방으로 쏜 레이 안에 몹을 탐색
            EnemyEyeLightMob enemy = hit.collider.GetComponent<EnemyEyeLightMob>(); 
            //  탐색된 몹이 eyelight스크립트를 가지고있을경우에 기절시킴         
            if (enemy != null)
            {
                enemy.OnFlashHit(); // 적 기절 처리
                Debug.Log("으악 내눈!");
            }
        }
    }
}

