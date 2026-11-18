using System.Drawing;
using System.Windows.Forms;

namespace SimuladorAutomatas.UI;

/// <summary>
/// Paleta y fábricas de controles compartidas por todos los formularios.
/// Toda la interfaz se construye por código; no se usa el diseñador visual.
/// </summary>
public static class Estilos
{
    public static readonly Color Fondo = Color.FromArgb(244, 247, 251);
    public static readonly Color Superficie = Color.White;
    public static readonly Color Primario = Color.FromArgb(30, 64, 105);
    public static readonly Color PrimarioSuave = Color.FromArgb(59, 106, 160);
    public static readonly Color Acento = Color.FromArgb(0, 122, 204);
    public static readonly Color Exito = Color.FromArgb(22, 121, 76);
    public static readonly Color Error = Color.FromArgb(178, 44, 44);
    public static readonly Color Texto = Color.FromArgb(28, 40, 55);
    public static readonly Color TextoSecundario = Color.FromArgb(94, 108, 125);
    public static readonly Color Borde = Color.FromArgb(212, 220, 230);

    public static readonly Font FuenteTitulo = new("Segoe UI", 16f, FontStyle.Bold);
    public static readonly Font FuenteSubtitulo = new("Segoe UI", 10f);
    public static readonly Font FuenteNormal = new("Segoe UI", 10f);
    public static readonly Font FuenteNegrita = new("Segoe UI", 10f, FontStyle.Bold);
    public static readonly Font FuenteCodigo = new("Consolas", 10f);

    public static TableLayoutPanel CrearRejilla(int filas, int columnas, int padding = 8)
    {
        var rejilla = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = filas,
            ColumnCount = columnas,
            Padding = new Padding(padding),
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };

        for (int i = 0; i < filas; i++)
        {
            rejilla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        for (int j = 0; j < columnas; j++)
        {
            rejilla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columnas));
        }

        return rejilla;
    }

    public static GroupBox CrearGrupo(string titulo) => new()
    {
        Text = titulo,
        Dock = DockStyle.Fill,
        Padding = new Padding(6),
        Font = FuenteNegrita,
        ForeColor = Primario,
        BackColor = Superficie
    };

    public static Button CrearBoton(string texto, bool secundario = false)
    {
        var fuente = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        var boton = new Button
        {
            Text = texto,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Font = fuente,
            Cursor = Cursors.Hand,
            Margin = new Padding(3),
            BackColor = secundario ? Superficie : Primario,
            ForeColor = secundario ? Primario : Color.White
        };

        // Evita que el texto se parte en dos líneas cuando la celda es estrecha
        // o cuando la fuente crece con el DPI de la pantalla.
        Size minimo = TextRenderer.MeasureText(texto, fuente,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        boton.MinimumSize = new Size(minimo.Width + 18, 0);

        boton.FlatAppearance.BorderSize = secundario ? 1 : 0;
        boton.FlatAppearance.BorderColor = secundario ? Borde : Primario;
        boton.FlatAppearance.MouseOverBackColor = secundario ? Color.FromArgb(235, 242, 250) : PrimarioSuave;
        return boton;
    }

    public static TextBox CrearCaja()
    {
        return new TextBox
        {
            Dock = DockStyle.Fill,
            Font = FuenteNormal,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(3),
            BackColor = Superficie,
            ForeColor = Texto
        };
    }

    public static ComboBox CrearCombo()
    {
        return new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = FuenteNormal,
            Margin = new Padding(3),
            BackColor = Superficie,
            ForeColor = Texto
        };
    }

    public static ListBox CrearLista()
    {
        return new ListBox
        {
            Dock = DockStyle.Fill,
            Font = FuenteNormal,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(3),
            BackColor = Superficie,
            ForeColor = Texto,
            IntegralHeight = false
        };
    }

    public static CheckedListBox CrearListaVerificable()
    {
        return new CheckedListBox
        {
            Dock = DockStyle.Fill,
            Font = FuenteNormal,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(3),
            CheckOnClick = true,
            BackColor = Superficie,
            ForeColor = Texto,
            IntegralHeight = false
        };
    }

    public static DataGridView CrearTabla()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Superficie,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Primario,
                ForeColor = Color.White,
                Font = FuenteNegrita,
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                WrapMode = DataGridViewTriState.True
            },
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Superficie,
                ForeColor = Texto,
                Font = FuenteNormal,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 4, 0)
            },
            EnableHeadersVisualStyles = false,
            GridColor = Borde,
            MultiSelect = false,
            ReadOnly = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
    }

    public static Label CrearEtiqueta(string texto, bool negrita = false, int tamano = 10)
    {
        return new Label
        {
            Text = texto,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", tamano, negrita ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = Texto,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            AutoEllipsis = true,
            Margin = new Padding(3)
        };
    }

    /// <summary>
    /// Texto de sólo lectura con ajuste de línea y scroll vertical. Se usa para
    /// mensajes largos que deben poder leerse íntegramente en ventanas pequeñas.
    /// </summary>
    public static TextBox CrearTextoInformativo(string texto = "")
    {
        return new TextBox
        {
            Text = texto,
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            WordWrap = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            Font = FuenteNormal,
            ForeColor = Texto,
            BackColor = Superficie,
            Padding = new Padding(6, 3, 3, 3),
            Margin = new Padding(3)
        };
    }

    /// <summary>
    /// Etiqueta que crece con su contenido. Se usa para títulos y textos que no
    /// deben recortarse nunca, independientemente del DPI o del ancho de la celda.
    /// </summary>
    public static Label CrearEtiquetaAutomatica(string texto, Font? fuente = null, Color? color = null) => new()
    {
        Text = texto,
        Font = fuente ?? FuenteNormal,
        ForeColor = color ?? Texto,
        AutoSize = true,
        Dock = DockStyle.None,
        Margin = Padding.Empty
    };

    public static Label CrearTitulo(string texto) =>
        CrearEtiquetaAutomatica(texto, FuenteTitulo, Primario);

    /// <summary>
    /// Cabecera con título y subtítulo. Ambos se auto-dimensionan, de modo que
    /// ninguno queda cortado aunque la fuente crezca por el escalado de Windows.
    /// </summary>
    public static TableLayoutPanel CrearCabecera(string titulo, string subtitulo)
    {
        var cabecera = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 0, 10),
            BackColor = Color.Transparent,
            AutoSize = true
        };
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        cabecera.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cabecera.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        cabecera.Controls.Add(CrearTitulo(titulo), 0, 0);
        cabecera.Controls.Add(CrearEtiquetaAutomatica(subtitulo, FuenteSubtitulo, TextoSecundario), 0, 1);
        return cabecera;
    }
}
