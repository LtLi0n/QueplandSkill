using System;
using Microsoft.AspNetCore.Components;
using System.Collections.Generic;
using System.Net.Http;
using System.Linq;

public class Player
{
    public Inventory Inventory { get; }
    public Bank Bank { get; }
    public House House { get; }
    public Follower ActiveFollower { get; set; }
    public Pet ActivePet { get; set; }
    public MessageManager MessageManager { get; set; }
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    public List<Pet> Pets { get; }
    public SkillHandler Skills { get; }
    private readonly List<GameItem> _equippedItems;
    public IReadOnlyCollection<GameItem> EquippedItems => _equippedItems;

    public List<string> KnownAlchemicRecipes { get; set; }
    public List<string> ShorterRecipes { get; set; }

    //DEBUG Value!
    private readonly int _maxInventorySize = 30;
    public string LastLevelledSkill;
    public bool LastLevelledSkillLocked;

	public Player()
	{
        Inventory = new Inventory(_maxInventorySize);
        Bank = new Bank();
        House = new House();
        Skills = new SkillHandler(this);
        Skills.SkillExperienceAdd += Skills_SkillExperienceAdd;
        _equippedItems = new List<GameItem>();
        KnownAlchemicRecipes = new List<string>();
        ShorterRecipes = new List<string>();
        MaxHP = 50;
	}

    private void Skills_SkillExperienceAdd(object sender, SkillExperienceAddedEventArgs e)
    {
        if (ActivePet != null)
        {
            if (ActivePet.messageManager == null)
            {
                ActivePet.messageManager = MessageManager;
            }
            ActivePet.GainExperience(e.Skill.Name, e.Amount / 10);
            e.Skill.Experience += (long)(e.Amount * Skills.GetExperienceGainBonus(e.Skill) * ActivePet.GetSkillBoost(e.Skill));
        }
    }

    public void LearnNewAlchemyRecipe(GameItem metal, GameItem element, Building location, GameItem result)
    {
        string recipe = "" + metal.ItemName + " + " + element.ItemName + " in " + location.Name + " = " + result.ItemName;
        string shortRecipe = "" + metal.ItemName.Substring(0, 3) + "+" + element.ItemName.Substring(0,3) + "+" + location.Name.Substring(0,3) + "+" + result.ItemName.Substring(0,3);
        bool alreadyKnown = false;
        foreach(string r in KnownAlchemicRecipes)
        {
            if(r == recipe)
            {
                alreadyKnown = true;
            }
        }
        if(alreadyKnown == false)
        {
            KnownAlchemicRecipes.Add(recipe);
            ShorterRecipes.Add(recipe);
        }

        KnownAlchemicRecipes.Sort();
    }

    public bool HasPet(Pet pet)
    {
        foreach(Pet p in Pets)
        {
            if(p.Name == pet.Name)
            {
                return true;
            }
        }
        return false;
    }

    public void EquipItem(GameItem item)
    {
        UnequipItem(_equippedItems.Find(x => x.EquipSlot == item.EquipSlot));
        _equippedItems.Add(item);
        item.IsEquipped = true;
    }

    public void EquipItems(List<int> ids)
    {
        foreach (KeyValuePair<GameItem, int> pair in Inventory.GetItems())
        {
            foreach (int i in ids)
            {
                if (pair.Key.Id == i)
                {
                    EquipItem(pair.Key);
                }
            }
        }
    }
    public void UnequipItem(GameItem item)
    {
        if (item != null)
        {
            item.IsEquipped = false;
            _equippedItems.Remove(item);
        }
    }

    //Should be moved to Pet class.
    public string GetPetString()
    {
        string petString = "";
        foreach(Pet p in Pets)
        {
            petString += p.GetSaveString();
            petString += (char)16;
        }
        if (ActivePet != null)
        {
            petString += (char)17 + ActivePet.Name;
        }
        else
        {
            petString += (char)17 + "None";
        }
        return petString;
    }

