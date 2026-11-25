using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SimuladorAutomatas.Models;
using SimuladorAutomatas.Services;
using SimuladorAutomatas.UI;

namespace SimuladorAutomatas.Forms;

/// <summary>
/// Módulo 2: muestra las cinco palabras aceptadas más cortas del autómata compartido.
/// </summary>
public sealed class BusquedaPalabras : Form
{
    private readonly DataGridView _tabla = Estilos.CrearTabla();
    private readonly TextBox _resumen = Estilos.CrearTextoInformativo();
    private readonly TextBox _mensaje = Estilos.CrearTextoInformativo();
    private readonly BusquedaPalabrasService _servicio = new();

    public BusquedaPalabras()
    {
        Text = "Módulo 2 — Búsqueda de palabras aceptadas";
        Size = new Size(900, 600);
        MinimumSize = new Size(760, 520);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Estilos.Fondo;
        Font = Estilos.FuenteNormal;

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(16, 12, 16, 14),
            AutoScroll = true
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        raiz.Controls.Add(Estilos.CrearCabecera(
            "Búsqueda de las cinco palabras más cortas",
            "Se recorre el autómata en orden de longitud y después según el orden de los símbolos del alfabeto."), 0, 0);

        var resumenPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(230, 239, 249) };
        _resumen.ForeColor = Estilos.Primario;
        _resumen.BackColor = Color.FromArgb(230, 239, 249);
        _resumen.Font = Estilos.FuenteNegrita;
        resumenPanel.Controls.Add(_resumen);
        raiz.Controls.Add(resumenPanel, 0, 1);

        ConfigurarTabla();
        raiz.Controls.Add(_tabla, 0, 2);

        var mensajePanel = new Panel { Dock = DockStyle.Fill };
        _mensaje.ForeColor = Estilos.Primario;
        _mensaje.BackColor = Estilos.Fondo;
        _mensaje.Font = Estilos.FuenteNegrita;
        mensajePanel.Controls.Add(_mensaje);
        raiz.Controls.Add(mensajePanel, 0, 3);

        var botones = Estilos.CrearRejilla(1, 2, 2);
        var btnBuscar = Estilos.CrearBoton("Buscar de nuevo");
        btnBuscar.Click += (_, _) => EjecutarBusqueda();
        botones.Controls.Add(btnBuscar, 0, 0);
        var btnCerrar = Estilos.CrearBoton("Cerrar", secundario: true);
        btnCerrar.Click += (_, _) => Close();
        botones.Controls.Add(btnCerrar, 1, 0);
        raiz.Controls.Add(botones, 0, 4);

        Controls.Add(raiz);
        Load += (_, _) =>
        {
            if (!AutomataService.Instancia.HayAutomata)
            {
                MessageBox.Show("Todavía no se ha construido ningún autómata.", "Autómata no disponible",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            EjecutarBusqueda();
        };
    }

    private void ConfigurarTabla()
    {
        _tabla.ReadOnly = true;
        _tabla.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#", Name = "Orden", Width = 45, MinimumWidth = 45 });
        _tabla.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Palabra", Name = "Palabra", MinimumWidth = 110 });
        _tabla.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Longitud |w|", Name = "Longitud", MinimumWidth = 100 });
        _tabla.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Recorrido de estados", Name = "Recorrido", MinimumWidth = 260 });
        _tabla.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Estado final", Name = "EstadoFinal", MinimumWidth = 100 });
    }

    private void EjecutarBusqueda()
    {
        Automata automata = AutomataService.Instancia.ObtenerActualOExcepcion();
        ConfiguracionAceptacion configuracion = AutomataService.Instancia.ObtenerConfiguracionOExcepcion();
        ResultadoBusqueda resultado = _servicio.Buscar(automata, configuracion);

        _resumen.Text = $"Autómata: {automata.Nombre}   |   Σ = {{{string.Join(", ", automata.Alfabeto)}}}   |   " +
                        $"longitud máxima = {BusquedaPalabrasService.LongitudMaxima}   |   máximo = {BusquedaPalabrasService.LimitePalabras} palabras";

        _tabla.Rows.Clear();
        int orden = 1;
        foreach (PalabraEncontrada palabra in resultado.Palabras)
        {
            string visible = palabra.Palabra.Length == 0 ? "ε" : palabra.Palabra;
            _tabla.Rows.Add(
                orden++,
                visible,
                palabra.Longitud.ToString(),
                string.Join(" → ", palabra.Recorrido),
                palabra.EstadoFinal);
        }

        _mensaje.Text = resultado.Palabras.Count == 0
            ? $"No se encontraron palabras aceptadas con longitud ≤ {resultado.LongitudMaxima}."
            : $"Se encontraron {resultado.Palabras.Count} palabra(s) aceptada(s). Orden: longitud ascendente y lexicográfico según Σ.";
        _mensaje.ForeColor = resultado.Palabras.Count == 0 ? Estilos.Error : Estilos.Exito;
    }
}
