using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlashLight : MonoBehaviour
{ //플레이어에게 붙임
    public Transform flashLightHolPoint; //손전등을 들 위치
    private GameObject equippedFlashLight; //장착할 손전등을 저장하는 변수
    private bool isFlashLightOn = false; //손전등 on off 여부

    public void EquipFlashLight(GameObject flashlightPrefab)
    { //프리팹을 받아서 장착
        if (equippedFlashLight != null) return; //손전등이 있으면 리턴

        equippedFlashLight = Instantiate(flashlightPrefab, flashLightHolPoint); //손전등 생성
        equippedFlashLight.transform.localPosition = Vector3.zero; //위치 초기화
        equippedFlashLight.transform.localRotation = Quaternion.identity; //회전 초기화
        SetFlashlight(false); //최초 장착 시 손전등 꺼진상태로 설정
    }

    void Update()
    {
        if (equippedFlashLight != null && Input.GetKeyDown(KeyCode.F))
        {//손전등이 장착되어 있고 f키를 눌렀으면
            isFlashLightOn = !isFlashLightOn;
            SetFlashlight(isFlashLightOn); //키거나 끄기
        }
    }
        private void SetFlashlight(bool state)
        {
        Light light = equippedFlashLight.GetComponentInChildren<Light>();
        //손전등 오브젝트에서 light 찾고
            if (light != null) 
            {// 있으면 값에따라 온오프
                light.enabled = state;
            }
        }
}

