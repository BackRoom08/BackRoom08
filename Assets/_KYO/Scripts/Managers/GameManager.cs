using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum MapQuest
{ 
    Stage1,
    Stage2, 
    Stage3,
    Stage4,
}
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public PlayerData PlayerData = new PlayerData();
    public List<ItemDatas> allItemDatas;

    private PlayerStatus playerStatus;
    private PlayerMove playerMove;
    private FlashLight flashLight;
    private StateNoiseEmitter noiseEmitter;

    //스테이지1
    //private bool isStage1Active = false;
    //private bool isStage1Cleared = false;

    //스테이지2의 퀘스트관련 변수
    private bool isStage2Active = false;
    private bool isStage2Cleared = false;

    //스테이지3
    //private bool isStage3Active = false;
    //private bool isStage3Cleared = false;

    //스테이지4
    //private bool isStage4Active = false;
    //private bool isStage4Cleared = false;
    string savePath => Application.persistentDataPath + "/playerData.json";
    void Awake()
    {
        if (Instance == null)
        { 
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        playerStatus = player.GetComponent<PlayerStatus>();
        playerMove = player.GetComponent<PlayerMove>();
        flashLight = player.GetComponent<FlashLight>();
        noiseEmitter = player.GetComponent<StateNoiseEmitter>();

        InitializePlayerData();  
        LoadPlayerDataFromFile();
        ApplyPlayerDataToPlayer();
    }

    void InitializePlayerData() 
    {
        PlayerData = new PlayerData
        {
            // 정신력
            maxMentalHP = playerStatus.MentalHP,
            currentMentalHP = playerStatus.MentalHP,
            mentalInterval = playerStatus.MentalInterval,
            mentalDamage = playerStatus.MentalDamage,
            isDead = false,

            // 스태미너
            maxStamina = playerMove.maxStamina,
            currentStamina = playerMove.currentStamina,
            staminaUseRate = playerMove.StaminaUseRate,
            staminaHeal = playerMove.staminaHeal,
            staminaHealDelay = playerMove.staminaHealDelay,

            // 이동
            moveSpeed = playerMove.moveSpeed,
            sprintSpeed = playerMove.SprintSpeed,
            crouchSpeed = playerMove.crouchSpeed,
            jumpForce = playerMove.JumpForce,
            position = playerMove.transform.position,
            isCrouching = false,

            // 카메라
            crouchHeight = playerMove.crouchHeight,
            standHeight = playerMove.standHeight,
           
            inventoryItems = new List<ItemSaveData>()
        };
    }

    public void ApplyPlayerDataToPlayer()
    {
        if (playerStatus == null || playerMove == null)
        {
            Debug.LogWarning("플레이어 컴포넌트가 초기화되지 않았습니다.");
            return;
        }

        // 정신력
        playerStatus.MentalHP = PlayerData.currentMentalHP;
        playerStatus.MentalInterval = PlayerData.mentalInterval;
        playerStatus.MentalDamage = PlayerData.mentalDamage;

        // 스태미너
        playerMove.maxStamina = PlayerData.maxStamina;
        playerMove.currentStamina = PlayerData.currentStamina;
        playerMove.StaminaUseRate = PlayerData.staminaUseRate;
        playerMove.staminaHeal = PlayerData.staminaHeal;
        playerMove.staminaHealDelay = PlayerData.staminaHealDelay;

        // 이동
        playerMove.moveSpeed = PlayerData.moveSpeed;
        playerMove.SprintSpeed = PlayerData.sprintSpeed;
        playerMove.crouchSpeed = PlayerData.crouchSpeed;
        playerMove.JumpForce = PlayerData.jumpForce;
        playerMove.transform.position = PlayerData.position;
       // playerMove.isCrouch = PlayerData.isCrouching;

        // 카메라 높이
        playerMove.crouchHeight = PlayerData.crouchHeight;
        playerMove.standHeight = PlayerData.standHeight;

        // 인벤토리 복원
        List<ItemDatas> loadedInventory = LoadInventory(); //저장된 인벤토리 데이터를 불러옴
        PlayerInventory inventory = FindObjectOfType<PlayerInventory>();//현재 씬에서 찾은뒤 변수 저장
        if (inventory != null)
        { 
            inventory.RestoreInventory(loadedInventory);
        }
        else
        {
            Debug.LogWarning("PlayerInventory를 찾을 수 없습니다.");
        }
        Debug.Log("플레이어 데이터 적용 완료");
    }

    public void SaveInventory(List<ItemDatas> currentInventory)
    { 
        PlayerData.inventoryItems.Clear(); //기존 저장된 인벤토리를 모두 지움

        foreach (ItemDatas item in currentInventory)
        {//현재 있는 인벤토리 아이템들을 순차적으로 검사
            if (item == null) continue; // null 아이템은 무시

            ItemSaveData saveData = new ItemSaveData
            {
                uid = item.uid,
                itemName = item.itemName,
                type = item.type
            };
            PlayerData.inventoryItems.Add(saveData); //savedata를 인벤토리 리스트에 추가
        }
    }
    public List<ItemDatas> LoadInventory()
    {
        List<ItemDatas> loadedInventory = new List<ItemDatas>(); //저장된 데이터를 기반으로 복원할 인벤토리 리스트 생성

        foreach (ItemSaveData savedItem in PlayerData.inventoryItems)
        {//저장된 인벤토리 데이터를 하나씩 반복 처리
            ItemDatas matchedItem = allItemDatas.Find(item => item.uid == savedItem.uid);
            //저장된 UID를 기준으로 해당아이템을 찾음
            if (matchedItem != null)
            {// UID의 아이템이 존재하는지 확인
                loadedInventory.Add(matchedItem);
            }
        }       
        Debug.Log("인벤토리 로드 완료!");
        return loadedInventory;
    }

    public List<ItemDatas> GetSavedInventory()
    {
        return LoadInventory(); // 저장된 인벤토리 리스트 반환
    }


    public void SavePlayerDataToFile()
    {
        string json = JsonUtility.ToJson(PlayerData, true); // true는 보기 좋게 들여쓰기
        System.IO.File.WriteAllText(savePath, json); //json으로 저장.기존 파일 없으면 새로 만들고 있으면 덮어씀
    }

    public void LoadPlayerDataFromFile()
    { //외부파일에서 데이터 호출
        if (System.IO.File.Exists(savePath))
        { //save경로에 파일이존재하는지 확인
            string json = System.IO.File.ReadAllText(savePath);//파일 내용을 문자열로 읽어옴
            PlayerData = JsonUtility.FromJson<PlayerData>(json);//읽어온 문자열을 playerdata 타입의 객체로 저장
        }
    }


    public void InitializeMapQuest(MapQuest map)
    {
        switch (map)
        {
            case MapQuest.Stage1 :
                SetupStage1();
                break;
            case MapQuest.Stage2 :
             SetupStage2();
                break;
            case MapQuest.Stage3 :
                SetupStage3();
                break;
            case MapQuest.Stage4 :
                SetupStage4();
                break;
        }
    }

    private void SetupStage1()
    { 
    
    }
    private void SetupStage2()
    {
        isStage2Active = true;
        isStage2Cleared = false;
        Debug.Log("stage2 탈출 목표 : Exit의 컴퓨터에 비밀번호 입력");
    }
    public void CompleteStage2() 
    {
        isStage2Cleared = true;
        Debug.Log("Stage2 퀘스트 완료!");

    }
    private void SetupStage3()
    {

    }
    private void SetupStage4()
    {

    }

}


