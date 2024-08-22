﻿namespace RuiChen.AbpPro.LocalizationManagement
{
    public class LanguageTextDifference
    {
        public int Id { get; set; }
        public string CultureName { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public string ResourceName { get; set; }
        public string TargetCultureName { get; set; }
        public string TargetValue { get; set; }

        public LanguageTextDifference() { }
        public LanguageTextDifference(
            int id,
            string cultureName,
            string key,
            string value,
            string targetCultureName,
            string targetValue = null,
            string resourceName = null)
        {
            Id = id;
            Key = key;
            Value = value;
            CultureName = cultureName;
            TargetCultureName = targetCultureName;

            TargetValue = targetValue;
            ResourceName = resourceName;
        }
    }
}
