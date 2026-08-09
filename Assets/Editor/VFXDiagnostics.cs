using UnityEditor;
using UnityEngine;

public class VFXDiagnostics
{
    public static void RunCheck()
    {
        Debug.Log("--- INICIANDO DIAGNOSTICO VFX ---");
        
        // 1. Check Color Space
        Debug.Log("Color Space: " + PlayerSettings.colorSpace.ToString());
        if (PlayerSettings.colorSpace != ColorSpace.Linear)
        {
            Debug.LogError("VFX Graph REQUIERE Color Space Linear. Actualmente es: " + PlayerSettings.colorSpace);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            Debug.Log("Color Space cambiado a Linear auto-magicamente.");
        }

        // 2. Check SRP
        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
        {
            Debug.LogError("No hay URP Asset asignado en GraphicsSettings.");
        }
        else
        {
            Debug.Log("SRP actual: " + UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name);
        }
        
        Debug.Log("--- FIN DIAGNOSTICO ---");
    }
}
