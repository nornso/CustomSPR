using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    const string bufferName = "Lighting";
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    const int maxDirLightCount = 4;

    static int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
               dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
               dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
               dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

    static Vector4[] dirLightColors = new Vector4[maxDirLightCount],
                     dirLightDirections = new Vector4[maxDirLightCount],
                     dirLightShadowData = new Vector4[maxDirLightCount];

    CullingResults cullingResults;
    Shadows shadows = new Shadows();
    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        shadows.Setup(context, cullingResults, shadowSettings);
        SetupLight();
        shadows.Render();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void Cleanup() 
    {
        shadows.Cleanup();
    }

    private void SetupLight()
    {
        int dirLIghtCount = 0;
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];

            if (visibleLight.lightType == LightType.Directional)
            {
                SetupDirectionalLight(dirLIghtCount++, ref visibleLight);

                if (dirLIghtCount >= maxDirLightCount)
                    break;
            }
        }

        buffer.SetGlobalInt(dirLightCountId, dirLIghtCount);
        buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
        buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
        buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
    }

    private void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
    {
        dirLightColors[index] = visibleLight.finalColor;
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light, index);
    }
}
