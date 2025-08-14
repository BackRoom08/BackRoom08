using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HealItem", menuName = "Scriptable Object/Items/HealItem")]
public class HealItem : ItemDatas
{ // 스크립터블. item data 형식에 참조되어있음
    public int HealMentalHP = 50;
    private bool isDrink = false;
    public void Use(PlayerStatus playerstatus)
    {
        if (playerstatus != null)
        {
            playerstatus.HealMentalHP(HealMentalHP);
            Debug.Log("냠냠 회복중");          
        }
    }

    protected override void Reset()
    {
        type = ItemType.Consumable;
        itemName = "물병";
        description = "정신력을 회복시켜주는 시원한 물병";
        HealMentalHP = 50;
    }
    private IEnumerator ResetDrink(PlayerStatus playerstatus)
    {
        yield return new WaitForSeconds(1f);
        playerstatus.anim.SetBool("Drink", false);
    }

}
