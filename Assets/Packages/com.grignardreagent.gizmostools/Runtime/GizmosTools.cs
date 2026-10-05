using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GizmosTools
{
    public static void DrawCircle(
        Vector3 center,
        float radius,
        Vector3 upAxis,
        int segments = 48)
    {
        if (radius <= 0f || segments < 3)
            return;

        if (upAxis.sqrMagnitude < Mathf.Epsilon)
            upAxis = Vector3.up;

        upAxis.Normalize();

        Vector3 tangent = Vector3.Cross(
            upAxis,
            Mathf.Abs(Vector3.Dot(upAxis, Vector3.up)) < 0.999f
                ? Vector3.up
                : Vector3.right
        ).normalized;

        Vector3 bitangent = Vector3.Cross(upAxis, tangent);

        Vector3 previous = center + tangent * radius;

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * (2f * Mathf.PI / segments);

            Vector3 offset =
                Mathf.Cos(angle) * radius * tangent +
                bitangent * Mathf.Sin(angle) * radius;

            Vector3 current = center + offset;

            Gizmos.DrawLine(previous, current);
            previous = current;
        }
    }
}
