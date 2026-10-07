using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unique identifier for each clip.
/// TODO: Implement a custom "Identifier" package. Current implementation stays naive.
/// </summary>
public enum AudioId
{
    DefaultSfx = 0,
    DefaultOst = 1,
    MouseClick = 2,
    GameMainMenu = 3,
    PlugIn = 4,
    PlugOut = 5,
    GameStart = 6,
    Activated = 7,
    GroundStepFront = 8,
    GroundStepBack = 9,
    MetalStepFront = 10,
    MetalStepBack = 11,

    // Optional audio hooks use None without submitting a playback request.
    None = -1
}
