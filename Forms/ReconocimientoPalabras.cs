using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SimuladorAutomatas.Models;
using SimuladorAutomatas.Services;
using SimuladorAutomatas.UI;

namespace SimuladorAutomatas.Forms;

/// <summary>
/// Módulo 3: comprueba si una palabra pertenece al lenguaje del autómata compartido.
/// Permite realizar tantas pruebas como se desee sin reconstruir el AFD.
/// </summary>
public sealed class ReconocimientoPalabras : Form
{
    private readonly ReconocimientoService _servicio = new();
    private readonly TextBox _txtPalabra = Estilos.CrearCaja();
    private readonly TextBox _txtSimbolos = Estilos.CrearCaja();
    private readonly TextBox _txtRecorrido = Estilos.CrearCaja();
    private readonly TextBox _txtEstadoFinal = Estilos.CrearCaja();
    private readonly TextBox _txtResultado = Estilos.CrearCaja();
    private readonly TextBox _resumen = Estilos.CrearTextoInformativo();
    private readonly TextBox _mensaje = Estilos.CrearTextoInformativo();

    public ReconocimientoPalabras()
    {
        Text = "Módulo 3 — Reconocimiento de palabras";
        Size = new Size(880, 620);
        MinimumSize = new Size(740, 540);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Estilos.Fondo;
        Font = Estilos.FuenteNormal;

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(16, 12, 16, 14),
            AutoScroll = true
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        raiz.Controls.Add(Estilos.CrearCabecera(
            "Reconocimiento de palabras",
            "Introduzca una palabra, observe los símbolos procesados y el recorrido completo de estados."), 0, 0);

        var panelResumen = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(230, 239, 249) };
        _resumen.ForeColor = Estilos.Primario;
        _resumen.BackColor = Color.FromArgb(230, 239, 249);
        _resumen.Font = Estilos.FuenteNegrita;
        panelResumen.Controls.Add(_resumen);
        raiz.Controls.Add(panelResumen, 0, 1);

        raiz.Controls.Add(CrearGrupoEntrada(), 0, 2);

        var panelMensaje = new Panel { Dock = DockStyle.Fill };
        _mensaje.BackColor = Estilos.Fondo;
        _mensaje.ForeColor = Estilos.TextoSecundario;
        panelMensaje.Controls.Add(_mensaje);
        raiz.Controls.Add(panelMensaje, 0, 3);

        raiz.Controls.Add(CrearGrupoResultado(), 0, 4);

        var botones = Estilos.CrearRejilla(1, 3, 2);
        var btnProcesar = Estilos.CrearBoton("PROCESAR PALABRA");
        btnProcesar.Click += Procesar_Click;
        botones.Controls.Add(btnProcesar, 0, 0);
        var btnLimpiar = Estilos.CrearBoton("Limpiar", secundario: true);
        btnLimpiar.Click += Limpiar_Click;
        botones.Controls.Add(btnLimpiar, 1, 0);
        var btnCerrar = Estilos.CrearBoton("Cerrar", secundario: true);
        btnCerrar.Click += (_, _) => Close();
        botones.Controls.Add(btnCerrar, 2, 0);
        raiz.Controls.Add(botones, 0, 5);

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

