using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using SimuladorAutomatas.Models;

namespace SimuladorAutomatas.Controls;

/// <summary>
/// Control personalizado que dibuja el autómata con GDI+: estados como círculos,
/// finales con doble circunferencia, flecha inicial, arcos dirigidos y bucles.
/// Permite desplazar el dibujo arrastrando con el ratón y ampliarlo con la rueda.
/// </summary>
public sealed class GrafoAutomata : Panel
{
    private const float RadioEstado = 27f;
    private const float SeparacionBucle = 52f;

    private readonly Pen _penEstados = new(Color.FromArgb(35, 70, 110), 2f);
    private readonly Pen _penFinales = new(Color.FromArgb(22, 121, 76), 2f);
    private readonly Pen _penTransiciones = new(Color.FromArgb(70, 90, 115), 2f);
    private readonly Pen _penInicial = new(Color.FromArgb(0, 122, 204), 2.5f);
    private readonly Brush _rellenoEstado = new SolidBrush(Color.White);
    private readonly Brush _rellenoInicial = new SolidBrush(Color.FromArgb(219, 235, 250));
    private readonly Brush _rellenoFinal = new SolidBrush(Color.FromArgb(226, 245, 232));
    private readonly Brush _rellenoInicialFinal = new SolidBrush(Color.FromArgb(222, 240, 246));

    private Automata? _automata;
    private ConfiguracionAceptacion? _configuracion;
    private PointF _desplazamiento = PointF.Empty;
    private float _escala = 1f;
    private bool _arrastrando;
    private Point _ultimoPunto;

    public event EventHandler? VistaCambiada;

    public Automata? Automata
    {
        get => _automata;
        set
        {
            _automata = value;
            Invalidate();
        }
    }

    /// <summary>Configuración de aceptación usada para marcar inicial y finales.</summary>
    public ConfiguracionAceptacion? Configuracion
    {
        get => _configuracion;
        set
        {
            _configuracion = value;
            Invalidate();
        }
    }

    public float Escala => _escala;

