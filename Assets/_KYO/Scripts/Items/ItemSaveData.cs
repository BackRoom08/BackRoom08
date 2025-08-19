using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ItemSaveData
{
    public int uid;           // 아이템 고유 ID
    public string itemName;   // 이름 (UI용)
    public ItemType type;     // 타입
    // icon, modelPrefab, description은 ScriptableObject에서 다시 불러오면됨
}
