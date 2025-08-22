using System.Collections;
using System.Collections.Generic;
using Google.Protobuf;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    public float mouseSensitivity = 200f;
    public Transform Playerbody;
    float xRotation = 0f;
    public Transform flashlight; // 손전등 Transform 추가

    private GameSetting settings;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        settings = UIManager.Instance.Settings;
    }

    void Update()
    {
        float sens = settings.mouseSensitivity;
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime * sens;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime * sens;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f); // 카메라 상하 회전
        Playerbody.Rotate(Vector3.up * mouseX); // 플레이어 좌우 회전
                                                // 손전등 회전 동기화
        flashlight.rotation = transform.rotation;

    }

}
