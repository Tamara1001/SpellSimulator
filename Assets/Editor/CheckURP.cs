using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CheckURP
{
    public static void Check()
    {
        RenderPipelineAsset current = GraphicsSettings.currentRenderPipeline;
        if (current == null)
        {
            Debug.LogError("No Render Pipeline Asset assigned in Graphics Settings!");
        }
        else
        {
            Debug.Log("Current SRP: " + current.name);
        }
    }
}
