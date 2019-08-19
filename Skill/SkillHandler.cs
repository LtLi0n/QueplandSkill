using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

public class SkillHandler
{
    public Player Player { get; }
    private readonly Dictionary<SkillType, Skill> _skills;
    public IReadOnlyDictionary<SkillType, Skill> Skills => _skills;
    public bool Loaded { get; private set; }
    public SkillType LastLevelledSkill { get; private set; }
    public bool LastLevelledSkillLocked { get; private set; }
    public int TotalLevels => _skills.Values.Sum(x => x.Level);
    public event EventHandler<SkillExperienceAddedEventArgs> SkillExperienceAdd;

    public Skill this[SkillType skillType] => Skills.ContainsKey(skillType) ? Skills[skillType] : null;

    public SkillHandler(Player player)
    {
        Player = player;
        _skills = new Dictionary<SkillType, Skill>();
        Loaded = false;
    }

    public async Task LoadSkills(HttpClient Http)
    {
        List<Skill> skills = await Http.GetJsonAsync<List<Skill>>("data/skills.json");
        _skills.Clear();
        foreach (Skill skill in skills)
        {
            _skills.Add(skill.SkillType, skill);
            skill.LevelUp += Skill_LevelUp;
        }
        //?
        Console.WriteLine(Skills.FirstOrDefault().Value.LevelBoosted);
        Loaded = true;
    }

    private void Skill_LevelUp(object sender, SkillLevelUpEventArgs e)
    {
        Player.MessageManager.AddMessage("You leveled up! Your " + e.Skill.Name + " level is now " + e.Skill.Level + ".");
        if (e.Skill.SkillType == SkillType.Strength)
        {
            Player.Inventory.IncreaseMaxSizeBy(1);

            if (e.Skill.Level % 10 == 0)
            {
                Player.Inventory.IncreaseMaxSizeBy(4);
                Player.MessageManager.AddMessage("You feel stronger. You can now carry 5 more items in your inventory.");
            }
            else
            {
                Player.MessageManager.AddMessage("You feel stronger. You can now carry 1 more item in your inventory.");
            }
        }
        else if (e.Skill.SkillType == SkillType.HP)
        {
            Player.MaxHP += 5;
            if (e.Skill.Level % 5 == 0)
            {
                Player.MaxHP += 10;
                Player.MessageManager.AddMessage("You feel much healthier. Your maximum HP has increased by 15!");
            }
            else
            {
                Player.MessageManager.AddMessage("You feel healthier. Your maximum HP has increased by 5.");
            }
        }
    }

    public void SyncSkills(IEnumerable<Skill> skillList)
    {
        foreach (Skill s in skillList)
        {
            if (_skills.ContainsKey(s.SkillType))
            {
                Skill focused_skill = _skills[s.SkillType];

                focused_skill.Experience = s.Experience;
                focused_skill.Level = s.Level;

                if (s.SkillType == SkillType.Strength)
                {
                    int extraSlots = (s.Level / 10) * 4;
                    //Subtract 1 to make up for starting at level 1.
                    Player.Inventory.ResetMaxSize();
                    Player.Inventory.IncreaseMaxSizeBy(s.Level + extraSlots - 1);
                }
                else if (s.SkillType == SkillType.HP)
                {
                    Player.MaxHP = 50;
                    int extraHP = (s.Level / 5) * 10;
                    Player.MaxHP += (s.Level * 5) + extraHP - 5;
                }
            }
        }

    }

    /// <param name="skill_custom">Contains skill name and skill level seperated by ':' symbol.</param>
    public void AddExperienceFromMultipleItems(string skill_custom, int amount)
    {
        if(string.IsNullOrEmpty(skill_custom))
        {
            return;
        }

        if (int.TryParse(skill_custom.Split(':')[1], out int multi))
        {
            AddExperience(Skills.Values.FirstOrDefault(x => x.Name == skill_custom.Split(':')[0]).SkillType, multi * amount);
        }
    }

    /// <param name="skill_custom">Contains skill name and skill level seperated by ':' symbol.</param>
    public void AddExperience(string skill_custom)
    {
        if (string.IsNullOrEmpty(skill_custom))
        {
            return;
        }
        if (int.TryParse(skill_custom.Split(':')[1], out int amount))
        {
            AddExperience(Skills.Values.FirstOrDefault(x => x.Name == skill_custom.Split(':')[0]).SkillType, amount);
        }
    }

