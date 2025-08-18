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

        GameObject inventoryGO = GameObject.Find("Canvas/P_PlayerUI/P_InventoryController");
        if (inventoryGO != null)
        {
            inventory = inventoryGO.GetComponent<PlayerInventory>();
        }
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)); //ray를 앞으로 쏨
            if (Physics.Raycast(ray, out RaycastHit hit, pickUpRange))
            {//ray를 쏴서 거리내에 뭔가 닿았다면 hit에 값을 저장

                // IInteractable 인터페이스를 가진 컴포넌트인지 검사
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                if (interactable != null)
                {
                    // 상호작용 실행
                    interactable.Interact();
                }
                else
                {
                    // 아이템 줍기
                    ItemObject item = hit.collider.GetComponent<ItemObject>(); //뭔가 닿은게 itemobject잇는지 검사
                    if (item != null) //아이템 일경우
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
                        Destroy(item.gameObject); //주우면 아이템 파괴

                    }
                    else
                    {

                    }
                }
            }
        }


    }

}