    public GrafoAutomata()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.ResizeRedraw, true);
        Paint += Dibujar;
        MouseDown += IniciarArrastre;
        MouseMove += Arrastrar;
        MouseUp += TerminarArrastre;
        MouseLeave += TerminarArrastre;
        MouseWheel += CambiarEscala;
    }

    public void RestablecerVista()
    {
        _desplazamiento = PointF.Empty;
        _escala = 1f;
        Invalidate();
        VistaCambiada?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _penEstados.Dispose();
            _penFinales.Dispose();
            _penTransiciones.Dispose();
            _penInicial.Dispose();
            _rellenoEstado.Dispose();
            _rellenoInicial.Dispose();
            _rellenoFinal.Dispose();
            _rellenoInicialFinal.Dispose();
        }

        base.Dispose(disposing);
    }

    private void IniciarArrastre(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _arrastrando = true;
        _ultimoPunto = e.Location;
        Cursor = Cursors.SizeAll;
    }

    private void Arrastrar(object? sender, MouseEventArgs e)
    {
        if (!_arrastrando)
        {
            return;
        }

        _desplazamiento.X += e.X - _ultimoPunto.X;
        _desplazamiento.Y += e.Y - _ultimoPunto.Y;
        _ultimoPunto = e.Location;
        Invalidate();
    }

    private void TerminarArrastre(object? sender, EventArgs e)
    {
        if (!_arrastrando)
        {
            return;
        }

        _arrastrando = false;
        Cursor = Cursors.Hand;
        VistaCambiada?.Invoke(this, EventArgs.Empty);
    }

    private void CambiarEscala(object? sender, MouseEventArgs e)
    {
        float nuevo = _escala + (e.Delta > 0 ? 0.1f : -0.1f);
        _escala = Math.Max(0.5f, Math.Min(2.5f, nuevo));
        Invalidate();
        VistaCambiada?.Invoke(this, EventArgs.Empty);
    }

    private void Dibujar(object? sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        if (_automata is null || _automata.Estados.Count == 0)
        {
            using var fuente = new Font("Segoe UI", 10f);
            using var brocha = new SolidBrush(Color.FromArgb(120, 132, 148));
            using var formato = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.Word
            };
            const string texto = "Construya un autómata para visualizar su diagrama.";
            const int margen = 16;
            var area = new RectangleF(margen, margen,
                Math.Max(0, ClientSize.Width - margen * 2),
                Math.Max(0, ClientSize.Height - margen * 2));
            g.DrawString(texto, fuente, brocha, area, formato);
            return;
        }

        g.TranslateTransform(ClientSize.Width / 2f + _desplazamiento.X,
                             ClientSize.Height / 2f + _desplazamiento.Y);
        g.ScaleTransform(_escala, _escala);

        Dictionary<string, PointF> posiciones = CalcularPosiciones(_automata);
        DibujarTransiciones(g, _automata, posiciones);
        DibujarEstados(g, _automata, posiciones, Configuracion);
    }

    private static Dictionary<string, PointF> CalcularPosiciones(Automata automata)
    {
        var posiciones = new Dictionary<string, PointF>(StringComparer.Ordinal);
        int cantidad = automata.Estados.Count;
        if (cantidad == 1)
        {
            posiciones[automata.Estados[0]] = PointF.Empty;
            return posiciones;
        }

        float radio = Math.Max(100f, cantidad * 25f);
        double inicio = cantidad <= 2 ? 0 : -Math.PI / 2;
        for (int i = 0; i < cantidad; i++)
        {
            double angulo = inicio + (2 * Math.PI * i / cantidad);
            float x = (float)(radio * Math.Cos(angulo));
            float y = (float)(radio * Math.Sin(angulo));
            posiciones[automata.Estados[i]] = new PointF(x, y);
        }

        return posiciones;
    }

    private void DibujarTransiciones(Graphics g, Automata automata, Dictionary<string, PointF> posiciones)
    {
        var dibujadas = new HashSet<(string Origen, string Destino)>();

        foreach (string origen in automata.Estados)
        {
            foreach (string destino in automata.Estados)
            {
                List<char> simbolos = automata.Alfabeto
                    .Where(simbolo => automata.ObtenerDestino(origen, simbolo) == destino)
                    .ToList();

                if (simbolos.Count == 0 || !dibujadas.Add((origen, destino)))
                {
                    continue;
                }

                string etiqueta = string.Join(", ", simbolos);
                PointF puntoOrigen = posiciones[origen];
                PointF puntoDestino = posiciones[destino];

                if (string.Equals(origen, destino, StringComparison.Ordinal))
                {
                    DibujarBucle(g, puntoOrigen, etiqueta);
                }
                else
                {
                    bool reciprocada = automata.Alfabeto.Any(s => automata.ObtenerDestino(destino, s) == origen);
                    DibujarArco(g, puntoOrigen, puntoDestino, etiqueta, reciprocada);
                }
            }
        }
    }

    private void DibujarArco(Graphics g, PointF origen, PointF destino, string etiqueta, bool reciprocada)
    {
        float dx = destino.X - origen.X;
        float dy = destino.Y - origen.Y;
        float longitud = (float)Math.Sqrt(dx * dx + dy * dy);
        if (longitud < 1f)
        {
            return;
        }

        float ux = dx / longitud;
        float uy = dy / longitud;
        float px = -uy;
        float py = ux;

        PointF inicio = new(origen.X + ux * RadioEstado, origen.Y + uy * RadioEstado);
        PointF fin = new(destino.X - ux * (RadioEstado + 3f), destino.Y - uy * (RadioEstado + 3f));
        float curva = reciprocada ? 38f : 0f;

        PointF control1 = new(inicio.X + dx / 3f + px * curva, inicio.Y + dy / 3f + py * curva);
        PointF control2 = new(inicio.X + 2f * dx / 3f + px * curva, inicio.Y + 2f * dy / 3f + py * curva);

        g.DrawBezier(_penTransiciones, inicio, control1, control2, fin);
        DibujarCabeza(g, fin, new PointF(fin.X - control2.X, fin.Y - control2.Y), _penTransiciones.Color);

        CalcularMitad(out float mx, out float my, control1, control2);
        DibujarEtiqueta(g, etiqueta, new PointF(mx + px * (reciprocada ? 12f : 14f), my + py * (reciprocada ? 12f : 14f)));
    }

    private static void CalcularMitad(out float x, out float y, PointF a, PointF b)
    {
        x = (a.X + b.X) / 2f;
        y = (a.Y + b.Y) / 2f;
    }

    private void DibujarBucle(Graphics g, PointF centro, string etiqueta)
    {
        PointF inicio = PuntoEnCirculo(centro, RadioEstado, -52);
        PointF fin = PuntoEnCirculo(centro, RadioEstado + 3f, -128);
        PointF control1 = new(centro.X + 36f, centro.Y - RadioEstado - SeparacionBucle);
        PointF control2 = new(centro.X - 36f, centro.Y - RadioEstado - SeparacionBucle);

        g.DrawBezier(_penTransiciones, inicio, control1, control2, fin);
        DibujarCabeza(g, fin, new PointF(fin.X - control2.X, fin.Y - control2.Y), _penTransiciones.Color);
        DibujarEtiqueta(g, etiqueta, new PointF(centro.X, centro.Y - RadioEstado - SeparacionBucle - 12f));
    }

    private void DibujarEstados(
        Graphics g,
        Automata automata,
        Dictionary<string, PointF> posiciones,
        ConfiguracionAceptacion? configuracion)
    {
        foreach (string estado in automata.Estados)
        {
            PointF centro = posiciones[estado];
            bool esInicial = configuracion is not null &&
                             string.Equals(estado, configuracion.EstadoInicial, StringComparison.Ordinal);
            bool esFinal = configuracion?.EsFinal(estado) ?? false;

            if (esInicial)
            {
                PointF cola = new(centro.X - RadioEstado - 52f, centro.Y);
                PointF punta = new(centro.X - RadioEstado - 3f, centro.Y);
                g.DrawLine(_penInicial, cola, punta);
                DibujarCabeza(g, punta, new PointF(1, 0), _penInicial.Color);
                DibujarEtiqueta(g, "inicio", new PointF(centro.X - RadioEstado - 52f, centro.Y - 20f), 8.5f);
            }

            Brush relleno = esInicial && esFinal
                ? _rellenoInicialFinal
                : esInicial ? _rellenoInicial : esFinal ? _rellenoFinal : _rellenoEstado;

            g.FillEllipse(relleno, centro.X - RadioEstado, centro.Y - RadioEstado, RadioEstado * 2, RadioEstado * 2);
            g.DrawEllipse(_penEstados, centro.X - RadioEstado, centro.Y - RadioEstado, RadioEstado * 2, RadioEstado * 2);

            if (esFinal)
            {
                const float separacion = 5f;
                g.DrawEllipse(_penFinales,
                    centro.X - RadioEstado - separacion, centro.Y - RadioEstado - separacion,
                    (RadioEstado + separacion) * 2, (RadioEstado + separacion) * 2);
            }

            using var fuente = new Font("Segoe UI", 11f, FontStyle.Bold);
            using var brochaTexto = new SolidBrush(Color.FromArgb(25, 45, 70));
            using var formato = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(estado, fuente, brochaTexto,
                new RectangleF(centro.X - RadioEstado, centro.Y - RadioEstado, RadioEstado * 2, RadioEstado * 2),
                formato);
        }
    }

    private static PointF PuntoEnCirculo(PointF centro, float radio, double grados)
    {
        double radianes = grados * Math.PI / 180d;
        return new PointF(
            centro.X + (float)(radio * Math.Cos(radianes)),
            centro.Y + (float)(radio * Math.Sin(radianes)));
    }

    private static void DibujarCabeza(Graphics g, PointF punta, PointF direccion, Color color)
    {
        double angulo = Math.Atan2(direccion.Y, direccion.X);
        const double apertura = 0.42;
        const float longitud = 11f;

        PointF lado1 = new(
            (float)(punta.X - longitud * Math.Cos(angulo - apertura)),
            (float)(punta.Y - longitud * Math.Sin(angulo - apertura)));
        PointF lado2 = new(
            (float)(punta.X - longitud * Math.Cos(angulo + apertura)),
            (float)(punta.Y - longitud * Math.Sin(angulo + apertura)));

        using var brocha = new SolidBrush(color);
        g.FillPolygon(brocha, new[] { punta, lado1, lado2 });
    }

    private static void DibujarEtiqueta(Graphics g, string texto, PointF centro, float tamano = 10f)
    {
        using var fuente = new Font("Segoe UI", tamano, FontStyle.Bold);
        SizeF medida = g.MeasureString(texto, fuente);
        var rectangulo = new RectangleF(
            centro.X - medida.Width / 2f - 4f,
            centro.Y - medida.Height / 2f - 2f,
            medida.Width + 8f,
            medida.Height + 4f);

        using var fondo = new SolidBrush(Color.FromArgb(238, 244, 250));
        using var borde = new Pen(Color.FromArgb(205, 218, 232));
        using var brochaTexto = new SolidBrush(Color.FromArgb(25, 50, 80));
        using var formato = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        g.FillRectangle(fondo, rectangulo);
        g.DrawRectangle(borde, rectangulo.X, rectangulo.Y, rectangulo.Width, rectangulo.Height);
        g.DrawString(texto, fuente, brochaTexto, rectangulo, formato);
    }
}
