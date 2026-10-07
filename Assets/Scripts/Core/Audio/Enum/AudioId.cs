using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unique identifier for each clip.
/// TODO: Implement a custom "Identifier" package. Current implementation stays naive.
/// </summary>
public enum AudioId
{
    DefaultSfx,
    DefaultOst,

    MouseClick,
    
    plugin,

    plugout,

    gamestart,

    activated,

    groundstepfront,

    groundstepback,

    metalstepfront,

    metalstepback,

    
    // Optional audio hooks use None without submitting a playback request.
    None = -1
}
