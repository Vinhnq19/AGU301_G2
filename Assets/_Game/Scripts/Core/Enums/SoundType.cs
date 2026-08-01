namespace DungeonBuilder.Core.Enums
{
    public enum SoundType
    {
        None = 0,

        // BGM

        BGM_Main_Menu = 100,
        BGM_Prep_Phase = 101,
        BGM_Combat_Phase = 102,
        BGM_Boss_Fight = 103,
        BGM_Victory = 104,
        BGM_Defeat = 105,

        // SFX

        SFX_Core = 200,
        SFX_Hero_Footstep = 201,
        SFX_Hero_Hurt = 202,
        SFX_Hero_Death = 203,
        SFX_Tool_Chop = 204,
        SFX_Tool_Mine_Metal = 205, // Renamed from SFX_Tool_Mine
        SFX_Tool_Mine_Stone = 206, // Added
        SFX_Skill_Stun = 207,
        SFX_Build_Place = 208,
        SFX_Build_Upgrade = 209,
        SFX_Build_Sell = 210,
        SFX_Arrow_Tower = 211,
        SFX_Canon_Tower = 212,
        SFX_Laser_Tower = 213,
        SFX_Frost_Tower = 214,
        SFX_Spike_Trap = 215,
        SFX_Item_Pickup = 216,
        SFX_Shop_Buy = 217,
        SFX_Warning_Timer = 218,
        SFX_Click = 219,
        SFX_Enemy_Die = 220, // Added
        SFX_Canon_Boom = 221, // Added
        SFX_Hero_Dash = 222, // Added
        SFX_Error = 223, // Added
        SFX_Get_Coins = 224, // Added
        SFX_Item_Magnet = 225 // Tiếng item bị hút về phía player (khác với SFX_Item_Pickup lúc nhặt)
    }
}
