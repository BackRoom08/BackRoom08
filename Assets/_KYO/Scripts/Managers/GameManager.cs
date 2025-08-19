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
        ApplyPlayerDataToPlayer();
        LoadPlayerDataFromFile();
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

            //
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
        List<ItemDatas> loadedInventory = LoadInventory();
        // 여기에 loadedInventory를 실제 인벤토리 시스템에 넘겨주는 코드 추가 필요

        Debug.Log("플레이어 데이터 적용 완료");
    }

    public void SaveInventory(List<ItemDatas> currentInventory)
    {
        PlayerData.inventoryItems.Clear(); //플레이어 데이터 기존인벤토리 정보 초기화

        foreach (ItemDatas item in currentInventory)
        { //인벤토리에 있는 아이템을 하나씩 반복해서 처리
            ItemSaveData saveData = new ItemSaveData
            { //저장가능한 형태인 itemsavedata로 변환 스크립터블은 직접저장 불가
                uid = item.uid,
                itemName = item.itemName,
                type = item.type
            };
             PlayerData.inventoryItems.Add(saveData); //변환한 데이터를 인벤토리 리스트에 추가
        }
        Debug.Log("인벤토리 저장 완료");
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
            else
            {//없..
                Debug.LogWarning($"아이템 UID {savedItem.uid}에 해당하는 데이터가 없습니다.");
            }
        }
        //있음.
        Debug.Log("인벤토리 로드 완료!");
        return loadedInventory;
    }

    public void SavePlayerDataToFile()
    {
        string json = JsonUtility.ToJson(PlayerData, true); // true는 보기 좋게 들여쓰기
        System.IO.File.WriteAllText(savePath, json);
        Debug.Log("플레이어 데이터가 JSON 파일로 저장되었습니다: " + savePath);
    }

    public void LoadPlayerDataFromFile()
    {
        if (System.IO.File.Exists(savePath))
        {
            string json = System.IO.File.ReadAllText(savePath);
            PlayerData = JsonUtility.FromJson<PlayerData>(json);
            Debug.Log("플레이어 데이터가 JSON 파일에서 불러와졌습니다.");
        }
        else
        {
            Debug.LogWarning("저장된 JSON 파일이 없습니다.");
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


