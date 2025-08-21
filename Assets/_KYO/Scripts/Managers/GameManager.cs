using System.Collections;
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

    private PlayerStatus playerStatus;
    private PlayerMove playerMove;
    private FlashLight flashLight;
    private StateNoiseEmitter noiseEmitter;

    string savePath => Application.persistentDataPath + "/playerData.json";

    private readonly string[] playableScenes = { "Stage1", "Stage2", "Stage3", "Stage4" };

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);

        SavePlayerStatus();
    }

    void Start()
    {
        SavePlayerStatus();
        LoadPlayerDataFromFile();
        SceneLoader.Instance.OnSceneLoaded += HandleSceneLoaded;
    }

    private void HandleSceneLoaded(string sceneName)
    {
        Debug.Log($"씬 '{sceneName}' 로딩 완료됨.");

        if (playableScenes.Contains(sceneName))
        {
            Debug.Log("플레이어가 존재하는 씬입니다. 데이터 적용 시작.");
            ApplyPlayerDataToPlayer();
        }
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

    private IEnumerator WaitAndApplyPlayerData()
    {
        yield return new WaitUntil(() => GameObject.FindGameObjectWithTag("Player") != null);
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        playerStatus = player.GetComponent<PlayerStatus>();
        playerMove = player.GetComponent<PlayerMove>();
        flashLight = player.GetComponent<FlashLight>();
        noiseEmitter = player.GetComponent<StateNoiseEmitter>();

        if (playerStatus == null || playerMove == null)
        {
            Debug.LogWarning("플레이어 컴포넌트가 초기화되지 않았습니다.");
            yield break;
        }

        // 플레이어 상태 복원
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

        playerMove.crouchHeight = PlayerData.crouchHeight;
        playerMove.standHeight = PlayerData.standHeight;
    }

    public void ApplyPlayerDataToPlayer()
    {
        StartCoroutine(WaitAndApplyPlayerData());
    }

    public void SavePlayerStatus()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        playerStatus = player.GetComponent<PlayerStatus>();
        playerMove = player.GetComponent<PlayerMove>();

        if (playerStatus == null || playerMove == null) return;

        PlayerData.currentMentalHP = playerStatus.MentalHP;
        PlayerData.mentalInterval = playerStatus.MentalInterval;
        PlayerData.mentalDamage = playerStatus.MentalDamage;

        PlayerData.maxStamina = playerMove.maxStamina;
        PlayerData.currentStamina = playerMove.currentStamina;
        PlayerData.staminaUseRate = playerMove.StaminaUseRate;
        PlayerData.staminaHeal = playerMove.staminaHeal;
        PlayerData.staminaHealDelay = playerMove.staminaHealDelay;

        PlayerData.moveSpeed = playerMove.moveSpeed;
        PlayerData.sprintSpeed = playerMove.SprintSpeed;
        PlayerData.crouchSpeed = playerMove.crouchSpeed;
        PlayerData.jumpForce = playerMove.JumpForce;

        PlayerData.crouchHeight = playerMove.crouchHeight;
        PlayerData.standHeight = playerMove.standHeight;

        Debug.Log("플레이어 상태 저장 완료");
        SavePlayerDataToFile();
    }

    public void InitializeMapQuest(MapQuest map)
    {
        switch (map)
        {
            case MapQuest.Stage1: SetupStage1(); break;
            case MapQuest.Stage2: SetupStage2(); break;
            case MapQuest.Stage3: SetupStage3(); break;
            case MapQuest.Stage4: SetupStage4(); break;
        }
    }

    private void SetupStage1() { }
    private void SetupStage2() { }
    private void SetupStage3() { }
    private void SetupStage4() { }
}
