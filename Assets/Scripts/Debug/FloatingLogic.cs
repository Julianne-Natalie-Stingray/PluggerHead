using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.UIElements;

public class FloatingLogic : MonoBehaviour
{
    [System.Serializable]
    private class DriftSettings
    {
        [SerializeField, Range(1f, 15f), Tooltip("Evaluated in seconds")] public float period = 15f;
        [SerializeField, Range(0f, Mathf.PI * 2)] public float phaseOffset = 0f;
        [SerializeField, Range(0f, 1f)] public float xAmplitude = 0.75f;
        [SerializeField, Range(0f, 1f)] public float yAmplitude = 0.75f;
        [SerializeField] public bool xYSync = true;
    }

    [System.Serializable]
    private class OscillationSettings
    {
        [SerializeField, Range(0f, 1f)] public float amplitude = 0.5f;
        [SerializeField, Range(0f, 1f)] public float frequency = 0.5f;
    }

    [System.Serializable]
    private class VisualizationSettings
    {
        [SerializeField, Range(0f, 1f)] public float radius = 1f;
    }

    [System.Serializable]
    private class RotationSettings
    {
        [SerializeField, Range(-1f, 1f)] public float angularVelocity = 0.25f;
    }

    //External Parameters:
    [Header("External Parameters")]
    [SerializeField, Range(0.01f, 1f)] float mass = 1f;
    [SerializeField] DriftSettings drift;
    [SerializeField] OscillationSettings oscillation;
    [SerializeField] RotationSettings rotation;
    [SerializeField] VisualizationSettings visualization;
    [Header("--------------------------------------------")]

    //Internal States:
    [Header("Internal States")]
    [SerializeField] Vector3 initialPosition;
    [SerializeField] float randomSeed;

    private float _mass;

    private float _xDriftAmplitude;
    private float _yDriftAmplitude;
    private float _DriftPeriod;

    private float _OscillationAmplitude;
    private float _OscillationFrequency;

    private float _AngularVelocity;

    private float _previousXAmplitude;
    private float _previousYAmplitude;

    //Internal Scalers:
    private readonly float _MassScaler = 25f;

    private readonly float _DriftAmplitudeScaler = 0.25f;

    private readonly float _OscillationAmplitudeScaler = 2f;
    private readonly float _OscillationFrequencyScaler = 1.2f;

    private readonly float _angularVelocityScaler = 20f;

    private readonly float _radiusScaler = 1.5f;

    private void Awake()
    {
        CalculateEffectiveValues();
        initialPosition = transform.localPosition;
        randomSeed = UnityEngine.Random.value;

    }

    private void Update()
    {
        float t = Time.time;
        Vector3 finalOffset = Drift(t) + Oscillate(t);
        transform.localPosition = initialPosition + finalOffset;
        BaseRotation();
    }

    private void OnValidate()
    {
        if (drift.xYSync)
        {
            if (drift.xAmplitude != _previousXAmplitude)
            {
                drift.yAmplitude = drift.xAmplitude;
            }
            else if (drift.yAmplitude != _previousYAmplitude)
            {
                drift.xAmplitude = drift.yAmplitude;
            }
        }
        _previousXAmplitude = drift.xAmplitude;
        _previousYAmplitude = drift.yAmplitude;

        CalculateEffectiveValues();
    }

    private Vector3 Drift(float t)
    {
        float k = 2 * Mathf.PI / _DriftPeriod;
        float dir = randomSeed < 0.5 ? -1 : 1;

        float x = Mathf.Cos(k * dir * t) * _xDriftAmplitude;
        float y = Mathf.Sin(k * (dir * t + drift.phaseOffset)) * _yDriftAmplitude;

        return new Vector3(x, y, 0);
    }

    private Vector3 Oscillate(float t)
    {
        // radial oscillation using Perlin noise
        float randX = Mathf.PerlinNoise(_OscillationFrequency * t, randomSeed);
        float randY = Mathf.PerlinNoise(randomSeed, _OscillationFrequency * t);
        randX = Mathf.Clamp01(randX) - 0.5f;
        randY = Mathf.Clamp01(randY) - 0.5f;
        return _OscillationAmplitude * new Vector3(randX, randY, 0);
    }

    private void BaseRotation()
    {
        transform.RotateAround(transform.localPosition, Vector3.forward, _AngularVelocity * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(initialPosition, transform.localPosition);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(initialPosition, visualization.radius * _radiusScaler);
    }

    private void CalculateEffectiveValues()
    {
        AvoidDivideByZero();

        _mass = mass * _MassScaler;

        _xDriftAmplitude = drift.xAmplitude * _DriftAmplitudeScaler;
        _yDriftAmplitude = drift.yAmplitude * _DriftAmplitudeScaler;
        _DriftPeriod = drift.period;

        _OscillationAmplitude = oscillation.amplitude * _OscillationAmplitudeScaler * Mathf.Pow(_mass, -0.5f);
        _OscillationFrequency = oscillation.frequency * _OscillationFrequencyScaler;

        _AngularVelocity = rotation.angularVelocity * _angularVelocityScaler;
    }

    private void AvoidDivideByZero()
    {
        if (mass <= 0) mass = 0.01f;
        if (drift.period <= 0) drift.period = 0.01f;
    }

    private Vector3 _RadialOscillate(float t)
    {
        // radial oscillation using Perlin noise
        float randValue = Mathf.PerlinNoise(t, randomSeed);
        randValue = Mathf.Clamp01(randValue) - 0.5f;
        return _OscillationAmplitude * randValue * transform.localPosition.normalized;
    }
}