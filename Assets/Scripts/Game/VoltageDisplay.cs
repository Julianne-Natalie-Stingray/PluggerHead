using System.Globalization;
using UnityEngine;

/// <summary>Displays this scene's accumulated voltage drop and requirement.
/// 显示同场景累计降压量与需求；控制器应挂在保持启用的父对象上。
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class VoltageDisplay : MonoBehaviour
{
    [SerializeField] private TMPro.TMP_Text voltageText;

    private void LateUpdate()
    {
        if (voltageText == null)
        {
            return;
        }

        EnvironmentFacade environment = EnvironmentFacade.ForScene(gameObject.scene);
        bool visible = environment != null && environment.NeededVoltage > 0f;
        string message = visible
            ? "电压: " + environment.ReducedVoltage.ToString("0.##", CultureInfo.InvariantCulture)
                + "/" + environment.NeededVoltage.ToString("0.##", CultureInfo.InvariantCulture)
            : string.Empty;
        if (voltageText.text != message)
        {
            voltageText.text = message;
        }

        if (voltageText.gameObject.activeSelf != visible)
        {
            voltageText.gameObject.SetActive(visible);
        }
    }
}
