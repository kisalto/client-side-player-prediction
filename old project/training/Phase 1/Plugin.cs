using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TelemetryMod
{
    // O atributo BepInPlugin é OBRIGATÓRIO para o BepInEx carregar a DLL
    [BepInPlugin("com.seu_nome.telemetrylogger", "Telemetry Logger", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            // Inscreve-se no evento de carregamento de cena
            // Isso garante que o logger só seja injetado quando uma fase real carregar,
            // evitando que ele quebre no Menu Principal onde o PlayerStateMachine não existe.
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            Logger.LogInfo($"Plugin Telemetry Logger carregado!");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Verifica se existe um jogador nesta cena (para não rodar no menu principal)
            if (Object.FindFirstObjectByType<PlayerStateMachine>() != null)
            {
                // Equivalente a criar um GameObject vazio na hierarchy
                GameObject loggerObject = new GameObject("TelemetryLogger_Injected");
                
                // Equivalente a arrastar o script para o GameObject
                loggerObject.AddComponent<TelemetryLogger>();
                
                Logger.LogInfo($"TelemetryLogger injetado na cena: {scene.name}");
            }
        }
    }
}