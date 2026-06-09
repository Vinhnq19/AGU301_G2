using System;
using System.Collections.Generic;

namespace DungeonBuilder.Core.Enums
{
    public static class ResourceTypeUtility
    {
        private static readonly IReadOnlyList<ResourceType> Values = Array.AsReadOnly(BuildValues());

        public static IReadOnlyList<ResourceType> All => Values;

        public static bool IsValid(ResourceType type)
        {
            return Enum.IsDefined(typeof(ResourceType), type) && type != ResourceType.MAX;
        }

        private static ResourceType[] BuildValues()
        {
            var values = new List<ResourceType>();
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                if (IsValid(type))
                {
                    values.Add(type);
                }
            }

            return values.ToArray();
        }
    }
}
