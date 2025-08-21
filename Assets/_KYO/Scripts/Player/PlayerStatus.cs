using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStatus : MonoBehaviour
{ //플레이어에게 붙임
    public int MentalHP = 100; //정신력
    public float MentalInterval = 2f; //정신력감소간격
    public int MentalDamage = 1; //감소데미지

    private bool isDead = false;
    private int currentMentalHP;
    public Animator anim;
    public Text MentalText;
    private bool isMentalDamageActive = false; // 정신력 감소 활성화 여부
    private Transform playerDeadRoomPoint; //죽는 장소

    void Awake()
    {
        GameObject textObj = GameObject.Find("MentalViewUI");
        if (textObj == null)
        {
            return;
        }
        MentalText = textObj.GetComponent<Text>();

        // 데드룸 위치 설정
        playerDeadRoomPoint = MapManager.Instance.playerDeadRoomPoint;

    }

    void Start()
    {
        //anim = GetComponent<Animator>();
        currentMentalHP = MentalHP;
        StartCoroutine(DecreaseMentalHP()); //코루틴 시작
    }

    void MentalUI()
    {
        if (MentalText != null)
        {
            MentalText.text = $"정신력 : {currentMentalHP}";
        }
    }

    public void HealMentalHP(int amount)
    {
        if (isDead) return; //죽었으면 함수종료

        currentMentalHP += amount; //정신력 회복
        currentMentalHP = Mathf.Min(currentMentalHP, MentalHP); //정신력 최대치 제한
        MentalUI();
    }
    IEnumerator DecreaseMentalHP()
    {
        while (!isDead) //안죽었으면
        {
            yield return new WaitForSeconds(MentalInterval);
            if (isMentalDamageActive) // 조건이 true일 때만 감소
            {
                currentMentalHP -= MentalDamage; //코루틴 시간마다 정신력 감소
                currentMentalHP = Mathf.Max(currentMentalHP, 0); //최소값 0으로 제한
                Debug.Log("현재 정신력 : " + currentMentalHP);
                MentalUI();

                if (currentMentalHP <= 0)
                {
                    Debug.Log("사망씬 넣어주세요");
                    StartCoroutine(HandleMentalDeath());
                }
            }
        }
    }
    public void SetMentalDamageActive(bool isActive)
    {
        isMentalDamageActive = isActive;
    }
    private IEnumerator HandleMentalDeath()
    {
        isDead = true;

        // 페이드 아웃
        MapManager.Instance.FadeOut(0.5f);
        yield return new WaitForSeconds(0.5f);

        // 플레이어 조작 비활성화
        GetComponent<PlayerMove>().enabled = false;
        GetComponent<ItemPickUp>().enabled = false;

        var cameraScript = GetComponentInChildren<NewBehaviourScript>();
        if (cameraScript != null) cameraScript.enabled = false;

        // 데드룸 위치로 이동
        transform.position = MapManager.Instance.playerDeadRoomPoint.position;

        // 페이드 인
        MapManager.Instance.FadeIn(1.5f);

        // 사망 UI 표시
        UIManager.Instance.ShowDeathUI();

        Debug.Log("정신력 0으로 데드룸 이동 및 게임 오버 처리");
    }

}
