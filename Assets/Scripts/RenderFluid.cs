using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluidRayMarching : MonoBehaviour
{
  [Header("dependencies")]
  public ComputeShader CustomShader;
  public Camera Camera;

  public SPH Sph;


  [Header("params")]
  public Color Color;

  public Color AmbientLight;

  public Light LightSource;



  float r = 0.01f;
  float blend = 0.5f;
  RenderTexture texture;

  bool enableRendering = false;
  bool isInitializing = false;

  public ComputeBuffer _particlesBuffer;

  public void Initialize()
  {
    // Load the render texture
    if (texture != null && texture.width == Camera.pixelWidth && texture.height == Camera.pixelHeight)
    {
      return;
    }

    if (texture != null)
    {
      texture.Release();
    }

    Camera.depthTextureMode = DepthTextureMode.Depth;

    texture = new RenderTexture(Camera.pixelWidth, Camera.pixelHeight, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
    texture.enableRandomWrite = true;
    texture.Create();

    // Set shader parameters
    CustomShader.SetBuffer(0, "particles", Sph.ParticlesBuffer);
    CustomShader.SetInt("numParticles", Sph.Particles.Length);
    CustomShader.SetFloat("particleRadius", r);
    CustomShader.SetFloat("blendStrength", blend);
    CustomShader.SetVector("waterColor", Color);
    CustomShader.SetVector("_AmbientLight", AmbientLight);
    CustomShader.SetTextureFromGlobal(0, "_DepthTexture", "_CameraDepthTexture");

    enableRendering = true;
  }


  void OnRenderImage(RenderTexture source, RenderTexture destination)
  {
    if (!enableRendering)
    {
      Initialize();
    }

    CustomShader.SetVector("_Light", LightSource.transform.forward);

    CustomShader.SetTexture(0, "Source", source);
    CustomShader.SetTexture(0, "Destination", texture);
    CustomShader.SetVector("_CameraPos", Camera.transform.position);
    CustomShader.SetMatrix("_CameraToWorld", Camera.cameraToWorldMatrix);
    CustomShader.SetMatrix("_CameraInverseProjection", Camera.projectionMatrix.inverse);

    int threadGroupsX = Mathf.CeilToInt(Camera.pixelWidth / 8.0f);
    int threadGroupsY = Mathf.CeilToInt(Camera.pixelHeight / 8.0f);
    CustomShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

    Graphics.Blit(texture, destination);
  }
}
