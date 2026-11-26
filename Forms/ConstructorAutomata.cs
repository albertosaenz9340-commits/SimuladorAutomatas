using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using SimuladorAutomatas.Controls;
using SimuladorAutomatas.Models;
using SimuladorAutomatas.Services;
using SimuladorAutomatas.UI;

namespace SimuladorAutomatas.Forms;

/// <summary>
/// Módulo 1: permite definir estados, alfabeto, estado inicial, estados finales
/// y transiciones; valida el AFD, publica la instancia compartida y muestra su
/// representación formal, su tabla de transiciones y su diagrama GDI+.
/// </summary>
public sealed class ConstructorAutomata : Form
{
    private readonly ValidacionAutomataService _validador = new();
    private readonly List<string> _estados = new();
    private readonly List<char> _alfabeto = new();
    private readonly List<Transicion> _transiciones = new();
    private string? _inicialDeseado;
    private HashSet<string> _finalesDeseados = new(StringComparer.Ordinal);
    private bool _sincronizando;

    private readonly TextBox _txtNuevoEstado = Estilos.CrearCaja();
    private readonly ListBox _lstEstados = Estilos.CrearLista();
    private readonly TextBox _txtNuevoSimbolo = Estilos.CrearCaja();
    private readonly ListBox _lstAlfabeto = Estilos.CrearLista();
    private readonly ComboBox _cmbInicial = Estilos.CrearCombo();
    private readonly CheckedListBox _lstFinales = Estilos.CrearListaVerificable();
    private readonly TextBox _txtOrigen = Estilos.CrearCaja();
    private readonly ComboBox _cmbSimbolo = Estilos.CrearCombo();
    private readonly ComboBox _cmbDestino = Estilos.CrearCombo();
    private readonly DataGridView _dgvTransiciones = Estilos.CrearTabla();
    private readonly ComboBox _cmbCasos = Estilos.CrearCombo();
    private readonly TextBox _lblMensaje = Estilos.CrearTextoInformativo();

