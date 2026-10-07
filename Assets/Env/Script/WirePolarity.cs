using System;

/// <summary>
/// Electrical property of one wire, and of the interfaces that accept it.
/// Subsystem: Environment.
/// 一条线的电性, 也是带电接口所能接受的电性.
/// Subsystem 归属: Environment.
/// </summary>

public enum WirePolarity
{
    None = 0,
    Live = 1,
    Neutral = 2,
    Ground = 4
}
