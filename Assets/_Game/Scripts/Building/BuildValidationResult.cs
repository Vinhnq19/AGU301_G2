namespace DungeonBuilder.Building
{
    public enum BuildValidationCode
    {
        Allowed,
        NotBuildPhase,
        SenderPlayerNotFound,
        OutOfRange,
        InvalidGridPosition,
        TowerNotFound
    }

    public readonly struct BuildValidationResult
    {
        public static BuildValidationResult Allowed => new(BuildValidationCode.Allowed);

        public BuildValidationCode Code { get; }
        public bool IsAllowed => Code == BuildValidationCode.Allowed;

        public BuildValidationResult(BuildValidationCode code)
        {
            Code = code;
        }
    }
}