    private readonly TextBox _txtFormal = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Font = Estilos.FuenteCodigo,
        BackColor = Color.White,
        BorderStyle = BorderStyle.None,
        Padding = new Padding(10)
    };

    private readonly DataGridView _dgvTabla = Estilos.CrearTabla();
    private readonly GrafoAutomata _grafo = new();
    private readonly Label _lblZoom = Estilos.CrearEtiquetaAutomatica("Zoom: 160%", Estilos.FuenteNegrita);

    public ConstructorAutomata()
    {
        Text = "Módulo 1 — Construcción de autómatas finitos";
        Size = new Size(1180, 720);
        MinimumSize = new Size(980, 640);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Estilos.Fondo;
        Font = Estilos.FuenteNormal;
        ConstruirInterfaz();
        ConfigurarTablaTransiciones();
        CargarCasos();
        ActualizarListas(conservarSeleccion: false);
        ActualizarVistaPrevia();

        Automata? existente = AutomataService.Instancia.Actual;
        if (existente is not null)
        {
            _lblMensaje.Text = $"Hay un autómata construido previamente: {existente.Nombre}.";
            MostrarAutomata(existente);
        }
    }

    private void ConstruirInterfaz()
    {
        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(14, 12, 14, 12)
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        raiz.Controls.Add(Estilos.CrearCabecera(
            "Construcción de autómatas finitos",
            "Defina la 5-tupla M = (Q, Σ, δ, q0, F) con exactamente 5 nodos."), 0, 0);

        var division = new SplitContainer
        {
            Orientation = Orientation.Vertical,
            Size = new Size(1140, 620),
            Panel1MinSize = 400,
            Panel2MinSize = 520,
            SplitterDistance = 480,
            BackColor = Estilos.Borde
        };
        division.Dock = DockStyle.Fill;

        var areaDefinicion = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Estilos.Fondo
        };
        areaDefinicion.Controls.Add(CrearPanelDefinicion());
        division.Panel1.Controls.Add(areaDefinicion);
        division.Panel2.Controls.Add(CrearPanelResultados());
        raiz.Controls.Add(division, 0, 1);
        Controls.Add(raiz);
        ActualizarZoom();
    }

    private TableLayoutPanel CrearPanelDefinicion()
    {
        var panel = Estilos.CrearRejilla(7, 1, 6);
        panel.Dock = DockStyle.Top;
        panel.AutoSize = true;
        for (int i = 0; i < 7; i++)
        {
            panel.RowStyles[i] = new RowStyle(SizeType.AutoSize);
        }

        GroupBox casos = CrearGrupoCasos();
        casos.MinimumSize = new Size(0, 116);
        GroupBox estados = CrearGrupoEstados();
        estados.MinimumSize = new Size(0, 184);
        GroupBox alfabeto = CrearGrupoAlfabeto();
        alfabeto.MinimumSize = new Size(0, 164);
        GroupBox inicialFinales = CrearGrupoInicialFinales();
        inicialFinales.MinimumSize = new Size(0, 184);
        GroupBox transiciones = CrearGrupoTransiciones();
        transiciones.MinimumSize = new Size(0, 270);

        panel.Controls.Add(casos, 0, 0);
        panel.Controls.Add(estados, 0, 1);
        panel.Controls.Add(alfabeto, 0, 2);
        panel.Controls.Add(inicialFinales, 0, 3);
        panel.Controls.Add(transiciones, 0, 4);
        panel.Controls.Add(CrearPanelBotones(), 0, 5);

        var marcoMensaje = new Panel
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(0, 84),
            BackColor = Color.FromArgb(232, 242, 252)
        };
        _lblMensaje.ForeColor = Estilos.Primario;
        _lblMensaje.BackColor = Color.FromArgb(232, 242, 252);
        marcoMensaje.Controls.Add(_lblMensaje);
        panel.Controls.Add(marcoMensaje, 0, 6);
        return panel;
    }

    private GroupBox CrearGrupoCasos()
    {
        var grupo = Estilos.CrearGrupo("Casos de prueba (carga rápida)");
        var rejilla = Estilos.CrearRejilla(2, 3, 3);
        rejilla.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, 64);
        rejilla.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 100f);
        rejilla.ColumnStyles[2] = new ColumnStyle(SizeType.AutoSize);
        rejilla.RowStyles[0] = new RowStyle(SizeType.Absolute, 44);
        rejilla.RowStyles[1] = new RowStyle(SizeType.AutoSize);

        rejilla.Controls.Add(Estilos.CrearEtiqueta("Caso:"), 0, 0);
        rejilla.Controls.Add(_cmbCasos, 1, 0);

        var btnCargar = Estilos.CrearBoton("Carga");
        btnCargar.Click += CargarCaso_Click;
        rejilla.Controls.Add(btnCargar, 2, 0);

        var ayuda = Estilos.CrearTextoInformativo(
            "Seleccione un caso y pulse Carga para probar los ejemplos incluidos.");
        ayuda.MinimumSize = new Size(0, 60);
        ayuda.ForeColor = Estilos.TextoSecundario;
        ayuda.BackColor = Estilos.Superficie;
        ayuda.Font = Estilos.FuenteSubtitulo;
        rejilla.Controls.Add(ayuda, 0, 1);
        rejilla.SetColumnSpan(ayuda, 3);

        grupo.Controls.Add(rejilla);
        return grupo;
    }

    private GroupBox CrearGrupoEstados()
    {
        var grupo = Estilos.CrearGrupo("Conjunto Q de estados (5 nodos)");
        var rejilla = Estilos.CrearRejilla(2, 3, 3);
        rejilla.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, 100);
        rejilla.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 100f);
        rejilla.ColumnStyles[2] = new ColumnStyle(SizeType.AutoSize);
        rejilla.RowStyles[0] = new RowStyle(SizeType.Absolute, 44);
        rejilla.RowStyles[1] = new RowStyle(SizeType.Percent, 100);

        rejilla.Controls.Add(Estilos.CrearEtiqueta("Nuevo:"), 0, 0);
        rejilla.Controls.Add(_txtNuevoEstado, 1, 0);
        var btnAgregar = Estilos.CrearBoton("Agrega");
        btnAgregar.Click += AgregarEstado_Click;
        rejilla.Controls.Add(btnAgregar, 2, 0);

        var inferior = Estilos.CrearRejilla(1, 2, 3);
        inferior.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 100f);
        inferior.ColumnStyles[1] = new ColumnStyle(SizeType.AutoSize);
        inferior.Controls.Add(_lstEstados, 0, 0);
        var btnQuitar = Estilos.CrearBoton("Quitar", secundario: true);
        btnQuitar.Click += QuitarEstado_Click;
        inferior.Controls.Add(btnQuitar, 1, 0);
        rejilla.Controls.Add(inferior, 0, 1);
        rejilla.SetColumnSpan(inferior, 3);

        grupo.Controls.Add(rejilla);
        return grupo;
    }

    private GroupBox CrearGrupoAlfabeto()
    {
        var grupo = Estilos.CrearGrupo("Alfabeto Σ (un carácter por símbolo)");
        var rejilla = Estilos.CrearRejilla(2, 3, 3);
        rejilla.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, 100);
        rejilla.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 100f);
        rejilla.ColumnStyles[2] = new ColumnStyle(SizeType.AutoSize);
        rejilla.RowStyles[0] = new RowStyle(SizeType.Absolute, 44);
        rejilla.RowStyles[1] = new RowStyle(SizeType.Percent, 100);

        rejilla.Controls.Add(Estilos.CrearEtiqueta("Símbolos:"), 0, 0);
        rejilla.Controls.Add(_txtNuevoSimbolo, 1, 0);
        var btnAgregar = Estilos.CrearBoton("Agrega");
        btnAgregar.Click += AgregarSimbolo_Click;
        rejilla.Controls.Add(btnAgregar, 2, 0);

        var inferior = Estilos.CrearRejilla(1, 2, 3);
        inferior.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 100f);
        inferior.ColumnStyles[1] = new ColumnStyle(SizeType.AutoSize);
        inferior.Controls.Add(_lstAlfabeto, 0, 0);
        var btnQuitar = Estilos.CrearBoton("Quitar", secundario: true);
        btnQuitar.Click += QuitarSimbolo_Click;
        inferior.Controls.Add(btnQuitar, 1, 0);
        rejilla.Controls.Add(inferior, 0, 1);
        rejilla.SetColumnSpan(inferior, 3);

        grupo.Controls.Add(rejilla);
        return grupo;
    }

    private GroupBox CrearGrupoInicialFinales()
    {
        var grupo = Estilos.CrearGrupo("Estado inicial q0 y estados finales F");
        var rejilla = Estilos.CrearRejilla(2, 2, 3);
        rejilla.ColumnStyles[0] = new ColumnStyle(SizeType.AutoSize);
        rejilla.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 100f);
        rejilla.RowStyles[0] = new RowStyle(SizeType.Absolute, 44);
        rejilla.RowStyles[1] = new RowStyle(SizeType.Percent, 100);

        rejilla.Controls.Add(Estilos.CrearEtiquetaAutomatica("Inicial q0:", Estilos.FuenteNegrita), 0, 0);
        rejilla.Controls.Add(Estilos.CrearEtiquetaAutomatica("Finales F:", Estilos.FuenteNegrita), 1, 0);
        rejilla.Controls.Add(_cmbInicial, 0, 1);
        rejilla.Controls.Add(_lstFinales, 1, 1);
        grupo.Controls.Add(rejilla);
        return grupo;
    }

    private GroupBox CrearGrupoTransiciones()
    {
        var grupo = Estilos.CrearGrupo("Función de transición δ (editable en la tabla)");
        var rejilla = Estilos.CrearRejilla(5, 3, 3);
        rejilla.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, 100);
        rejilla.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 100f);
        rejilla.ColumnStyles[2] = new ColumnStyle(SizeType.Absolute, 100);
        rejilla.RowStyles[0] = new RowStyle(SizeType.Absolute, 44);
        rejilla.RowStyles[1] = new RowStyle(SizeType.Absolute, 48);
        rejilla.RowStyles[2] = new RowStyle(SizeType.Absolute, 44);
        rejilla.RowStyles[3] = new RowStyle(SizeType.Percent, 100);
        rejilla.RowStyles[4] = new RowStyle(SizeType.Absolute, 48);

        rejilla.Controls.Add(Estilos.CrearEtiquetaAutomatica("Origen", Estilos.FuenteNegrita), 0, 0);
        rejilla.Controls.Add(Estilos.CrearEtiquetaAutomatica("Símbolo", Estilos.FuenteNegrita), 1, 0);
        rejilla.Controls.Add(Estilos.CrearEtiquetaAutomatica("Destino", Estilos.FuenteNegrita), 2, 0);

        rejilla.Controls.Add(_txtOrigen, 0, 1);
        rejilla.Controls.Add(_cmbSimbolo, 1, 1);
        rejilla.Controls.Add(_cmbDestino, 2, 1);

        var btnAgregar = Estilos.CrearBoton("Agregar δ");
        btnAgregar.Click += AgregarTransicion_Click;
        rejilla.Controls.Add(btnAgregar, 0, 2);
        rejilla.SetColumnSpan(btnAgregar, 3);

        rejilla.Controls.Add(_dgvTransiciones, 0, 3);
        rejilla.SetColumnSpan(_dgvTransiciones, 3);

        var btnQuitar = Estilos.CrearBoton("Quitar transición seleccionada", secundario: true);
        btnQuitar.Click += QuitarTransicion_Click;
        rejilla.Controls.Add(btnQuitar, 0, 4);
        rejilla.SetColumnSpan(btnQuitar, 3);

        grupo.Controls.Add(rejilla);
        return grupo;
    }

    private FlowLayoutPanel CrearPanelBotones()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(3),
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };

        Button Crear(string texto)
        {
            Button boton = Estilos.CrearBoton(texto);
            boton.Dock = DockStyle.None;
            boton.AutoSize = true;
            boton.Margin = new Padding(4, 3, 4, 3);
            return boton;
        }

        var btnConstruir = Crear("VALIDAR Y CONSTRUIR");
        btnConstruir.Click += ValidarYConstruir_Click;
        panel.Controls.Add(btnConstruir);

        var btnLimpiar = Crear("Limpiar todo");
        btnLimpiar.Click += Limpiar_Click;
        panel.Controls.Add(btnLimpiar);

        var btnCerrar = Crear("Cerrar");
        btnCerrar.Click += (_, _) => Close();
        panel.Controls.Add(btnCerrar);

        return panel;
    }

    private TabControl CrearPanelResultados()
    {
        var pestanas = new TabControl { Dock = DockStyle.Fill, Font = Estilos.FuenteNormal, Padding = new Point(8, 5) };

        var paginaFormal = new TabPage("Representación formal") { BackColor = Estilos.Fondo, Padding = new Padding(6) };
        paginaFormal.Controls.Add(_txtFormal);
        pestanas.TabPages.Add(paginaFormal);

        var paginaTabla = new TabPage("Tabla de transiciones") { BackColor = Estilos.Fondo, Padding = new Padding(6) };
        paginaTabla.Controls.Add(_dgvTabla);
        pestanas.TabPages.Add(paginaTabla);

        var paginaGrafico = new TabPage("Diagrama (GDI+)") { BackColor = Color.White, Padding = new Padding(0) };
        var contenido = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = Padding.Empty };
        contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contenido.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var barra = Estilos.CrearRejilla(1, 4, 4);
        barra.ColumnStyles[0] = new ColumnStyle(SizeType.AutoSize);
        barra.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 100f);
        barra.ColumnStyles[2] = new ColumnStyle(SizeType.AutoSize);
        barra.ColumnStyles[3] = new ColumnStyle(SizeType.AutoSize);
        var ayuda = Estilos.CrearEtiquetaAutomatica(
            "Arrastre: desplazar · Rueda: ampliar", Estilos.FuenteSubtitulo, Estilos.TextoSecundario);
        barra.Controls.Add(ayuda, 0, 0);
        var btnRestablecer = Estilos.CrearBoton("Restablecer vista", secundario: true);
        btnRestablecer.Click += (_, _) => _grafo.RestablecerVista();
        barra.Controls.Add(btnRestablecer, 2, 0);
        barra.Controls.Add(_lblZoom, 3, 0);
        _grafo.VistaCambiada += (_, _) => ActualizarZoom();

        contenido.Controls.Add(barra, 0, 0);
        contenido.Controls.Add(_grafo, 0, 1);
        paginaGrafico.Controls.Add(contenido);
        pestanas.TabPages.Add(paginaGrafico);

        return pestanas;
    }

    private void ConfigurarTablaTransiciones()
    {
        var columnaOrigen = new DataGridViewComboBoxColumn
        {
            HeaderText = "Origen", Name = "Origen", DataPropertyName = "Origen", Width = 100, MinimumWidth = 100
        };
        var columnaSimbolo = new DataGridViewComboBoxColumn
        {
            HeaderText = "Símbolo", Name = "Simbolo", DataPropertyName = "Simbolo", Width = 90, MinimumWidth = 90
        };
        var columnaDestino = new DataGridViewComboBoxColumn
        {
            HeaderText = "Destino", Name = "Destino", DataPropertyName = "Destino", Width = 100, MinimumWidth = 100
        };
        _dgvTransiciones.Columns.Add(columnaOrigen);
        _dgvTransiciones.Columns.Add(columnaSimbolo);
        _dgvTransiciones.Columns.Add(columnaDestino);
        _dgvTransiciones.DataError += (_, e) => e.ThrowException = false;
        _dgvTransiciones.CellValueChanged += (_, _) =>
        {
            if (_sincronizando)
            {
                return;
            }

            SincronizarTransicionesDesdeGrid();
            ActualizarVistaPrevia();
        };
        _dgvTransiciones.RowsRemoved += (_, _) =>
        {
            if (_sincronizando)
            {
                return;
            }

            SincronizarTransicionesDesdeGrid();
            ActualizarVistaPrevia();
        };
    }

    private void CargarCasos()
    {
        foreach (CasoPrueba caso in CasosPruebaService.ObtenerCasos())
        {
            _cmbCasos.Items.Add(caso);
        }

        if (_cmbCasos.Items.Count > 0)
        {
            _cmbCasos.SelectedIndex = 0;
        }
    }

    private void AgregarEstado_Click(object? sender, EventArgs e)
    {
        foreach (string estado in Dividir(_txtNuevoEstado.Text))
        {
            if (_estados.Count >= ValidacionAutomataService.CantidadEstadosRequerida)
            {
                Mensaje("Q debe tener exactamente 5 nodos. Quite uno antes de agregar otro.");
                break;
            }

            if (_estados.Any(x => string.Equals(x, estado, StringComparison.Ordinal)))
            {
                _lblMensaje.Text = $"El estado '{estado}' ya está en Q (no se permiten duplicados).";
                continue;
            }

            _estados.Add(estado);
        }

        _txtNuevoEstado.Clear();
        ActualizarListas();
    }

    private void QuitarEstado_Click(object? sender, EventArgs e)
    {
        if (_lstEstados.SelectedItem is not string estado)
        {
            Mensaje("Seleccione un estado de la lista para quitarlo.");
            return;
        }

        _estados.Remove(estado);
        _transiciones.RemoveAll(t => t.Origen == estado || t.Destino == estado);
        _finalesDeseados.Remove(estado);
        if (string.Equals(_inicialDeseado, estado, StringComparison.Ordinal))
        {
            _inicialDeseado = null;
        }

        ActualizarListas();
        Mensaje($"Estado '{estado}' eliminado junto con sus transiciones.");
    }

    private void AgregarSimbolo_Click(object? sender, EventArgs e)
    {
        foreach (char simbolo in _txtNuevoSimbolo.Text.Where(c => !char.IsWhiteSpace(c) && c != ',' && c != ';' && c != '|'))
        {
            if (_alfabeto.Contains(simbolo))
            {
                Mensaje($"El símbolo '{simbolo}' ya está en Σ (no se permiten duplicados).");
                continue;
            }

            _alfabeto.Add(simbolo);
        }

        _txtNuevoSimbolo.Clear();
        ActualizarListas();
    }

    private void QuitarSimbolo_Click(object? sender, EventArgs e)
    {
        if (_lstAlfabeto.SelectedItem is not string texto || texto.Length == 0)
        {
            Mensaje("Seleccione un símbolo del alfabeto para quitarlo.");
            return;
        }

        char simbolo = texto[0];
        _alfabeto.Remove(simbolo);
        _transiciones.RemoveAll(t => t.Simbolo == simbolo);
        ActualizarListas();
        Mensaje($"Símbolo '{simbolo}' eliminado junto con sus transiciones.");
    }

    private void AgregarTransicion_Click(object? sender, EventArgs e)
    {
        _dgvTransiciones.EndEdit();
        string origen = _txtOrigen.Text.Trim();
        string? simboloTexto = _cmbSimbolo.SelectedItem?.ToString();
        string destino = _cmbDestino.SelectedItem?.ToString() ?? string.Empty;

        if (!_estados.Contains(origen) || simboloTexto is null || simboloTexto.Length != 1 || !_estados.Contains(destino))
        {
            Mensaje("Seleccione un origen existente, un símbolo de Σ y un destino existente.");
            return;
        }

        char simbolo = simboloTexto[0];
        if (_transiciones.Any(t => t.Origen == origen && t.Simbolo == simbolo))
        {
            Mensaje($"Ya existe δ({origen}, {simbolo}). Un AFD no admite transiciones duplicadas.");
            return;
        }

        _transiciones.Add(new Transicion(origen, simbolo, destino));
        RedibujarTransiciones();
        ActualizarVistaPrevia();
        Mensaje($"Agregada δ({origen}, {simbolo}) = {destino}.");
    }

    private void QuitarTransicion_Click(object? sender, EventArgs e)
    {
        if (_dgvTransiciones.CurrentRow is null || _dgvTransiciones.CurrentRow.IsNewRow)
        {
            Mensaje("Seleccione la transición que desea quitar.");
            return;
        }

        _dgvTransiciones.Rows.Remove(_dgvTransiciones.CurrentRow);
        SincronizarTransicionesDesdeGrid();
        ActualizarVistaPrevia();
        Mensaje("Transición eliminada.");
    }

    private void ValidarYConstruir_Click(object? sender, EventArgs e)
    {
        _dgvTransiciones.EndEdit();
        BindingContext? contexto = BindingContext;
        contexto?[_dgvTransiciones]?.EndCurrentEdit();
        SincronizarTransicionesDesdeGrid();

        ResultadoValidacion resultado = AutomataService.Instancia.Establecer(
            "Autómata construido",
            _estados,
            _alfabeto,
            _cmbInicial.SelectedItem?.ToString() ?? string.Empty,
            FinalesSeleccionados(),
            _transiciones);

        if (!resultado.EsValido)
        {
            string detalle = string.Join(Environment.NewLine, resultado.Errores.Take(16));
            if (resultado.Errores.Count > 16)
            {
                detalle += Environment.NewLine + $"... y {resultado.Errores.Count - 16} error(es) más.";
            }

            Mensaje("La definición no es un AFD válido. Revise los errores indicados.");
            MessageBox.Show(detalle, "Errores de validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Automata construido = AutomataService.Instancia.Actual!;
        MostrarAutomata(construido);
        Mensaje($"Autómata construido: {construido.Nombre}. Ya puede usar los módulos 2 y 3.");
        MessageBox.Show(
            "El autómata se construyó correctamente y está disponible en los tres módulos.",
            "Autómata válido", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void Limpiar_Click(object? sender, EventArgs e)
    {
        _estados.Clear();
        _alfabeto.Clear();
        _transiciones.Clear();
        _inicialDeseado = null;
        _finalesDeseados = new HashSet<string>(StringComparer.Ordinal);
        AutomataService.Instancia.Limpiar();
        _txtNuevoEstado.Clear();
        _txtNuevoSimbolo.Clear();
        _txtOrigen.Clear();
        ActualizarListas(conservarSeleccion: false);
        ActualizarVistaPrevia();
        Mensaje("Definición limpia. No hay autómata construido.");
    }

    private void CargarCaso_Click(object? sender, EventArgs e)
    {
        if (_cmbCasos.SelectedItem is not CasoPrueba caso)
        {
            Mensaje("Seleccione un caso de prueba.");
            return;
        }

        CargarCaso(caso);
    }

    /// <summary>Carga un caso de prueba en los controles y lo publica como autómata actual.</summary>
    public void CargarCaso(CasoPrueba caso)
    {
        ArgumentNullException.ThrowIfNull(caso);

        _estados.Clear();
        _estados.AddRange(caso.Estados);
        _alfabeto.Clear();
        _alfabeto.AddRange(caso.Alfabeto);
        _transiciones.Clear();
        _transiciones.AddRange(caso.Transiciones.Select(t => new Transicion(t.Origen, t.Simbolo, t.Destino)));
        _inicialDeseado = caso.EstadoInicial;
        _finalesDeseados = new HashSet<string>(caso.EstadosFinales, StringComparer.Ordinal);

        ActualizarListas(conservarSeleccion: false);

        Automata automata = caso.CrearAutomata();
        AutomataService.Instancia.Establecer(automata);
        MostrarAutomata(automata);
        Mensaje($"Caso cargado: {caso.Nombre}. {caso.Descripcion}");
    }

    private void ActualizarListas(bool conservarSeleccion = true)
    {
        // La lista _transiciones es la fuente de verdad cuando se muta por código;
        // los cambios hechos directamente en la rejilla se sincronizan en su evento.
        if (conservarSeleccion)
        {
            if (_cmbInicial.SelectedItem is string inicialActual)
            {
                _inicialDeseado = inicialActual;
            }

            _finalesDeseados = new HashSet<string>(FinalesSeleccionados(), StringComparer.Ordinal);
        }

        _lstEstados.BeginUpdate();
        _lstEstados.Items.Clear();
        foreach (string estado in _estados)
        {
            _lstEstados.Items.Add(estado);
        }
        _lstEstados.EndUpdate();

        _lstAlfabeto.BeginUpdate();
        _lstAlfabeto.Items.Clear();
        foreach (char simbolo in _alfabeto)
        {
            _lstAlfabeto.Items.Add(simbolo.ToString());
        }
        _lstAlfabeto.EndUpdate();

        _cmbInicial.BeginUpdate();
        _cmbInicial.Items.Clear();
        foreach (string estado in _estados)
        {
            _cmbInicial.Items.Add(estado);
        }

        if (_inicialDeseado is not null && _estados.Contains(_inicialDeseado))
        {
            _cmbInicial.SelectedItem = _inicialDeseado;
        }
        else if (_estados.Count > 0)
        {
            _cmbInicial.SelectedIndex = 0;
            _inicialDeseado = _estados[0];
        }
        else
        {
            _inicialDeseado = null;
        }
        _cmbInicial.EndUpdate();

        _lstFinales.BeginUpdate();
        _lstFinales.Items.Clear();
        for (int i = 0; i < _estados.Count; i++)
        {
            _lstFinales.Items.Add(_estados[i]);
            _lstFinales.SetItemChecked(i, _finalesDeseados.Contains(_estados[i]));
        }
        _lstFinales.EndUpdate();

        _cmbSimbolo.BeginUpdate();
        _cmbSimbolo.Items.Clear();
        foreach (char simbolo in _alfabeto)
        {
            _cmbSimbolo.Items.Add(simbolo.ToString());
        }
        if (_cmbSimbolo.Items.Count > 0)
        {
            _cmbSimbolo.SelectedIndex = 0;
        }
        _cmbSimbolo.EndUpdate();

        _cmbDestino.BeginUpdate();
        _cmbDestino.Items.Clear();
        foreach (string estado in _estados)
        {
            _cmbDestino.Items.Add(estado);
        }
        if (_cmbDestino.Items.Count > 0)
        {
            _cmbDestino.SelectedIndex = 0;
        }
        _cmbDestino.EndUpdate();

        LimpiarFilasTransiciones();
        ActualizarColumnasTransiciones();
        RedibujarTransiciones();
        ActualizarVistaPrevia();
    }

    private void LimpiarFilasTransiciones()
    {
        _sincronizando = true;
        try
        {
            _dgvTransiciones.Rows.Clear();
        }
        finally
        {
            _sincronizando = false;
        }
    }

    private void ActualizarColumnasTransiciones()
    {
        LlenarItems(((DataGridViewComboBoxColumn)_dgvTransiciones.Columns["Origen"]!).Items, _estados);
        LlenarItems(((DataGridViewComboBoxColumn)_dgvTransiciones.Columns["Simbolo"]!).Items, _alfabeto.Select(c => c.ToString()));
        LlenarItems(((DataGridViewComboBoxColumn)_dgvTransiciones.Columns["Destino"]!).Items, _estados);
    }

    private static void LlenarItems(DataGridViewComboBoxCell.ObjectCollection items, IEnumerable<string> valores)
    {
        items.Clear();
        foreach (string valor in valores)
        {
            items.Add(valor);
        }
    }

    private void RedibujarTransiciones()
    {
        _sincronizando = true;
        try
        {
            _dgvTransiciones.Rows.Clear();
            foreach (Transicion transicion in _transiciones)
            {
                int fila = _dgvTransiciones.Rows.Add(transicion.Origen, transicion.Simbolo.ToString(), transicion.Destino);
                _dgvTransiciones.Rows[fila].ErrorText = string.Empty;
            }
        }
        finally
        {
            _sincronizando = false;
        }
    }

    private void SincronizarTransicionesDesdeGrid()
    {
        if (_sincronizando)
        {
            return;
        }

        _transiciones.Clear();
        foreach (DataGridViewRow fila in _dgvTransiciones.Rows)
        {
            string? origen = fila.Cells[0].Value?.ToString()?.Trim();
            string? simbolo = fila.Cells[1].Value?.ToString()?.Trim();
            string? destino = fila.Cells[2].Value?.ToString()?.Trim();

            if (string.IsNullOrEmpty(origen) || string.IsNullOrEmpty(simbolo) || string.IsNullOrEmpty(destino))
            {
                continue;
            }

            _transiciones.Add(new Transicion(origen, simbolo[0], destino));
        }
    }

    private void ActualizarVistaPrevia()
    {
        string inicial = _cmbInicial.SelectedItem?.ToString() ?? string.Empty;
        ResultadoValidacion resultado = _validador.Validar(_estados, _alfabeto, inicial, FinalesSeleccionados(), _transiciones);

        if (!resultado.EsValido)
        {
            _grafo.Automata = null;
            _dgvTabla.Columns.Clear();
            _dgvTabla.Rows.Clear();
            _txtFormal.Text =
                "Definición incompleta. La vista previa se mostrará cuando la 5-tupla con 5 nodos sea válida." +
                Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, resultado.Errores);
            return;
        }

        var vistaPrevia = new Automata("Vista previa", _estados, _alfabeto, inicial, FinalesSeleccionados(), _transiciones);
        MostrarAutomata(vistaPrevia);
    }

    private void MostrarAutomata(Automata automata)
    {
        _txtFormal.Text = automata.RepresentacionFormal;
        MostrarTabla(automata);
        _grafo.Automata = automata;
    }

    private void MostrarTabla(Automata automata)
    {
        _dgvTabla.Columns.Clear();
        _dgvTabla.Rows.Clear();

        _dgvTabla.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Estado",
            Name = "Estado",
            MinimumWidth = 110,
            ReadOnly = true
        });

        foreach (char simbolo in automata.Alfabeto)
        {
            _dgvTabla.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = simbolo.ToString(),
                Name = $"Simbolo{simbolo}",
                MinimumWidth = 70,
                ReadOnly = true
            });
        }

        foreach (string estado in automata.Estados)
        {
            bool esInicial = estado == automata.EstadoInicial;
            bool esFinal = automata.EsFinal(estado);
            string marcador = (esInicial ? "→" : string.Empty) + (esFinal ? "*" : string.Empty);
            var valores = new List<object> { $"{marcador}{estado}" };
            valores.AddRange(automata.Alfabeto.Select(s => (object)automata.ObtenerDestino(estado, s)!));
            _dgvTabla.Rows.Add(valores.ToArray());
        }
    }

    private IReadOnlyList<string> FinalesSeleccionados()
    {
        var seleccionados = new List<string>();
        for (int i = 0; i < _lstFinales.Items.Count; i++)
        {
            if (_lstFinales.GetItemChecked(i))
            {
                seleccionados.Add(_lstFinales.Items[i].ToString()!);
            }
        }

        return seleccionados;
    }

    private void ActualizarZoom() => _lblZoom.Text = $"Zoom: {_grafo.Escala * 100:0}%";

    private void Mensaje(string texto) => _lblMensaje.Text = texto;

    private static IEnumerable<string> Dividir(string texto) =>
        Regex.Split(texto.Trim(), @"[,;\s]+").Where(parte => parte.Length > 0);
}
