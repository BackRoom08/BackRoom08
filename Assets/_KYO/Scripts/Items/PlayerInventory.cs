using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static UnityEditor.Progress;

public class PlayerInventory : MonoBehaviour
{ //빈 게임오브젝트를 인벤토리로 이름 짓고 거기에 붙임
    public Image[] itemSlot = new Image[5]; //5개의 템칸 이미지
    public ItemDatas[] items = new ItemDatas[5]; //슬롯에 들어간 아이템 데이터
    public Color selectedSlot = Color.blue;   // 선택된 칸의 색
    public Color anotherSlot = Color.white; //선택되지않은 다른칸의 색

    private int selectedIndex = -1; //현재 선택된 슬롯의 인덱스 변수. 초기값은 아무것도 없는 상태


        void Start()
    {
     DeselectAllSlot();
    }

    void Update()
    {
      
        for (int i = 0; i < 5; i++)
        {
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) 
            { //숫자키를 누르면 selectslot함수 호출하고 슬롯을 선택.
                selectSlot(i);
              //  Debug.Log($"슬롯{i+1}선택됨");
            }
        }

    }

    void selectSlot(int index)
    { 
        selectedIndex = index;
        for (int i = 0; i < itemSlot.Length; i++)
        {
            itemSlot[i].color = (i == index) ? selectedSlot : anotherSlot;
        }
        //선택된 슬롯 인덱스를 저장하고 선택한 슬롯은 파랑색, 나머지는 흰색으로 
        // 아이템 관련 코드 추가             
    }
    public void AddItem(ItemDatas newItem)
    {
        for (int i = 0; i < items.Length; i++)
        { 
            if (items[i] == null)
            { //아이템슬롯이 비었다면 
                items[i] = newItem; //아이템 추가
                itemSlot[i].sprite = newItem.icon; //스프라이트 추가
                itemSlot[i].color = anotherSlot;
             //   Debug.Log($"슬롯 {i + 1}에 '{newItem.itemName}' 추가");
                return;
            }
        }
    }
    void DeselectAllSlot() 
    {
        foreach (var slot in itemSlot)
        {
            slot.color = anotherSlot;
        }
    }

    public void RemoveItem(ItemDatas item)
    {
        for (int i = 0; i < items.Length; i++)
        { //items배열 순회
            if (items[i] == item)
            { //슬롯에 있는 템이 없앨 아이템과 같다면
                items[i] = null; //아이템데이터제거
                itemSlot[i].sprite = null; //스프라이트제거
                itemSlot[i].color = anotherSlot; //흰색으로 되돌림
              //  Debug.Log($"'{item.itemName}' 아이템 제거됨!");
                return;
            }
        }

    }

    public ItemDatas GetSelectedItem() 
    {
        if (selectedIndex >= 0 && selectedIndex < items.Length)
        { //선택된 슬롯이 있는지 확인하고 0~4에 해당하는지 검사
            return items[selectedIndex]; //선택된 슬롯의 아이템을 가져옴
        }
        return null;
    }
}
