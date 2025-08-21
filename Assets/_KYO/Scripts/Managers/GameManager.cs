using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;


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
        ApplyPlayerDataToPlayer();
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
            maxMentalHP = playerStatus.MentalHP,
            currentMentalHP = playerStatus.MentalHP,
            mentalInterval = playerStatus.MentalInterval,
            mentalDamage = playerStatus.MentalDamage,
            isDead = false,

            maxStamina = playerMove.maxStamina,
            currentStamina = playerMove.currentStamina,
            staminaUseRate = playerMove.StaminaUseRate,
            staminaHeal = playerMove.staminaHeal,
            staminaHealDelay = playerMove.staminaHealDelay,

            moveSpeed = playerMove.moveSpeed,
            sprintSpeed = playerMove.SprintSpeed,
            crouchSpeed = playerMove.crouchSpeed,
            jumpForce = playerMove.JumpForce,
            position = playerMove.transform.position,
            isCrouching = false,

            crouchHeight = playerMove.crouchHeight,
            standHeight = playerMove.standHeight,

            inventoryItems = new List<ItemSaveData>(),
            selectedInventoryIndex = -1
        };
    }

    public void ApplyPlayerDataToPlayer()
    {
        if (playerStatus == null || playerMove == null)
        {
            Debug.LogWarning("플레이어 컴포넌트가 초기화되지 않았습니다.");
            return;
        }

        playerStatus.MentalHP = PlayerData.currentMentalHP;
        playerStatus.MentalInterval = PlayerData.mentalInterval;
        playerStatus.MentalDamage = PlayerData.mentalDamage;

        playerMove.maxStamina = PlayerData.maxStamina;
        playerMove.currentStamina = PlayerData.currentStamina;
        playerMove.StaminaUseRate = PlayerData.staminaUseRate;
        playerMove.staminaHeal = PlayerData.staminaHeal;
        playerMove.staminaHealDelay = PlayerData.staminaHealDelay;

        playerMove.moveSpeed = PlayerData.moveSpeed;
        playerMove.SprintSpeed = PlayerData.sprintSpeed;
        playerMove.crouchSpeed = PlayerData.crouchSpeed;
        playerMove.JumpForce = PlayerData.jumpForce;
        playerMove.transform.position = PlayerData.position;

        playerMove.crouchHeight = PlayerData.crouchHeight;
        playerMove.standHeight = PlayerData.standHeight;

        List<ItemDatas> loadedInventory = LoadInventory();
        PlayerInventory inventory = FindObjectOfType<PlayerInventory>();
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
        PlayerData.inventoryItems.Clear();

        var uniqueItems = currentInventory
            .Where(item => item != null)
            .GroupBy(item => item.uid)
            .Select(group => group.First())
            .ToList();

        foreach (ItemDatas item in uniqueItems)
        {
            ItemSaveData saveData = new ItemSaveData
            {
                uid = item.uid,
                itemName = item.itemName,
                type = item.type
            };
            PlayerData.inventoryItems.Add(saveData);
        }
    }

    public List<ItemDatas> LoadInventory()
    {
        List<ItemDatas> loadedInventory = new List<ItemDatas>();

        foreach (ItemSaveData savedItem in PlayerData.inventoryItems)
        {
            ItemDatas matchedItem = allItemDatas?.Find(item => item != null && item.uid == savedItem?.uid);
            if (matchedItem != null)
            {
                loadedInventory.Add(matchedItem);
            }
        }
        //Debug.Log("인벤토리 로드 완료!");
        return loadedInventory;
    }

    public List<ItemDatas> GetSavedInventory()
    {
        return LoadInventory();
    }

    public void SavePlayerDataToFile()
    {
        string json = JsonUtility.ToJson(PlayerData, true);
        System.IO.File.WriteAllText(savePath, json);
    }

    public void LoadPlayerDataFromFile()
    {
        if (System.IO.File.Exists(savePath))
        {
            string json = System.IO.File.ReadAllText(savePath);
            PlayerData = JsonUtility.FromJson<PlayerData>(json);
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

    }
    private void SetupStage3()
    {

    }
    private void SetupStage4()
    {

    }

}


