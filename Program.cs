using System.Windows.Forms;

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
        Application.Run(new Forms.MenuPrincipal());
    }
}
