using Highlighters;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Localization.SmartFormat.Core.Parsing;
using UnityEngine.SceneManagement;
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
    private bool isUIInitialized = false;


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
        StartCoroutine(InitializeUI());
    }

    private IEnumerator InitializeUI()
    {
        // 모든 GameObject 중에서 Canvas 찾기
        GameObject canvas = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(go => go.name == "Canvas");

        if (canvas == null)
        {
            yield break;
        }

        // Canvas의 자식 중 P_PasswordUI 찾기 비활성화여도 찾음
        GameObject passwordUIObj = canvas.GetComponentsInChildren<Transform>(true)
            .Select(t => t.gameObject)
            .FirstOrDefault(go => go.name == "P_PasswordUI");

        if (passwordUIObj == null)
        {
            yield break;
        }
        passwordUI = passwordUIObj;

        // P_PasswordUI의 자식 중 passwordArea 찾기 비활성화여도 찾음
        GameObject passwordAreaObj = passwordUIObj.GetComponentsInChildren<Transform>(true)
            .Select(t => t.gameObject)
            .FirstOrDefault(go => go.name == "passwordArea");

        if (passwordAreaObj == null)
        {
            yield break;
        }

        // digitTexts 연결
        bool allDigitsConnected = true;

        for (int i = 0; i < 4; i++) //4개의 패스워드 입력칸을 하나씩 처리함.
        {
            string pwName = $"password({i + 1})";
            string placeholderName = $"Placeholder{i + 1}"; //"password(1)", "Placeholder1" 같은 이름을 동적으로 생성.

            GameObject passwordObj = passwordAreaObj.GetComponentsInChildren<Transform>(true)
                .Select(t => t.gameObject)
                .FirstOrDefault(go => go.name == pwName); //password의 자식들중에서 해당이름을 가진 오브젝트를 찾음

            if (passwordObj == null)
            {
                allDigitsConnected = false;
                continue; //못찾으면 실패로 해두고 다음으로 넘어감
            }

            GameObject placeholderObj = passwordObj.GetComponentsInChildren<Transform>(true)
                .Select(t => t.gameObject)
                .FirstOrDefault(go => go.name == placeholderName); //password의 자식중 placeholder를 찾음

            if (placeholderObj == null)
            {
                allDigitsConnected = false;
                continue; //못찾으면 실패로 해두고 다음으로 넘어감
            }

            Text textComponent = placeholderObj.GetComponent<Text>();
            if (textComponent == null) //Placeholder 오브젝트에서 Text 컴포넌트를 가져옴.

            {
                allDigitsConnected = false;
                continue; //못찾으면 실패로 해두고 다음으로 넘어감
            }

            digitTexts[i] = textComponent;
        }

        if (!allDigitsConnected)
        { //4개중 1개라도 연결 실패면 코루틴 종료
            yield break;
        }

        // Player 오브젝트 찾기
        yield return new WaitUntil(() => GameObject.Find("Player") != null);
        //GameObject playerObj = GameObject.Find("Player");

        //Transform lookTransform = playerObj.transform.Find("Look");
        //if (lookTransform != null)
        //{
        //    Transform virtualCamTransform = lookTransform.Find("VirtualCamera");
        //    if (virtualCamTransform != null)
        //    {
        //        cameraControllerObject = virtualCamTransform.gameObject;
        //        Debug.Log(" VirtualCamera 연결 완료");
        //    }
        //}

        isUIInitialized = true;

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
                cameraControllerObject.SetActive(true);
                passwordUI.SetActive(false);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                // 비활성화된 오브젝트까지 포함해서 전체 Transform에서 찾기
                Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
                Transform found = allTransforms.FirstOrDefault(t => t.name == "NextMapTrigger");

                if (found != null)
                {
                    found.gameObject.SetActive(true);
                }
            }
            else
            {
                Debug.Log("오답");
            }
            //ClosePasswordUI();
        }
    }

    private void UpdateDisplay()
    {
        for (int i = 0; i < digitTexts.Length; i++)
        {
            if (digitTexts[i] == null)
            {
                throw new System.Exception($"digitTexts[{i}]가 null입니다. UI 연결 실패.");
            }

            digitTexts[i].text = i < currentInput.Length ? currentInput[i].ToString() : "";
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
        passwordUI.SetActive(false);
        cameraControllerObject.SetActive(true);

        //  마우스 커서 숨기고 잠금
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        //  시점 제어 스크립트 다시 활성화
        var lookScript = cameraControllerObject.GetComponent<NewBehaviourScript>();
        if (lookScript != null)
            lookScript.enabled = true;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

    }
}