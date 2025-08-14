using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStatus : MonoBehaviour
{ //플레이어에게 붙임
    public int MentalHP = 100; //정신력
    public float MentalInterval = 2f; //정신력감소간격
    public int MentalDamage = 1; //감소데미지

    private bool isDead = false;
    private int currentMentalHP;
    public Animator anim;

    void Start()
    {
        //anim = GetComponent<Animator>();
        currentMentalHP = MentalHP;
        StartCoroutine(DecreaseMentalHP()); //코루틴 시작
    }

    public void HealMentalHP(int amount)
    {
        if (isDead) return; //죽었으면 함수종료

        currentMentalHP += amount; //정신력 회복
        currentMentalHP = Mathf.Min(currentMentalHP, MentalHP); //정신력 최대치 제한   
    }
    IEnumerator DecreaseMentalHP()
    {
        while (!isDead) //안죽었으면
        {
            yield return new WaitForSeconds(MentalInterval);

            currentMentalHP -= MentalDamage; //코루틴 시간마다 정신력 감소
            currentMentalHP = Mathf.Max(currentMentalHP, 0); //최소값 0으로 제한
            //Debug.Log("현재 정신력 : " + currentMentalHP);

            if (currentMentalHP <= 0)
            {
                Die(); //사망
            }

            void Die()
            {
                isDead = true;
                Debug.Log("사망");
                anim.SetTrigger("Dead");

                PlayerMove moveScript = GetComponent<PlayerMove>();
                if (moveScript != null)
                {
                    moveScript.enabled = false;     //비활성화   
                }
                StartCoroutine(RemoveAfterDelay(3f)); //3초뒤 파괴
            }


        }
    }

    IEnumerator RemoveAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }
}
