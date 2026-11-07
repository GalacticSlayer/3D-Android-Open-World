using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// One depth sample per pixel, fixed-function alpha blending, no color copy or
// second camera. Runs before transparent water/fins, which fog their own surfaces.
internal sealed class UnderwaterFogPass : ScriptableRenderPass
{
    private readonly Material material;
    private sealed class PassData { public Material material; }

    public UnderwaterFogPass(Material material)
    {
        this.material = material;
        renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        ConfigureInput(ScriptableRenderPassInput.Depth);
    }

    public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
    {
        UniversalResourceData resources = frameData.Get<UniversalResourceData>();
        if (!resources.cameraDepthTexture.IsValid()) return;
        using (var builder = graph.AddRasterRenderPass<PassData>("Submerged scenery fade", out var data))
        {
            data.material = material;
            builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
            // ReadWrite preserves the existing color for fixed-function blending.
            builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                context.cmd.DrawProcedural(Matrix4x4.identity, pass.material, 0, MeshTopology.Triangles, 3, 1));
        }
    }

    // Support projects that still have URP Compatibility Mode enabled.
#pragma warning disable CS0672, CS0618
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer command = CommandBufferPool.Get("Submerged scenery fade");
        command.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);
        context.ExecuteCommandBuffer(command);
        CommandBufferPool.Release(command);
    }
#pragma warning restore CS0672, CS0618
}