    //Should be moved to Pet class.
    public void LoadPetsFromString(string data)
    {
        string[] lines = data.Split((char)17)[0].Split((char)16);
        foreach(string line in lines)
        {
            if(line.Length > 1)
            {
                Pet newPet = new Pet();
                string[] info = line.Split((char)15)[0].Split((char)14);
                newPet.Name = info[0];
                newPet.Description = info[1];
                newPet.Nickname = info[2];

                newPet.MinLevel = int.Parse(info[3]);

                newPet.Affinity = info[4];

                newPet.Identifier = info[5];
                string skillString = line.Split((char)15)[1];
                
                newPet.SetSkills(Extensions.GetSkillsFromString(skillString));
                if(Pets.Find(x => x.Name == info[0]) == null)
                {
                    Pets.Add(newPet);
                }
            }

        }
        if (data.Split((char)17).Length > 1)
        {
            if(data.Split((char)17)[1] == "None")
            {
                return;
            }
            ActivePet = Pets.Find(x => x.Name == data.Split((char)17)[1]);
        }
    }

    public bool HasIngredients(int[] ingredientIDs)
    {
        foreach(int ingredient in ingredientIDs)
        {
            if(Inventory.HasItem(ingredient) == false)
            {
                return false;
            }
        }
        return true;
    }

    public int GetDamageDealt(Monster opponent)
    {
        int str = Skills[SkillType.Strength].LevelBoosted;
        int deft = Skills[SkillType.Deftness].LevelBoosted;
        float baseDamage = 1 + (str / 4);

        int equipmentBonus = GetEquipmentBonus();

        if (GetWeapon() != null)
        {
            string action = GetWeapon().ActionRequired;

            if (action.Contains("Knife"))
            {
                baseDamage += deft * 2;
                baseDamage += Skills[SkillType.Knifesmanship].LevelBoosted;
            }
            else if (action.Contains("Sword"))
            {
                baseDamage += str;
                baseDamage += deft * 2;
                baseDamage += Skills[SkillType.Swordsmanship].LevelBoosted;
            }
            else if (action.Contains("Axe"))
            {
                baseDamage += str * 2;
                baseDamage += deft / 2f;
                baseDamage += Skills[SkillType.Axemanship].LevelBoosted * 2;
            }
            else if (action.Contains("Hammer"))
            {
                baseDamage += str * 3;
                baseDamage += Skills[SkillType.Hammermanship].LevelBoosted * 2;
            }
            else if (action.Contains("Archery"))
            {
                baseDamage = 1 + Skills[SkillType.Archery].LevelBoosted * 3 + (deft * 2);
                if (Inventory.HasArrows())
                {
                    baseDamage += Inventory.GetStrongestArrows().Damage;
                }
            }
            if (opponent.Weakness.Contains(action))
            {
                baseDamage *= 1.75f;
                baseDamage *= 1 - ((float)Extensions.CalculateArmorDamageReduction(opponent) / 3f);
            }
            else if (opponent.Strength.Contains(action))
            {
                baseDamage /= 1.75f;
                baseDamage *= 1 - (float)Extensions.CalculateArmorDamageReduction(opponent);
            }
            else
            {
                baseDamage *= 1 - (float)Extensions.CalculateArmorDamageReduction(opponent);
            }
            
            return Math.Max(Extensions.GetGaussianRandomInt(baseDamage + equipmentBonus, baseDamage / 3f), 1);
        }
        baseDamage *= 1 - (float)Extensions.CalculateArmorDamageReduction(opponent);
        return Math.Max(Extensions.GetGaussianRandomInt(baseDamage + equipmentBonus, baseDamage / 3f), 1);
    }
    public int GetEquipmentBonus()
    {
        int total = 0;
        foreach(GameItem item in _equippedItems)
        {
            if(item.ActionRequired == "Archery" && !Inventory.HasArrows())
            {
                total += item.Damage / 4;
            }
            else
            {
                total += (item.Damage * 4);
            }          
        }
        return total;
    }

    public GameItem GetWeapon() => _equippedItems.Find(x => x.EquipSlot == "Right Hand");

    public int GetWeaponAttackSpeed()
    {
        if(GetWeapon() != null)
        {
            return GetWeapon().AttackSpeed - Skills[SkillType.Deftness].LevelBoosted;
        }
        else
        {
            return 1500 - Skills[SkillType.Deftness].LevelBoosted;
        }
    }

    
    public bool HasItemToAccessArea(string requirement)
    {
        if(requirement == null)
        {
            return true;
        }
        foreach(KeyValuePair<GameItem, int> item in Inventory.GetItems())
        {
            if(item.Key.ActionsEnabled != null && item.Key.ActionsEnabled.Contains(requirement))
            {
                return true;
            }
        }
        return false;
    }
}
