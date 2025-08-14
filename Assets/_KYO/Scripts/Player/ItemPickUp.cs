using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemPickUp : MonoBehaviour
{ //플레이어에게 붙임
    public Camera cam;
    public float pickUpRange = 3f;
    public PlayerInventory inventory;

    void Awake()
    {       
        cam = Camera.main;
        GameObject canvas = GameObject.Find("Canvas");

        if (canvas != null)
        {
            Transform playerUI = canvas.transform.Find("P_PlayerUI");
            if (playerUI != null)
            {
                Transform inventoryTransform = playerUI.Find("P_InventoryController");
                if (inventoryTransform != null)
                {
                    inventory = inventoryTransform.GetComponent<PlayerInventory>();
                }
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            Ray ray = new Ray(cam.transform.position, cam.transform.forward); //ray를 앞으로 쏨
            if (Physics.Raycast(ray, out RaycastHit hit, pickUpRange))
            {//ray를 쏴서 거리내에 뭔가 닿았다면 hit에 값을 저장
                ItemObject item = hit.collider.GetComponent<ItemObject>(); //뭔가 닿은게 itemobject잇는지 검사
                if (item != null)
                {
                    inventory.AddItem(item.data); //인벤토리에 아이템 추가

                    if (item.data.type == ItemType.Flash) //손전등일경우
                    {
                        FlashLight flashLight = this.GetComponent<FlashLight>(); //flash 컴포넌트 가져옴
                        if (flashLight != null)
                        {
                            flashLight.EquipFlashLight(item.data.modelPrefab); //손전등 컴포넌트가 있으면 장착
                        }                
                    }

                    else if (item.data.type == ItemType.Other)                    
                    {
                        Door door = hit.collider.GetComponent<Door>();
                        if (door != null)
                        {
                            print("Other객체 입니당");

                            door.ToggleDoor(); // 문 스크립트의 함수 호출                         
                        }
                    }
                    Destroy(item.gameObject); //주우면 사라짐
                                              // Debug.Log($"{item.data.itemName} 획득");
                }
            }
        }


    }

}
