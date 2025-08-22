using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PasswordClearMap : MonoBehaviour
{//passwordPC 프리팹에 부착
 //레이어 = interactible
 //cameracontroller = 플레이어 프리팹의 Look 게임오브젝트
 //iteract Text = textUSE 연결하면 됩니다

    public float interactDistance = 3f; //상호작용 가능거리
    public LayerMask interactLayer; //어떤 레이어와 상호작용 할건지 결정
    public GameObject passwordUI; //비밀번호UI 

    public GameObject cameraControllerObject; //UI가 떴을때의 시점제어용

    public GameObject interactText; // 안내 텍스트
    private bool isOnMouse = false; //마우스가 올라왔나 확인
    private bool isEkeydown = false;

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
        StartCoroutine(InitializeReferences());
    }

    IEnumerator InitializeReferences()
    {
        // Canvas 준비 대기
        yield return new WaitUntil(() => GameObject.Find("Canvas") != null);
        GameObject canvas = GameObject.Find("Canvas");

        passwordUI = canvas.GetComponentsInChildren<Transform>(true)
                           .FirstOrDefault(t => t.name == "P_PasswordUI")?.gameObject;

        interactText = canvas.GetComponentsInChildren<Text>(true)
                             .FirstOrDefault(t => t.name == "P_PasswordText")?.gameObject;

        // Player 준비 대기
        yield return new WaitUntil(() => GameObject.Find("Player") != null);
        GameObject suitObject = GameObject.Find("Player");

        Transform lookTransform = suitObject.transform.Find("Look");
        if (lookTransform != null)
            cameraControllerObject = lookTransform.gameObject;
    }

    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit; //레이를 쏘고 정보를 변수에 담음

        if (Physics.Raycast(ray, out hit, interactDistance, interactLayer))
        { //오브젝트에 닿았나 확인
            if (hit.collider.CompareTag("computerPassword"))
            { //태그 확인
                if (!isOnMouse && !isEkeydown)
                {//마우스가 오브젝트에 올라왔으면
                    interactText.SetActive(true);
                    isOnMouse = true;
                }//안내텍스트 보여줌

                if (Input.GetKeyDown(KeyCode.E))
                {
                    isEkeydown = true;
                    cameraControllerObject.SetActive(false);
                    passwordUI.SetActive(true);
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    //UI를 띄우고 시점을 고정하고 커서를 보이게 함

                    var lookScript = cameraControllerObject.GetComponent<NewBehaviourScript>();
                    if (lookScript != null)
                        lookScript.enabled = false;

                    HideText();
                }
            }
            else
            {
                HideText();
            }
        }
        else
        {
            HideText();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ClosePasswordUI();
        }
    }

    public void ClosePasswordUI()
    {
        passwordUI.SetActive(false);
        cameraControllerObject.SetActive(true);

        //  마우스 커서 숨기고 잠금
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        //  시점 제어 스크립트 다시 활성화
        var lookScript = cameraControllerObject.GetComponent<NewBehaviourScript>();
        if (lookScript != null)
            lookScript.enabled = true;

        isEkeydown = false;

    }

    void HideText()
    {
        if (isOnMouse)
        {
            interactText.SetActive(false);
            isOnMouse = false;
        }
    }
}