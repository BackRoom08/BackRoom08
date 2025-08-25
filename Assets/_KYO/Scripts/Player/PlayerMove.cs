using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class PlayerMove : MonoBehaviour
{ //플레이어에게 붙임
    UnityEngine.CharacterController charctrl;
    public Animator anim;

    public float moveSpeed;   //걸을때 속도.
    public float SprintSpeed; //달리기속도
    public float crouchSpeed; //앉았을때 속도
    public float JumpForce; //점프 높이.

    public float maxStamina = 100f; //최대 스태미너
    public float currentStamina;    //현재 스태미너
    public float StaminaUseRate = 10f; //초당 소모
    public float staminaHeal = 10f;     //초당 회복
    public float staminaHealDelay = 2f; //다시 회복까지 텀
    public Text staminaText; // 스태미너 표시용 텍스트

    public Transform cam; //카메라(앉을때 높이 조절용)
    public float crouchHeight = 1f; //앉은 카메라 높이
    public float standHeight = 1.7f; // 서있을때 높이

    private bool isRecovering = false;
    private float recoveryTimer = 0f;


    private bool isCrouch = false; //앉은 상태 확인용
    private float gravityVelocity; //중력값
    private float currentSpeed; //걷던 뛰던 현재의 속도

    Vector3 defaultCamPos;
    
    // 오민호 사운드
    private StateNoiseEmitter noise;
    
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked; //커서 숨기기
        defaultCamPos = cam.localPosition;
        currentStamina = maxStamina; 
    }
    void Awake()
    {
        anim = GetComponent<Animator>();
        charctrl = GetComponent<UnityEngine.CharacterController>();
        noise = GetComponent<StateNoiseEmitter>();
        GameObject staminaObj = GameObject.Find("StaminaNum");
        if (staminaObj != null)
        {
            staminaText = staminaObj.GetComponent<Text>();
        }
    }
    void Update()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 inputValue = Vector3.ClampMagnitude(new Vector3(x, 0, z), 1);
        bool isMovement = inputValue.magnitude > 0.01f; //입력이 있는지 확인
        
        if (Input.GetKeyDown(KeyCode.LeftControl))
        { //앉고 일어나기
            isCrouch = !isCrouch;            
            noise.SetState(CharacterMoveState.Crouch);
            //print("player Crouch");
            
            float targetY = isCrouch ? crouchHeight : standHeight;

            // 카메라의 로컬 위치 Y값만 조정
            cam.localPosition = new Vector3(
                cam.localPosition.x,
                targetY,
                cam.localPosition.z
            );
            anim.SetBool("Crouch", isCrouch);  
            
        }
        
        if (charctrl.isGrounded && Input.GetButtonDown("Jump") && !isCrouch)
        { //땅에 있고 앉은게 아니면 점프
            gravityVelocity = JumpForce;
            noise.SetState(CharacterMoveState.Jump);
            //print("player Jump");
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            UIManager.Instance.ToggleSettings();
        }
        
        if (anim.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
        {
            noise.SetState(CharacterMoveState.Idle);
        }
        
        bool isSprint = Input.GetKey(KeyCode.LeftShift) && z > 0 && currentStamina > 0f;
        bool isWalk = currentSpeed > 0f && !isCrouch && !isSprint;
        if (isCrouch)
        {
            // 앉은 상태
            currentSpeed = crouchSpeed;
            anim.SetBool("CrouchWalk", isMovement);
            anim.SetBool("Walk", false);
            anim.SetBool("Run", false);
            noise.SetState(CharacterMoveState.Crouch);
            //print("player Crouch");

        }
        else if (isSprint)
        {
            // 뛰기
            currentSpeed = SprintSpeed;
            currentSpeed = SprintSpeed;
            anim.SetBool("Run", isMovement && isSprint);
            anim.SetBool("Walk", isMovement);
            anim.SetBool("CrouchWalk", false);
            // 스테미나 탈진상태
            if (currentStamina <= maxStamina * noise.breathRangeMul)
            {
                noise.SetState(CharacterMoveState.Exhaustion);
                //print("player Exhausted");
            }
            else
            {
                noise.SetState(CharacterMoveState.Run);
                //print("player Run");
            }
        }
        else 
        {
            // 걷기
            currentSpeed = moveSpeed;
            anim.SetBool("Walk", isMovement);
            anim.SetBool("Run", false);
            anim.SetBool("CrouchWalk", false);
            if(anim.GetCurrentAnimatorStateInfo(0).IsName("Walk"))
                noise.SetState(CharacterMoveState.Walk);
           // print("player Walk");
           var st = anim.GetCurrentAnimatorStateInfo(0);
           if (st.IsName("Idle"))
           {
               noise.SetState(CharacterMoveState.Idle);
               // print("Idle 호출");
           }
        }
        // 현재 상태에 따라 속도를 바꿈
        
        if (isSprint && isMovement)
        {
            currentStamina -= StaminaUseRate * Time.deltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
            isRecovering = false;
            recoveryTimer = 0f;
            StaminaUI();
        }
        else 
        {
            if (!isRecovering)
            {
                recoveryTimer += Time.deltaTime;
                if (recoveryTimer >= staminaHealDelay)
                {
                    isRecovering = true;
                }
            }

            if (isRecovering)
            {
                currentStamina += staminaHeal * Time.deltaTime;
                currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
                StaminaUI();
            }
        }
        
        Vector3 move = transform.TransformDirection(inputValue) * currentSpeed;

        if (charctrl.isGrounded && gravityVelocity < 0f)
        {
            gravityVelocity = -5f;
        }
        gravityVelocity += Physics.gravity.y * Time.deltaTime;
        move.y = gravityVelocity;

        charctrl.Move(move * Time.deltaTime);
        
    }

    void StaminaUI() 
    {
        if (staminaText != null)
        {
            // staminaText.text = $"스태미너 : {Mathf.RoundToInt(currentStamina)}";
            staminaText.text = $"{Mathf.RoundToInt(currentStamina)}";
        }
    }
    public void HealStamina(int amount)
    { //스태미너 회복물약을 사용하기 위한 함수
        currentStamina += amount;
        currentStamina = Mathf.Min(currentStamina, maxStamina);
        StaminaUI();
    }
}
