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

    public Transform HandTransform; //손위치
    private GameObject currentHeldItem; //현재 손에든 아이템
    private int selectedIndex = -1; //현재 선택된 슬롯의 인덱스 변수. 초기값은 아무것도 없는 상태


    void Awake()
    {
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            Transform handPoint = player.transform.Find("root/pelvis/spine_01/spine_02/spine_03/clavicle_l/upperarm_l/lowerarm_l/hand_l/HandPoint");
            if (handPoint != null)
            {
                HandTransform = handPoint;
            }
        }//플레이어 손위치 자동으로 인스펙터에서 연결


            // Canvas 아래의 InventoryArea를 전체 씬에서 찾기
            GameObject inventoryAreaGO = GameObject.Find("InventoryArea");
        if (inventoryAreaGO == null)
        {
            Debug.LogError("InventoryArea 오브젝트를 찾을 수 없습니다.");
            return;
        }

        Transform inventoryArea = inventoryAreaGO.transform;

        // IconBackGround 안의 자식들 중에서 Icon1~Icon5를 찾기
        int foundCount = 0;
        foreach (Transform background in inventoryArea)
        {
            foreach (Transform child in background)
            {
                if (child.name.StartsWith("Icon"))
                {
                    int index;
                    if (int.TryParse(child.name.Substring(4), out index) && index >= 1 && index <= 5)
                    {
                        Image iconImage = child.GetComponent<Image>();
                        if (iconImage != null)
                        {
                            itemSlot[index - 1] = iconImage;
                            foundCount++;
                        }//인벤토리 5칸의 이미지를 찾아서 인스펙터 자동 연결
                    }
                }
            }
        }
    }
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
        ShowHeldItem();
    }

    void ShowHeldItem() 
    {
        // 기존 아이템 제거
        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
        }

        ItemDatas selectedItem = GetSelectedItem();
        if (selectedItem != null && selectedItem.type == ItemType.Consumable && selectedItem.modelPrefab != null)
        { //선택된 아이템이 있고 소모타입이고 프리팹이 있으면
            currentHeldItem = Instantiate(selectedItem.modelPrefab, HandTransform); //복제해서 손에 붙임
            currentHeldItem.transform.localPosition = Vector3.zero; //위치 초기화
            currentHeldItem.transform.localRotation = Quaternion.identity; //회전 초기화

            Collider col = currentHeldItem.GetComponent<Collider>();
            if (col != null)//콜라이더가 있으면 비활성화
            {
                col.enabled = false;
            }

            Rigidbody rb = currentHeldItem.GetComponent<Rigidbody>();
            if (rb != null) //리지드바디가 있다면 중력끄고 키네마틱 온
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }
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
    {//선택되지않은 칸은 색을 되돌림
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

                if (currentHeldItem != null)
                {
                    Destroy(currentHeldItem);
                }
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
