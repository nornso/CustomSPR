using System;
using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    const string bufferName = "Shadows";
    const int maxShadowDirectionalLightCount = 4, maxCascades = 4;
    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
               dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
               cascadeCountId = Shader.PropertyToID("_CascadeCount"),
               cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
               cascadeDataId = Shader.PropertyToID("_CascadeData"),
               shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades],
                     cascadeData = new Vector4[maxCascades];

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowDirectionalLightCount * maxCascades];

    ScriptableRenderContext context;

    CullingResults cullingResults;

    ShadowSettings settings;

    int ShadowedDirectionalLightCount;

    struct shadowedDirectionalLight
    {
        public int visibleLightIndex;
    }

    shadowedDirectionalLight[] ShadowedDirectionalLights = new shadowedDirectionalLight[maxShadowDirectionalLightCount];

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings)
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;
        ShadowedDirectionalLightCount = 0;
    }

    public Vector2 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (ShadowedDirectionalLightCount < maxShadowDirectionalLightCount
            && light.shadows != LightShadows.None
            && light.shadowStrength > 0
            && cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
        {
            ShadowedDirectionalLights[ShadowedDirectionalLightCount] = new shadowedDirectionalLight { visibleLightIndex = visibleLightIndex };
            return new Vector2(light.shadowStrength, settings.directional.cascadeCount * ShadowedDirectionalLightCount++);
        }

        return Vector2.zero;
    }

    public void Render()
    {
        if (ShadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
    }

    //绘制阴影
    void RenderDirectionalShadows()
    {
        int atlasSize = (int)settings.directional.atlasSize;
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);

        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }

        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade, 1 / (1f - f * f)));
        buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
        buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    //绘制单个阴影
    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        shadowedDirectionalLight light = ShadowedDirectionalLights[index];
        var shadowSetting = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        int cascadeCount = settings.directional.cascadeCount;
        int tileoffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;

        for (int i = 0; i < cascadeCount; i++)
        {
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex, i, cascadeCount, ratios, tileSize,
                           0f, out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);

            if (index == 0)
            {
                SetCascaedData(i, splitData.cullingSphere, tileSize);
            }

            shadowSetting.splitData = splitData;
            int tileIndex = tileoffset + i;
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, SetTileViewport(tileIndex, split, tileSize), split);
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSetting);
        }
    }

    private static void SetCascaedData(int index, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;

        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere;

        cascadeData[index] = new Vector4(1f / cullingSphere.w, texelSize * 1.4142136f);
    }

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }

        float scale = 1f / split;
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);

        return m;
    }


    Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
        return offset;
    }

    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
