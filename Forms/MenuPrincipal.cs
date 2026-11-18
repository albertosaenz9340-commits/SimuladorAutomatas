using System;
using System.Drawing;
using System.Windows.Forms;
using SimuladorAutomatas.Models;
using SimuladorAutomatas.Services;
using SimuladorAutomatas.UI;

namespace SimuladorAutomatas.Forms;

/// <summary>Menú principal con acceso a los tres módulos de la aplicación.</summary>
public sealed class MenuPrincipal : Form
{
    private readonly Label _estadoActual;

    public MenuPrincipal()
    {
        Text = "Simulador de Autómatas Finitos Deterministas";
        Size = new Size(820, 560);
        MinimumSize = new Size(720, 500);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Estilos.Fondo;
        Font = Estilos.FuenteNormal;

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(28, 22, 28, 20),
            AutoScroll = true
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 8));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 74));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        raiz.Controls.Add(Estilos.CrearCabecera(
            "Simulador de Autómatas Finitos Deterministas",
            "Construya un AFD, visualice su diagrama y analice el lenguaje que reconoce."), 0, 0);

        var botones = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(60, 4, 60, 4),
            MinimumSize = new Size(0, 210)
        };
        botones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        for (int i = 0; i < 3; i++)
        {
            botones.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        }

        var btnConstruir = Estilos.CrearBoton("1   CONSTRUIR AUTÓMATA");
        btnConstruir.Click += (_, _) => Abrir(new ConstructorAutomata());
        botones.Controls.Add(btnConstruir, 0, 0);

        var btnBuscar = Estilos.CrearBoton("2   BUSCAR PALABRAS ACEPTADAS");
        btnBuscar.Click += (_, _) => AbrirModulo(() => new BusquedaPalabras());
        botones.Controls.Add(btnBuscar, 0, 1);

        var btnReconocer = Estilos.CrearBoton("3   RECONOCER UNA PALABRA");
        btnReconocer.Click += (_, _) => AbrirModulo(() => new ReconocimientoPalabras());
        botones.Controls.Add(btnReconocer, 0, 2);
        raiz.Controls.Add(botones, 0, 2);

        _estadoActual = new Label
        {
            Dock = DockStyle.Fill,
            Font = Estilos.FuenteNegrita,
            ForeColor = Estilos.Primario,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(230, 239, 249)
        };
        raiz.Controls.Add(_estadoActual, 0, 3);

        var btnSalir = Estilos.CrearBoton("Salir", secundario: true);
        btnSalir.Click += (_, _) => Close();
        raiz.Controls.Add(btnSalir, 0, 4);

        Controls.Add(raiz);
        AutomataService.Instancia.AutomataCambiado += (_, _) => ActualizarEstado();
        ActualizarEstado();
    }

    private void ActualizarEstado()
    {
        Automata? automata = AutomataService.Instancia.Actual;
        _estadoActual.Text = automata is null
            ? "Estado: no hay un autómata construido. Comience con el módulo 1."
            : $"Autómata actual: {automata.Nombre}   |   |Q| = {automata.CantidadEstados}, |Σ| = {automata.Alfabeto.Count}, |F| = {automata.EstadosFinales.Count}";
    }

    private static void Abrir(Form formulario)
    {
        using (formulario)
        {
            formulario.ShowDialog();
        }
    }

    private static void AbrirModulo(Func<Form> fabrica)
    {
        if (!AutomataService.Instancia.HayAutomata)
        {
            MessageBox.Show(
                "Todavía no se ha construido ningún autómata.\n" +
                "Use el módulo 1 (Construir autómata) antes de acceder a éste módulo.",
                "Autómata no disponible",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Abrir(fabrica());
    }
}
