using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
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

    void Awake()
    {
        GameObject canvas = GameObject.Find("Canvas");
        GameObject suitObject = GameObject.Find("Player");
        if (canvas != null)
        {
            //비밀번호 ui 찾아서 인스펙터 자동 연결
            passwordUI = canvas.GetComponentsInChildren<Transform>(true)
                               .FirstOrDefault(t => t.name == "P_PasswordUI")?.gameObject;
            //e키를 눌러상호작용 문구가 뜨는 텍스트 인스펙터에서 자동연결
            interactText = canvas.GetComponentsInChildren<Text>(true)
                                .FirstOrDefault(t => t.name == "P_PasswordText")?.gameObject;

        }



        if (suitObject != null)
        {
            // 그 자식 중 "Look" 오브젝트를 찾아서 연결
            Transform lookTransform = suitObject.transform.Find("Look");

            if (lookTransform != null)
            {
                cameraControllerObject = lookTransform.gameObject;
            }//시점제어용오브젝트 인스펙터 자동연결
        }
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
        passwordUI.SetActive(false);            //UI비활성화
        Cursor.lockState = CursorLockMode.Locked; //커서 숨기고 잠금
        Cursor.visible = false;

        cameraControllerObject.SetActive(true); // 시점 제어 다시 활성화
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
