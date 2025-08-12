using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemUser : MonoBehaviour
{ //빈 게임오브젝트를 인벤토리로 이름 짓고 거기에 붙임
    public PlayerStatus playerStatus;
    public ItemDatas currentItem;
    public PlayerInventory inventory;

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
        if (currentItem is HealItem healItem)
        { //선택된 템이 회복템이면
            healItem.Use(playerStatus); //회복 use함수 호출
            
            inventory.RemoveItem(currentItem); //사용했으면 제거
            currentItem = null;
        }
    }
}

