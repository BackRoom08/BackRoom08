using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ItemPickUp : MonoBehaviour
{ //플레이어에게 붙임
    public Camera cam;
    public float pickUpRange = 3f;
    public PlayerInventory inventory;

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
        // 씬이 로드된 후 오브젝트가 준비될 때까지 기다리는 코루틴 시작
        StartCoroutine(InitializeAfterSceneLoad());
    }

    IEnumerator InitializeAfterSceneLoad()
    {
        // MainCamera가 준비될 때까지 대기
        yield return new WaitUntil(() => GameObject.FindGameObjectWithTag("MainCamera") != null);
        cam = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();

        // 인벤토리 오브젝트가 준비될 때까지 대기
        yield return new WaitUntil(() => GameObject.Find("Canvas/P_PlayerUI/P_InventoryController") != null);
        inventory = GameObject.Find("Canvas/P_PlayerUI/P_InventoryController").GetComponent<PlayerInventory>();
    }



    void Update()
    {
        // 씬이 다시 로드될 때를 대비해 cam 참조가 비었으면 다시 찾아 할당합니다.
        if (cam == null)
        {
            cam = Camera.main;
        }

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
                        if (item.data.type == ItemType.Flash) //손전등 일경우
                        {
                            FlashLight flashLight = this.GetComponent<FlashLight>(); //flash 컴포넌트 가져옴
                            if (flashLight != null && flashLight.equippedFlashLight == null)
                            {
                                flashLight.EquipFlashLight(item.data.modelPrefab); //손전등 장착
                                inventory.AddItem(item.data); //인벤토리 아이템 추가
                                Destroy(item.gameObject); //주우면 아이템 파괴
                            }
                        }
                        else
                        {
                            inventory.AddItem(item.data);
                            Destroy(item.gameObject); //주우면 아이템 파괴
                        }
                    }
                }
            }
        }
    }
}