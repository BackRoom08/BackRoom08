using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemUser : MonoBehaviour
{ //빈 게임오브젝트를 인벤토리로 이름 짓고 거기에 붙임
    public PlayerStatus playerStatus;
    public ItemDatas currentItem;
    public PlayerInventory inventory;
    public PlayerMove playermove;

    void Update()
    {
        currentItem = inventory.GetSelectedItem();

        if (Input.GetMouseButtonDown(0)) // 마우스 좌클릭
        {
            UseItem();
        }
    }

    public void UseItem()
    {
        if (currentItem == null) return;

        // 정신력 회복 아이템
        if (currentItem is HealItem healItem)
        {
            healItem.Use(playerStatus);
            playermove.anim.SetBool("Drink", true);
            StartCoroutine(UseAndRemoveAfterDelay(1f));

        }
        // 스태미나 회복 아이템
        else if (currentItem is StaminaPotion staminaPotion)
        {
            PlayerMove playerMove = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerMove>();
            staminaPotion.Use(playerMove);
            StartCoroutine(UseAndRemoveAfterDelay(1f));
        }
        else
        {
            Debug.Log("사용할 수 없는 아이템입니다.");
        }
    }


    IEnumerator UseAndRemoveAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        playermove.anim.SetBool("Drink", false);

        if (currentItem != null)
        {
            inventory.RemoveItem(currentItem); // 1초 후 제거
            currentItem = null;
        }
    }

}

