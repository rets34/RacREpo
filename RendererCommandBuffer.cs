using System.Collections.Generic;

public class RendererCommandBuffer
{
    private List<Mesh> drawCommands = new();

    public void Submit(Mesh mesh)
    {
        drawCommands.Add(mesh);
    }

    public List<Mesh> GetCommands()
    {
        return drawCommands;
    }

    public void Clear()
    {
        drawCommands.Clear();
    }
}