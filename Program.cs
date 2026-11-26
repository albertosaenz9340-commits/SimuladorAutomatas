using System.Windows.Forms;
using SimuladorAutomatas.Services;

namespace SimuladorAutomatas;

/// <summary>
/// Punto de entrada de la aplicación de escritorio.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Precargar el primer caso al iniciar para que los tres módulos estén listos.
        AutomataService.Instancia.Establecer(CasosPruebaService.ObtenerCasos()[0].CrearAutomata());

        Application.Run(new Forms.MenuPrincipal());
    }
}
