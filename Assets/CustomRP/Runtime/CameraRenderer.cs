using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    const string bufferName = "Render Camera";
    //只渲染DefaultUnlit
    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

    ScriptableRenderContext context;
    Camera camera;
    CullingResults cullingResults;

    CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName
    };

    Lighting lighting = new Lighting();

    public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing, ShadowSettings shadowSettings)
    {
        this.context = context;
        this.camera = camera;

        PrepareBuffer();
        //渲染场景中的UI几何图形
        PrepareForSceneWindow();

        //获取相机的裁剪参数，如果相机无法渲染，返回false
        if (!Cull(shadowSettings.maxDistance))
            return;

        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        //光照设置
        lighting.Setup(context, cullingResults, shadowSettings);
        buffer.EndSample(SampleName);

        Setup();
        //渲染可见物体天空盒，透明，不透明

        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);
        //渲染不支持的shader
        DrawUnsupportShaders();
        //渲染Gizmos
        DrawGizmos();
        Submit();
    }

    void Setup()
    {
        //设置相机参数
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;

        //清除缓冲区
        buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags == CameraClearFlags.Color,
                                    flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
    }

    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing)
    {
        //渲染的排序设置
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
        {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData = PerObjectData.Lightmaps
        };
        drawingSettings.SetShaderPassName(1, litShaderTagId);
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

        context.DrawSkybox(camera);

        //后渲染透明物体,否则会被天空盒挡住。
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        lighting.Cleanup();
        context.Submit();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    bool Cull(float maxShadowDistance)
    {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }

        return false;
    }
}
