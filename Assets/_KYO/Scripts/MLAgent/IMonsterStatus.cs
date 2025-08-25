using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MonsterMode
{
    Idle = 0,        // 대기
    Patrol = 1,      // 순찰(포인트 순회)
    Investigate = 2, // 소리 지점/의심 지점 수색
    Stalk = 3,       // 주시(2초 정지→응시 스택 채우기)
    Chase = 4,       // 추격
    Ambush = 5       // 매복(길목 대기)
}


public interface IMonsterStatus
{
    MonsterMode CurrentMode { get; }
    bool IsEmpowered { get; }   // 강화 모드 여부
    float StareStack { get; }   // 0~30초
    float EmpowerRemain { get; } // 0~40초
}

