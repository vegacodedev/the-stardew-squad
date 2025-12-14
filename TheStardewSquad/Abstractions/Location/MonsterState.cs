namespace TheStardewSquad.Abstractions.Location
{
    /// <summary>
    /// Represents the type of monster for testing purposes.
    /// </summary>
    public enum MonsterType
    {
        Generic,
        Bug,
        RockCrab,
        Mummy,
        Duggy
    }

    /// <summary>
    /// Represents the state of a monster for testing combat logic.
    /// Contains flags for various untargetable states.
    /// </summary>
    public struct MonsterState
    {
        public MonsterType Type { get; set; }
        public bool IsArmoredBug { get; set; }
        public bool IsHidingCrab { get; set; }
        public bool IsRevivingMummy { get; set; }
        public bool IsHidingDuggy { get; set; }

        /// <summary>
        /// Gets whether this monster can be targeted for attack.
        /// A monster is targetable if it's not in any special untargetable state.
        /// </summary>
        public bool IsTargetable => !IsArmoredBug && !IsHidingCrab &&
                                     !IsRevivingMummy && !IsHidingDuggy;

        /// <summary>
        /// Creates a targetable monster with the specified type.
        /// </summary>
        public static MonsterState Targetable(MonsterType type = MonsterType.Generic)
        {
            return new MonsterState { Type = type };
        }

        /// <summary>
        /// Creates an armored bug (untargetable).
        /// </summary>
        public static MonsterState ArmoredBug()
        {
            return new MonsterState { Type = MonsterType.Bug, IsArmoredBug = true };
        }

        /// <summary>
        /// Creates a hiding rock crab (untargetable).
        /// </summary>
        public static MonsterState HidingCrab()
        {
            return new MonsterState { Type = MonsterType.RockCrab, IsHidingCrab = true };
        }

        /// <summary>
        /// Creates a reviving mummy (untargetable).
        /// </summary>
        public static MonsterState RevivingMummy()
        {
            return new MonsterState { Type = MonsterType.Mummy, IsRevivingMummy = true };
        }

        /// <summary>
        /// Creates a hiding duggy (untargetable).
        /// </summary>
        public static MonsterState HidingDuggy()
        {
            return new MonsterState { Type = MonsterType.Duggy, IsHidingDuggy = true };
        }
    }
}
