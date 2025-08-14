using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StaminaPotion", menuName = "Scriptable Object/Items/StaminaPption")]
public class StaminaPotion : ItemDatas
{
    public int HealStaminaRate = 100;
    private bool isDrink = false;
    public void Use(PlayerMove playermove) 
    {
        if (playermove != null)
        {
            playermove.HealStamina(HealStaminaRate);
            Debug.Log("냠냠 불스원샷");
            playermove.anim.SetBool("Drink", true);
            playermove.StartCoroutine(ResetDrink(playermove.anim));
        }
    }

    protected override void Reset()
    {
        type = ItemType.Consumable;
        itemName = "신비한 물병";
        description = "더욱 더 달릴 수 있게 해주는 신비한 물";
        HealStaminaRate = 50;
    }

    private IEnumerator ResetDrink(Animator anim)
    {
        yield return new WaitForSeconds(1f);
        anim.SetBool("Drink", false);
    }

}
