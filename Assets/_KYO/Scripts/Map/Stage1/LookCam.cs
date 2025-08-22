using UnityEngine;

public class LookCam : MonoBehaviour
{
    public Transform target;    // 메인 카메라
    public float minRadius = 0.1f;
    public float maxRadius = 0.2f;
    public float angularSpeedDeg = 30f; // 초당 도(deg/s)
    public float radiusPulseSpeed = 1f; // 반지름 변화 속도(Hz 느낌)

    Vector3 center;
    float angleRad;

    void Start()
    {
        center = target ? target.position : transform.position;
        angleRad = 0f;
    }

    void Update()
    {
        var cam = Camera.main ? Camera.main.transform : null;
        Vector3 right, fwdOnPlane;

        if (cam)
        {
            right = cam.right;
            right.y = 0f;
            right.Normalize();
            
            fwdOnPlane = cam.forward;
            fwdOnPlane.y = 0f;
            if (fwdOnPlane.sqrMagnitude < 0.0001f) fwdOnPlane = Vector3.forward; // 수직 카메라 예외 처리
            fwdOnPlane.Normalize();
        }
        else
        {
            right = Vector3.right;
            fwdOnPlane = Vector3.forward;
        }
        
        angleRad += Mathf.Deg2Rad * angularSpeedDeg * Time.unscaledDeltaTime;
        
        float t = 0.5f * (1f + Mathf.Sin(2f * Mathf.PI * radiusPulseSpeed * Time.unscaledDeltaTime + Time.time));
        float radius = Mathf.Lerp(minRadius, maxRadius, t);
        
        Vector3 offset = (Mathf.Cos(angleRad) * right + Mathf.Sin(angleRad) * fwdOnPlane) * radius;
        transform.position = center + offset;
    }
}
