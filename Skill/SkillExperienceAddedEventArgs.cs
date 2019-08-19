using System;

public class SkillExperienceAddedEventArgs : EventArgs
{
    public Skill Skill { get; }
    public SkillType SkillType => Skill.SkillType;
    public long Amount { get; }

    public SkillExperienceAddedEventArgs(Skill skill, long amount)
    {
        Skill = skill;
        Amount = amount;
    }
}
