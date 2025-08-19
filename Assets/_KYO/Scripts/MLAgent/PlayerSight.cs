using UnityEngine;

public class PlayerSight : MonoBehaviour
{
    [Header("FOV")]
    public Transform eye;            // 카메라나 머리 위치
    public float viewDistance = 20f;
    [Range(0,180f)] public float viewHalfAngle = 60f;
    public LayerMask obstacleMask;   // 벽/기둥 등 가림 레이어
    
    public bool IsSeeingMonster { get; private set; }
    public Vector3 LastSeenPosition { get; private set; }
    public float LastSeenTime { get; private set; }

    public bool CanSee(Transform monster)
    {
        IsSeeingMonster = false;
        if (!monster || !eye) return false;

        Vector3 from = eye.position;
        Vector3 to = monster.position;
        Vector3 v = to - from;
        float dist = v.magnitude;
        if (dist > viewDistance) return false;

        // 시야각
        Vector3 fwd = eye.forward; fwd.y = 0f;
        Vector3 flat = v; flat.y = 0f;
        if (Vector3.Angle(fwd, flat) > viewHalfAngle) return false;

        // 가림체(레이 충돌) 있으면 못봄
        if (Physics.Raycast(from, v.normalized, out var hit, viewDistance, obstacleMask))
        {
            if (hit.transform != monster && hit.transform.IsChildOf(monster) == false)
                return false;
        }

        // 봄!
        IsSeeingMonster = true;
        LastSeenPosition = to;
        LastSeenTime = Time.time;
        return true;
    }
}