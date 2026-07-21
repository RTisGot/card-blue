public enum CardType
{
    Start,
    Goal,
    PathStraight,
    PathCorner,
    PathTJunction,
    PathCross,
    DeadEnd,
    ActionRepair,
    ActionSabotage,
    ActionMap,
    ActionFallingRocks,

    // L字
    URload,         // L字-1       
    DLload,         // L字-2       
    DRload,         // L字-3       
    ULload,         // L字-4
    // T字路
    DLRload,        // T字路(横)-1 
    ULRload,        // T字路(横)-2 
    UDRload,        // T字路(縦)-1 
    UDLload,        // T字路(縦)-2
    // 十字路・直線
    UDLRload,       // 十字路      
    LRload,         // 直線(横)    
    UDload,         // 直線(縦)
    // 行き止まり
    UDLdeadend,     // 右以外行き止まり
    ULRdeadend,     // 下以外行き止まり
    RDdeadend,      // 下右行き止まり
    LDdeadend,      // 下左行き止まり 
    LRdeadend,      // 左右行き止まり 
    Ldeadend,       // 左行き止まり   
    UDdeadend,      // 上下行き止まり 
    Udeadend,       // 上行き止まり   
    UDLRdeadend,    // 全方向行き止まり

    // 小物類有り
    DLloadHandkerchief,     // L字-2 ハンカチ
    DRloadPocketwatch,      // L字-3 懐中時計
    ULRloadBucket,          // T字路(横)-2 バケツ
    ULRloadMouse,           // T字路(横)-2 ネズミ
    UDLloadPot,             // T字路(縦)-2 ポット
    UDLloadShoe,            // T字路(縦)-2 靴
    UDLRloadBone,           // 十字路 骨
    UDLRloadCup,            // 十字路 カップ
    UDLRloadHat,            // 十字路 帽子
    LRloadSpoon,            // 直線(横) スプーン
    LRloadWheel,            // 直線(横) 車輪
    UDloadBucket,           // 直線(縦) バケツ
    UDLdeadendHedgehog,     // 右以外行き止まり ハリネズミ
    UDdeadendFriedegg,      // 上下行き止まり 目玉焼き
    // アクションカード
    Lanternrepaire, // ランタン修理
    Lanternban,     // ランタン破壊
    Pickaxerepaire, // つるはし修理
    Pickaxeban,     // つるはし破壊
    Railcarrepaire, // トロッコ修理
    Railcarban,     // トロッコ破壊
    Treasuremap,    // 宝の地図
    Fallingrocks,   // 落石
    GoalGold,        // 金塊入りゴール（公開後）
    GoalEmpty,       // 旧ハズレゴール（互換用）
    GoalEmptyTop,    // 上段のハズレ：上下
    GoalEmptyMiddle, // 中段のハズレ：上下向き
    GoalEmptyBottom  // 下段のハズレ：上下
}