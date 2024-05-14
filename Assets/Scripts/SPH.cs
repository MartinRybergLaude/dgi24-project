using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using System.Linq;

[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size = 44)]
public struct Particle
{
  public Vector3 CurrForce;
  public Vector3 Velocity;
  public Vector3 Position;
  public float Pressure;
  public float Density;

}

public class SPH : MonoBehaviour
{
  [Header("collision")]
  public Transform collisionSphere;

  [Header("particles")]
  public float R = 0.1f;
  public Vector3Int AmountParticles = new Vector3Int(10, 10, 10);
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
  private int hashParticleKernel;
  private int sortKernel;
  private int offsetKernel;
  private ComputeBuffer argsBuffer;
  private ComputeBuffer indexBuffer;
  private ComputeBuffer cellIndexBuffer;
  private ComputeBuffer cellOffsetBuffer;
  private static readonly int SizeProperty = Shader.PropertyToID("_size");
  private static readonly int ParticlesBufferProperty = Shader.PropertyToID("_particlesBuffer");
  private int totalParticles
  {
    get
    {
      return AmountParticles.x * AmountParticles.y * AmountParticles.z;
    }
  }

  private void OnDrawGizmos()
  {
    Gizmos.color = Color.red;
    Gizmos.DrawWireSphere(SpawnLoc, 0.1f);
    Gizmos.color = Color.cyan;
    Gizmos.DrawWireCube(Vector3.zero, BoundingBox);
  }

  private void Awake()
  {
    SpawnParticles();

    uint[] args = {
      ParticleMesh.GetIndexCount(0),
      (uint)totalParticles,
      ParticleMesh.GetIndexStart(0),
      ParticleMesh.GetBaseVertex(0),
      0
    };

    ParticlesBuffer = new ComputeBuffer(totalParticles, 44);
    argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
    indexBuffer = new ComputeBuffer(totalParticles, 4);
    cellIndexBuffer = new ComputeBuffer(totalParticles, 4);
    cellOffsetBuffer = new ComputeBuffer(totalParticles, 4);
    uint[] particleIndexArray = Enumerable.Range(0, totalParticles).Select(i => (uint)i).ToArray();
    
    ParticlesBuffer.SetData(Particles);
    argsBuffer.SetData(args);
    indexBuffer.SetData(particleIndexArray);

    integrateKernel = CustomShader.FindKernel("Integrate");
    computeForceKernel = CustomShader.FindKernel("ComputeForces");
    densityPressureKernel = CustomShader.FindKernel("ComputeDensityPressure");
    hashParticleKernel = CustomShader.FindKernel("HashParticles");
    sortKernel = CustomShader.FindKernel("BitonicSort");
    offsetKernel = CustomShader.FindKernel("CalculateCellOffsets");

    CustomShader.SetInt("particleLength", totalParticles);
    CustomShader.SetFloat("particleMass", Mass);
    CustomShader.SetFloat("viscosity", Viscosity);
    CustomShader.SetFloat("gasConstant", GasConstant);
    CustomShader.SetFloat("restDensity", RestingDensity);
    CustomShader.SetFloat("boundDamping", Damping);
    CustomShader.SetFloat("pi", Mathf.PI);
    CustomShader.SetVector("boxSize", BoundingBox);

    CustomShader.SetFloat("radius", R);
    CustomShader.SetFloat("radius2", R * R);
    CustomShader.SetFloat("radius3", R * R * R);
    CustomShader.SetFloat("radius4", R * R * R * R);
    CustomShader.SetFloat("radius5", R * R * R * R * R);

    CustomShader.SetBuffer(integrateKernel, "_particles", ParticlesBuffer);

    CustomShader.SetBuffer(computeForceKernel, "_particles", ParticlesBuffer);
    CustomShader.SetBuffer(computeForceKernel, "_particleIndexArray", indexBuffer);
    CustomShader.SetBuffer(computeForceKernel, "_cellIndexArray", cellIndexBuffer);
    CustomShader.SetBuffer(computeForceKernel, "_cellOffset", cellOffsetBuffer);

    CustomShader.SetBuffer(densityPressureKernel, "_particles", ParticlesBuffer);
    CustomShader.SetBuffer(densityPressureKernel, "_particleIndexArray", indexBuffer);
    CustomShader.SetBuffer(densityPressureKernel, "_cellIndexArray", cellIndexBuffer);
    CustomShader.SetBuffer(densityPressureKernel, "_cellOffset", cellOffsetBuffer);

    CustomShader.SetBuffer(hashParticleKernel, "_particles", ParticlesBuffer);
    CustomShader.SetBuffer(hashParticleKernel, "_particleIndexArray", indexBuffer);
    CustomShader.SetBuffer(hashParticleKernel, "_cellIndexArray", cellIndexBuffer);
    CustomShader.SetBuffer(hashParticleKernel, "_cellOffset", cellOffsetBuffer);

    CustomShader.SetBuffer(sortKernel, "_particleIndexArray", indexBuffer);
    CustomShader.SetBuffer(sortKernel, "_cellIndexArray", cellIndexBuffer);

    CustomShader.SetBuffer(offsetKernel, "_particleIndexArray", indexBuffer);
    CustomShader.SetBuffer(offsetKernel, "_cellIndexArray", cellIndexBuffer);
    CustomShader.SetBuffer(offsetKernel, "_cellOffset", cellOffsetBuffer);

  }

  private void FixedUpdate()
  {
    CustomShader.SetVector("boxSize", BoundingBox);
    CustomShader.SetFloat("timestep", Timestep);
    CustomShader.SetVector("spherePos", collisionSphere.transform.position);
    CustomShader.SetFloat("sphereRadius", collisionSphere.transform.localScale.x / 2);
    
    CustomShader.Dispatch(hashParticleKernel, totalParticles / 256, 1, 1);
    
    for (var d = 2; d <= totalParticles; d <<= 1) {
      CustomShader.SetInt("sortDim", d);
      for (var b = d >> 1; b > 0; b >>= 1) {
        CustomShader.SetInt("sortBlock", b);
        CustomShader.Dispatch(sortKernel, totalParticles/256, 1, 1);
      }
    }
    
    CustomShader.Dispatch(offsetKernel, totalParticles/256, 1, 1);
    CustomShader.Dispatch(densityPressureKernel, totalParticles / 256, 1, 1);
    CustomShader.Dispatch(computeForceKernel, totalParticles / 256, 1, 1);
    CustomShader.Dispatch(integrateKernel, totalParticles / 256, 1, 1);
  }

  private void SpawnParticles()
  {
    List<Particle> particlesList = new List<Particle>();

    for (int x = 0; x < AmountParticles.x; x++)
    {
      for (int y = 0; y < AmountParticles.y; y++)
      {
        for (int z = 0; z < AmountParticles.z; z++)
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

  private void Update()
  {
    ParticleMat.SetFloat(SizeProperty, RenderSize);
    ParticleMat.SetBuffer(ParticlesBufferProperty, ParticlesBuffer);
    if (Visible)
    {
      Graphics.DrawMeshInstancedIndirect(
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
