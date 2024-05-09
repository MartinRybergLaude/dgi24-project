using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size = 44)]
public struct Particle {
  public Vector3 CurrForce;
  public Vector3 Velocity;
  public Vector3 Position;
  public float Pressure;
  public float Density;
    
}

public class SPH : MonoBehaviour {

  [Header("particles")]
  public float R = 0.1f;
  public int AmountParticlesPerAxis = 10;
  public int TotalParticles = 1000;
  public Vector3 SpawnLoc;
  public Vector3 G;
  public bool Visible;

  public float Jitter = 0.1f;

  [Header("render")]
  public float RenderSize = 10f;
  public Mesh ParticleMesh;
  public Material ParticleMat;
  public Vector3 BoundingBox = new Vector3(10, 10, 10);

  [Header("compute")]
  public ComputeShader CustomShader;
  public Particle[] Particles;

  [Header("simulation")]
  public float Damping = -0.3f;
  public float Viscosity = -0.003f;
  public float Mass = 1f;
  public float GasConstant = 2f;
  public float RestingDensity = 1f;
  public float Timestep = 0.007f;

  public ComputeBuffer ParticlesBuffer;
  private int integrateKernel;
  private int computeForceKernel;
  private int densityPressureKernel;
  private ComputeBuffer argsBuffer;
  private static readonly int SizeProperty = Shader.PropertyToID("_size");
  private static readonly int ParticlesBufferProperty = Shader.PropertyToID("_particlesBuffer");

  private void OnDrawGizmos()
  {
    Gizmos.color = Color.red;
    Gizmos.DrawWireSphere(SpawnLoc, 0.1f);
    Gizmos.color = Color.cyan;
    Gizmos.DrawWireCube(Vector3.zero, BoundingBox);
  }

  private void Awake () {
    uint[] args = {
      ParticleMesh.GetIndexCount(0),
      (uint)AmountParticlesPerAxis,
      ParticleMesh.GetIndexStart(0),
      ParticleMesh.GetBaseVertex(0),
      0
    };

    ParticlesBuffer = new ComputeBuffer(AmountParticlesPerAxis, 44);
    argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

    ParticlesBuffer.SetData(Particles);    
    argsBuffer.SetData(args);

    integrateKernel = CustomShader.FindKernel("Integrate");
    computeForceKernel = CustomShader.FindKernel("ComputeForces");
    densityPressureKernel = CustomShader.FindKernel("ComputeDensityPressure");

    CustomShader.SetInt("length", AmountParticlesPerAxis);
    CustomShader.SetFloat("mass", Mass);
    CustomShader.SetFloat("viscosity", Viscosity);
    CustomShader.SetFloat("gasConstant", GasConstant);
    CustomShader.SetFloat("restDensity", RestingDensity);
    CustomShader.SetFloat("amping", Damping);
    CustomShader.SetFloat("pi", Mathf.PI);
    CustomShader.SetVector("boundingSize", BoundingBox);

    CustomShader.SetFloat("radius", R);
    CustomShader.SetFloat("radius2", R * R);
    CustomShader.SetFloat("radius3", R * R * R);
    CustomShader.SetFloat("radius4", R * R * R * R);
    CustomShader.SetFloat("radius5", R * R * R * R * R);

    CustomShader.SetBuffer(integrateKernel, "_particles", ParticlesBuffer);
    CustomShader.SetBuffer(computeForceKernel, "_particles", ParticlesBuffer);
    CustomShader.SetBuffer(densityPressureKernel, "_particles", ParticlesBuffer);
  }

  private void FixedUpdate() {
    CustomShader.Dispatch(densityPressureKernel, TotalParticles / 100, 1, 1);
    CustomShader.Dispatch(computeForceKernel, TotalParticles / 100, 1, 1);
    CustomShader.Dispatch(integrateKernel, TotalParticles / 100, 1, 1);
  }
  
  private void SpawnParticles () {
    List<Particle> particlesList = new List<Particle>();

    for (int x = 0; x < AmountParticlesPerAxis; x++)
    {
      for (int y = 0; y < AmountParticlesPerAxis; y++)
      {
        for (int z = 0; z < AmountParticlesPerAxis; z++)
        {

          Vector3 positionSpawn = SpawnLoc + new Vector3(x * R * 2, y * R * 2, z * R * 2);
          positionSpawn += Random.onUnitSphere * R * Jitter;

          Particle p = new Particle
          {
            Position = positionSpawn,
          };

          particlesList.Add(p);
        }
      }
    }

    Particles = particlesList.ToArray();
  }

  private void Update () {
    ParticleMat.SetFloat(SizeProperty, RenderSize);
    ParticleMat.SetBuffer(ParticlesBufferProperty, ParticlesBuffer);
    if (Visible) {
      Graphics.DrawMeshInstancedIndirect (
        ParticleMesh,
        0,
        ParticleMat,
        new Bounds(Vector3.zero, BoundingBox),
        argsBuffer,
        castShadows: UnityEngine.Rendering.ShadowCastingMode.Off
      );
    }
  }
}
