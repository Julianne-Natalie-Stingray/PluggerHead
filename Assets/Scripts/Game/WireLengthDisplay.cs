using System.Globalization;
using UnityEngine;

/// <summary>Displays the carried wire's remaining routed length after the environment updates.
/// 在环境路径更新后显示当前持线的剩余长度；仅查询，不改变路径或计分。
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class WireLengthDisplay : MonoBehaviour
{
    [SerializeField] private TMPro.TMP_Text remainingLengthText;

    private void LateUpdate()
    {
        if (remainingLengthText == null)
        {
            return;
        }

        EnvironmentFacade environment = EnvironmentFacade.ForScene(gameObject.scene);
        Wire wire = environment != null ? environment.HeldWire : null;
        string value = "--";
        if (wire != null && wire.IsHeld)
        {
            if (wire.MaxLength <= 0f)
            {
                value = "Unlimited";
            }
            else if (environment.RoutingTilemap != null)
            {
                float remaining = Mathf.Max(0f,
                    wire.MaxLength - wire.TilePath.GetLength(environment.RoutingTilemap));
                value = remaining.ToString("0.0", CultureInfo.InvariantCulture);
            }
        }

        string message = "Wire left: " + value;
        if (remainingLengthText.text != message)
        {
            remainingLengthText.text = message;
        }
    }
}
