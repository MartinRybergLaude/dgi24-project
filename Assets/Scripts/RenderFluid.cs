using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluidRayMarching : MonoBehaviour
{
  public ComputeShader raymarching;
  public Camera cam;

  List<ComputeBuffer> buffersToDispose = new List<ComputeBuffer>();

  public SPH sph;

  RenderTexture target;

  [Header("Params")]
  public float viewRadius;
  public float blendStrength;
  public Color waterColor;

  public Color ambientLight;

  public Light lightSource;


  void InitRenderTexture()
  {
    if (target == null || target.width != cam.pixelWidth || target.height != cam.pixelHeight)
    {
      if (target != null)
      {
        target.Release();
      }

      cam.depthTextureMode = DepthTextureMode.Depth;

      target = new RenderTexture(cam.pixelWidth, cam.pixelHeight, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
      target.enableRandomWrite = true;
      target.Create();
    }
  }


  private bool render = false;

  public ComputeBuffer _particlesBuffer;

  public void Begin()
  {
    InitRenderTexture();
    raymarching.SetBuffer(0, "particles", sph.ParticlesBuffer);
    raymarching.SetInt("numParticles", sph.Particles.Length);
    raymarching.SetFloat("particleRadius", viewRadius);
    raymarching.SetFloat("blendStrength", blendStrength);
    raymarching.SetVector("waterColor", waterColor);
    raymarching.SetVector("_AmbientLight", ambientLight);
    raymarching.SetTextureFromGlobal(0, "_DepthTexture", "_CameraDepthTexture");
    render = true;
  }


  void OnRenderImage(RenderTexture source, RenderTexture destination)
  {

    if (!render)
    {
      Begin();
    }

    if (render)
    {

      raymarching.SetVector("_Light", lightSource.transform.forward);

      raymarching.SetTexture(0, "Source", source);
      raymarching.SetTexture(0, "Destination", target);
      raymarching.SetVector("_CameraPos", cam.transform.position);
      raymarching.SetMatrix("_CameraToWorld", cam.cameraToWorldMatrix);
      raymarching.SetMatrix("_CameraInverseProjection", cam.projectionMatrix.inverse);

      int threadGroupsX = Mathf.CeilToInt(cam.pixelWidth / 8.0f);
      int threadGroupsY = Mathf.CeilToInt(cam.pixelHeight / 8.0f);
      raymarching.Dispatch(0, threadGroupsX, threadGroupsY, 1);

      Graphics.Blit(target, destination);
    }
  }

}
