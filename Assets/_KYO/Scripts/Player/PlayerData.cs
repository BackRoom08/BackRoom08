using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerData
{
    // 정신력 관련
    public int maxMentalHP; //정신력 최대치
    public int currentMentalHP; //현재 정신력
    public float mentalInterval; //정신력감소간격
    public int mentalDamage; //정신력이 깎이는 데미지

    // 스태미너 관련
    public float maxStamina; //최대 스태미너
    public float currentStamina; //현재 스태미너
    public float staminaUseRate; //초당 소모되는 스태미너
    public float staminaHeal; //초당 회복되는 스태미너
    public float staminaHealDelay; //회복까지 대기시간

    // 이동 관련
    public float moveSpeed;
    public float sprintSpeed;
    public float crouchSpeed;
    public float jumpForce;

    // 카메라 관련
    public float crouchHeight; //앉았을때 카메라 높이
    public float standHeight; //서 있을때 카메라 높이

    // 위치 및 상태
    public Vector3 position; 
    public bool isCrouching;
    public bool isDead;

    // 손전등 상태
    public bool hasFlashlight; //손전등 소지 여부
    public string equippedFlashlightName; //손전등 이름

    // 손전등 관련
    public string flashlightID; // 프리팹 이름 또는 고유 ID
    public bool isFlashlightOn; // on/off 여부

    // 인벤토리 관련
    public List<ItemSaveData> inventoryItems = new List<ItemSaveData>();

    //// 사운드 관련
    //public CharacterMoveState currentSoundState; //플레이어 움직임상태
    //public float masterVolume; //전체사운드볼륨
    //public float walkLoudMul; //걷기사운드
    //public float runBreathLoudMul; //뛰기사운드
    //public float exhaustLoudMul; //탈진사운드
    //public float breathRangeMul; //숨소리사운드

}