    public void AddExperienceFromWeapon(GameItem weapon, int damageDealt)
    {
        if (weapon.ActionRequired == null)
        {
            return;
        }
        if (weapon.ActionRequired.Contains("Knife"))
        {
            AddExperience(SkillType.Deftness, (int)(damageDealt * 1.5));
            AddExperience(SkillType.Knifesmanship, (int)(damageDealt * 1.5));
        }
        else if (weapon.ActionRequired.Contains("Sword"))
        {
            AddExperience(SkillType.Deftness, (int)(damageDealt * 0.5));
            AddExperience(SkillType.Strength, damageDealt);
            AddExperience(SkillType.Swordsmanship, damageDealt);
        }
        else if (weapon.ActionRequired.Contains("Axe"))
        {
            AddExperience(SkillType.Deftness, (int)(damageDealt * 0.5));
            AddExperience(SkillType.Strength, damageDealt);
            AddExperience(SkillType.Axemanship, damageDealt);
        }
        else if (weapon.ActionRequired.Contains("Hammer"))
        {
            AddExperience(SkillType.Strength, (int)(damageDealt * 1.5));
            AddExperience(SkillType.Hammermanship, damageDealt);
        }
        else if (weapon.ActionRequired.Contains("Archery"))
        {
            if (Player.Inventory.HasArrows())
            {
                AddExperience(SkillType.Archery, (int)(damageDealt * 1.5));
            }
            else
            {
                AddExperience(SkillType.Strength, (int)(damageDealt * 0.5));
            }
        }
        else if (weapon.ActionRequired.Contains("Fishing"))
        {
            AddExperience(SkillType.Fishing, (int)(damageDealt * 0.1));
        }
    }

    public void AddExperience(SkillType skill_type, long amount)
    {
        if (skill_type == SkillType.Unknown)
        {
            Console.WriteLine("Gained " + amount + " experience in unfound skill.");
            return;
        }
        if (amount <= 0 || !Skills.ContainsKey(skill_type))
        {
            return;
        }

        Skill skill = Skills[skill_type];

        if (!LastLevelledSkillLocked)
        {
            LastLevelledSkill = skill.SkillType;
        }
        else
        {
            skill.Experience += (long)(amount * GetExperienceGainBonus(skill));
        }

        SkillExperienceAdd?.Invoke(this, new SkillExperienceAddedEventArgs(skill, amount));
        skill.TryIncreaseLevels();
    }

    ///<summary>Returns true if the player has the required skill level.
    ///<para>Use HasRequiredLevels for multiple skills.</para></summary>
    public bool HasRequiredLevel(GameItem item)
    {
        if (item.RequiredLevel == 0)
        {
            return true;
        }
        Skill skillToCheck = Skills.Values.FirstOrDefault(x => x.Name == item.ActionRequired);
        if (skillToCheck != null)
        {
            return skillToCheck.LevelBoosted >= item.RequiredLevel;
        }
        Console.WriteLine($"Skill {item.ActionRequired} was not found in player's list of skills.");
        return false;
    }

    public float GetExperienceGainBonus(SkillType skill) => GetExperienceGainBonus(this[skill]);

    public float GetExperienceGainBonus(Skill skill)
    {
        float baseExp = 1;
        if (skill == null)
        {
            return 1;
        }
        foreach (GameItem equipped in Player.EquippedItems)
        {
            if (equipped.ActionRequired == skill.Name)
            {
                baseExp += equipped.ExperienceGainBonus;
            }
        }
        return baseExp;
    }

    public float GetGatherSpeed(SkillType skillType)
    {
        float totalBonus = 1;
        if(!Skills.ContainsKey(skillType))
        {
            Skill skill = Skills[skillType];

            foreach (GameItem item in Player.EquippedItems)
            {
                if (item.ActionsEnabled != null && item.ActionsEnabled.Contains(skill.Name))
                {
                    if (item.GatherSpeedBonus > 0)
                    {
                        totalBonus *= 1 - item.GatherSpeedBonus;
                    }
                }
                else if (item.ActionRequired != null && item.ActionRequired.Contains(skill.Name))
                {
                    if (item.GatherSpeedBonus > 0)
                    {
                        totalBonus *= 1 - item.GatherSpeedBonus;
                    }
                }
            }
            int level = skill.Level;
            if (level < 100)
            {
                totalBonus *= 1 - (level * 0.005f);
            }
            else if (level < 200)
            {
                totalBonus *= 1 - (100 * 0.005f);
                totalBonus *= 1 - (level * 0.002f);
            }
            else if (level < 300)
            {
                totalBonus *= 1 - (100 * 0.005f);
                totalBonus *= 1 - (200 * 0.002f);
                totalBonus *= 1 - (level * 0.001f);
            }
            else
            {
                totalBonus *= 1 - (100 * 0.005f);
                totalBonus *= 1 - (200 * 0.002f);
                totalBonus *= 1 - (300 * 0.001f);
                totalBonus *= 1 - (level * 0.0005f);
            }
            //totalBonus -= GetLevel(skill) * 0.005f;
            totalBonus = Math.Max(totalBonus, 0.01f);
        }
        
        return totalBonus;
    }

    public bool HasLevelForRoadblock(string skill_string)
    {
        if (string.IsNullOrEmpty(skill_string))
        {
            return true;
        }

        string skillName = skill_string.Split(':')[0];
        int skillLevel = int.Parse(skill_string.Split(':')[1]);
        Skill skillToCheck = _skills.Values.FirstOrDefault(x => x.Name == skillName);
        if (skillToCheck != null)
        {
            return skillToCheck.LevelBoosted >= skillLevel;
        }
        Console.WriteLine($"Skill {skillName} was not found in player's list of skills.");
        return false;
    }

    public override string ToString()
    {
        string skillString = string.Empty;

        foreach (Skill skill in Skills.Values)
        {
            skillString += $"{skill.Name},{skill.Experience},{skill.Level}/";
        }
        skillString = skillString.Remove(skillString.Length - 1);
        return skillString;
    }
}

