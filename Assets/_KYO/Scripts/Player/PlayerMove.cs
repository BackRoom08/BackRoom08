using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    UnityEngine.CharacterController charctrl;
    public Animator anim;

    public float moveSpeed;   //걸을때 속도.
    public float SprintSpeed; //달리기속도
    public float crouchSpeed; //앉았을때 속도
    public float JumpForce; //점프 높이.

    public Transform cam; //카메라(앉을때 높이 조절용)
    public float crouchHeight = 1f; //앉은 카메라 높이
    public float standHeight = 1.7f; // 서있을때 높이

    private bool isCrouch = false; //앉은 상태 확인용
    private float gravityVelocity; //중력값
    private float currentSpeed; //걷던 뛰던 현재의 속도

    Vector3 defaultCamPos;
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked; //커서 숨기기
        defaultCamPos = cam.localPosition;
    }
    void Awake()
    {
        anim = GetComponent<Animator>();
        charctrl = GetComponent<UnityEngine.CharacterController>();
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
            Debug.Log("앉기키 작동");

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
        }

        bool isSprint = Input.GetKey(KeyCode.LeftShift) && z > 0;
        bool isWalk = currentSpeed > 0f && !isCrouch && !isSprint;
        if (isCrouch)
        {
            currentSpeed = crouchSpeed;
            anim.SetBool("CrouchWalk", isMovement);
            anim.SetBool("Walk", false);
            anim.SetBool("Run", false);

        }
        else if (isSprint)
        {
            currentSpeed = SprintSpeed;
            anim.SetBool("Run", isMovement && isSprint);
            anim.SetBool("Walk", isMovement);
            anim.SetBool("CrouchWalk", false);
        }
        else 
        {
            currentSpeed = moveSpeed;
            anim.SetBool("Walk", isMovement);
            anim.SetBool("Run", false);
            anim.SetBool("CrouchWalk", false);
        }     
        // 현재 상태에 따라 속도를 바꿈


        Vector3 move = transform.TransformDirection(inputValue) * currentSpeed;

        if (charctrl.isGrounded && gravityVelocity < 0f)
        {
            gravityVelocity = -5f;
        }
        gravityVelocity += Physics.gravity.y * Time.deltaTime;
        move.y = gravityVelocity;

        charctrl.Move(move * Time.deltaTime);
    }
}
