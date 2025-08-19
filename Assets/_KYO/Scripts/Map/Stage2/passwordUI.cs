using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class PasswordUI : MonoBehaviour
{   //computerPassword 프리팹에 부착
    //passwordUI = computerPassword를 넣어줌
    //cameraController = 플레이어 프리팹의 look 게임 오브젝트

    public string correctPW = "1111"; //비밀번호
    private string currentInput = ""; //입력중인 비밀번호

    public Text[] digitTexts = new Text[4]; // 4개의 숫자칸
    public GameObject passwordUI; //UI
    public GameObject cameraControllerObject;

    void Awake()
    {
     //처음에 시작할때는 다 none으로 뜨지만 컴퓨터오브젝트로 비밀번호UI를 띄우면 자동연결됨
        // 고정할 플레이어의 시점 look 오브젝트 찾아서 인스펙터 자동연결
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("Player 오브젝트를 찾을 수 없습니다.");
            return;
        }

        Transform lookTransform = player.transform.Find("Look");
        if (lookTransform == null)
        {
            Debug.LogError("Player의 자식 오브젝트 중 'Look'을 찾을 수 없습니다.");
            return;
        }
        cameraControllerObject = lookTransform.gameObject;


        // P_PasswordUI 찾아서 인스펙터 자동 연결
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            Debug.LogError("Canvas를 찾을 수 없습니다.");
            return;
        }

        Transform pwTransform = canvas.GetComponentsInChildren<Transform>(true)
                                      .FirstOrDefault(t => t.name == "P_PasswordUI");

        if (pwTransform == null)
        {
            Debug.LogError("Canvas 안에서 P_PasswordUI를 찾을 수 없습니다.");
            return;
        }

        passwordUI = pwTransform.gameObject;

        // 🔹 digitTexts를 찾아서 인스펙터 자동 연결.
        Transform passwordArea = transform.Find("passwordArea");
        if (passwordArea == null)
        {
            Debug.LogError("passwordArea를 찾을 수 없습니다.");
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            string passwordName = $"password({i + 1})";
            Transform passwordTransform = passwordArea.Find(passwordName);
            if (passwordTransform == null)
            {
                Debug.LogError($"{passwordName}을 찾을 수 없습니다.");
                continue;
            }

            string placeholderName = $"Placeholder{i + 1}";
            Transform placeholderTransform = passwordTransform.Find(placeholderName);
            if (placeholderTransform == null)
            {
                Debug.LogError($"{placeholderName}을 찾을 수 없습니다.");
                continue;
            }

            Text textComponent = placeholderTransform.GetComponent<Text>();
            if (textComponent == null)
            {
                Debug.LogError($"{placeholderName}에 Text 컴포넌트가 없습니다.");
                continue;
            }
            digitTexts[i] = textComponent;
        }
    }






    void Update()
    {
        //if (Input.GetKeyDown(KeyCode.E))
        //{
        //    OpenPasswordUI();
        //}

        //if (Input.GetKeyDown(KeyCode.Escape))
        //{
        //    ClosePasswordUI();
        //}
    }

    public void PressNumber(string number)
    { //버튼 누르면 호출됨
        if (currentInput.Length < digitTexts.Length)
        { //최대 4자리 까지입력 (4자리보더 적을때만 추가 가능 )
            currentInput += number; //입력숫자를 currentInput에저장
            Debug.Log($"{number} 누름");
            UpdateDisplay(); //저장된 숫자를 UI에 표시
        }
    }

    public void PressBackspace()
    {
        if (currentInput.Length > 0)
        { //숫자가 1개 이상 입력되었다면
            currentInput = currentInput.Substring(0, currentInput.Length - 1); //마지막 숫자를 제거
            UpdateDisplay();
        }
    }

    public void PressConfirm()
    {
        CheckPassword();
    }

    public void CheckPassword()
    {
        string enteredPW = currentInput; //현재 입렫된 비밀번호를 저장

        if (!string.IsNullOrEmpty(correctPW)) //비밀번호가 비었나 확인
        {
            if (enteredPW == correctPW) //정답비교
            {
                Debug.Log("통과");
                GameManager.Instance.CompleteStage2();
            }
            else
            {
                Debug.Log("오답");
            }
            ClosePasswordUI();
            cameraControllerObject.SetActive(true);
        }
    }

    private void UpdateDisplay()
    {
        for (int i = 0; i < digitTexts.Length; i++) //4칸을 순회하며 하나씩 처리
        {
            digitTexts[i].text = i < currentInput.Length ? currentInput[i].ToString() : "";
            //입력된 숫자는 표시하고, 입력하지 않은 칸은 빈칸으로 표시
        }
    }

    public void OpenPasswordUI()
    {
        passwordUI.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ClosePasswordUI()
    {
        currentInput = ""; //ui가 한번 닫혔으므로 초기화
        UpdateDisplay(); //입력된 숫자도 초기화
        passwordUI.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
