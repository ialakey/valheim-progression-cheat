using System;
using System.Reflection;

namespace ProgressionCheat
{
    /// <summary>
    /// Reads and writes raw skill levels.
    ///
    /// Skills.GetSkillLevel() is not usable here: it floors the value and lets status
    /// effects modify it, so it does not report what is actually stored. The real level
    /// lives in Skills.Skill.m_level, reached through the private Skills.GetSkill(),
    /// which also creates the entry for a skill the character has never used.
    /// </summary>
    internal static class SkillAccess
    {
        private static readonly MethodInfo GetSkillMethod = typeof(Skills).GetMethod(
            "GetSkill", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo LevelField = typeof(Skills.Skill).GetField(
            "m_level", BindingFlags.Instance | BindingFlags.Public);

        private static readonly object[] Args = new object[1];

        public static bool Available
        {
            get { return GetSkillMethod != null && LevelField != null; }
        }

        private static Skills.Skill GetSkill(Skills skills, Skills.SkillType type)
        {
            if (skills == null || !Available)
                return null;

            Args[0] = type;
            return GetSkillMethod.Invoke(skills, Args) as Skills.Skill;
        }

        public static float GetLevel(Skills skills, Skills.SkillType type)
        {
            Skills.Skill skill = GetSkill(skills, type);
            if (skill == null)
                return 0f;
            return (float)LevelField.GetValue(skill);
        }

        public static void SetLevel(Skills skills, Skills.SkillType type, float level)
        {
            Skills.Skill skill = GetSkill(skills, type);
            if (skill == null)
                return;
            LevelField.SetValue(skill, level);
        }
    }
}