            Automata automata = AutomataService.Instancia.Actual!;
            ConfiguracionAceptacion configuracion = AutomataService.Instancia.ConfiguracionActual!;
            _resumen.Text = $"Autómata: {automata.Nombre}   |   Σ = {{{string.Join(", ", automata.Alfabeto)}}}   |   " +
                            $"q0 = {configuracion.EstadoInicial}   |   F = {{{string.Join(", ", configuracion.EstadosFinales)}}}";
            _mensaje.Text = "Escriba una palabra y pulse Procesar. Use ε (o deje el campo vacío) para la palabra vacía.";
        };

        _txtPalabra.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                Procesar_Click(this, EventArgs.Empty);
            }
        };
    }

    private GroupBox CrearGrupoEntrada()
    {
        var grupo = Estilos.CrearGrupo("Palabra a comprobar");
        grupo.Dock = DockStyle.Top;
        grupo.AutoSize = true;
        grupo.MinimumSize = new Size(0, 136);

        var rejilla = Estilos.CrearRejilla(2, 2);
        rejilla.Dock = DockStyle.Top;
        rejilla.AutoSize = true;
        rejilla.ColumnStyles[0] = new ColumnStyle(SizeType.AutoSize);
        rejilla.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 100f);
        rejilla.RowStyles[0] = new RowStyle(SizeType.AutoSize);
        rejilla.RowStyles[1] = new RowStyle(SizeType.AutoSize);

        rejilla.Controls.Add(Estilos.CrearEtiquetaAutomatica("Palabra w:", Estilos.FuenteNegrita), 0, 0);
        rejilla.Controls.Add(Estilos.CrearEtiquetaAutomatica("ε o λ = palabra vacía", Estilos.FuenteSubtitulo, Estilos.TextoSecundario), 1, 0);

        _txtPalabra.MinimumSize = new Size(0, 34);
        rejilla.Controls.Add(_txtPalabra, 0, 1);
        rejilla.SetColumnSpan(_txtPalabra, 2);

        grupo.Controls.Add(rejilla);
        return grupo;
    }

    private GroupBox CrearGrupoResultado()
    {
        var grupo = Estilos.CrearGrupo("Resultado del reconocimiento");
        grupo.MinimumSize = new Size(0, 220);
        var rejilla = Estilos.CrearRejilla(5, 2);
        for (int i = 0; i < 5; i++)
        {
            rejilla.RowStyles[i] = new RowStyle(SizeType.Percent, 20f);
        }
        rejilla.ColumnStyles[0] = new ColumnStyle(SizeType.AutoSize);
        rejilla.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 100f);

        rejilla.Controls.Add(Estilos.CrearEtiquetaAutomatica("Símbolos procesados:", Estilos.FuenteNegrita), 0, 0);
        rejilla.Controls.Add(_txtSimbolos, 1, 0);
        rejilla.Controls.Add(Estilos.CrearEtiquetaAutomatica("Recorrido de estados:", Estilos.FuenteNegrita), 0, 1);
        rejilla.Controls.Add(_txtRecorrido, 1, 1);
        rejilla.Controls.Add(Estilos.CrearEtiquetaAutomatica("Estado final alcanzado:", Estilos.FuenteNegrita), 0, 2);
        rejilla.Controls.Add(_txtEstadoFinal, 1, 2);
        rejilla.Controls.Add(Estilos.CrearEtiquetaAutomatica("Resultado:", Estilos.FuenteNegrita), 0, 3);
        rejilla.Controls.Add(_txtResultado, 1, 3);
        rejilla.Controls.Add(Estilos.CrearEtiquetaAutomatica("Nota:", Estilos.FuenteNegrita), 0, 4);
        var nota = Estilos.CrearTextoInformativo(
            "El recorrido comienza en q0 e incluye un estado por cada símbolo leído.");
        nota.ForeColor = Estilos.TextoSecundario;
        nota.BackColor = Estilos.Fondo;
        rejilla.Controls.Add(nota, 1, 4);

        foreach (TextBox caja in new[] { _txtSimbolos, _txtRecorrido, _txtEstadoFinal, _txtResultado })
        {
            caja.ReadOnly = true;
            caja.Font = Estilos.FuenteNormal;
        }

        grupo.Controls.Add(rejilla);
        return grupo;
    }

    private void Procesar_Click(object? sender, EventArgs e)
    {
        if (!AutomataService.Instancia.HayAutomata)
        {
            MessageBox.Show("Todavía no se ha construido ningún autómata.", "Autómata no disponible",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ResultadoReconocimiento resultado = _servicio.Procesar(
            AutomataService.Instancia.ObtenerActualOExcepcion(),
            AutomataService.Instancia.ObtenerConfiguracionOExcepcion(),
            _txtPalabra.Text);

        _txtSimbolos.Text = resultado.SimbolosProcesados.Count == 0
            ? "ε (ningún símbolo)"
            : string.Join("  ", resultado.SimbolosProcesados);
        _txtRecorrido.Text = string.Join(" → ", resultado.RecorridoEstados);
        _txtEstadoFinal.Text = resultado.EstadoFinal ?? "—";

        if (!resultado.EntradaValida)
        {
            _txtResultado.Text = "ENTRADA INVÁLIDA";
            _txtResultado.ForeColor = Estilos.Error;
            _mensaje.ForeColor = Estilos.Error;
            _mensaje.Text = resultado.Mensaje;
            return;
        }

        _txtResultado.Text = resultado.Aceptada ? "ACEPTADA" : "RECHAZADA";
        _txtResultado.ForeColor = resultado.Aceptada ? Estilos.Exito : Estilos.Error;
        _mensaje.ForeColor = resultado.Aceptada ? Estilos.Exito : Estilos.TextoSecundario;
        _mensaje.Text = resultado.Mensaje;
    }

    private void Limpiar_Click(object? sender, EventArgs e)
    {
        _txtPalabra.Clear();
        _txtSimbolos.Clear();
        _txtRecorrido.Clear();
        _txtEstadoFinal.Clear();
        _txtResultado.Clear();
        _txtResultado.ForeColor = Estilos.Texto;
        _mensaje.ForeColor = Estilos.TextoSecundario;
        _mensaje.Text = "Escriba una palabra y pulse Procesar. Use ε (o deje el campo vacío) para la palabra vacía.";
        _txtPalabra.Focus();
    }
}
