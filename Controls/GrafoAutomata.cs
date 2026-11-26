using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using SimuladorAutomatas.Models;

namespace SimuladorAutomatas.Controls;

/// <summary>
/// Control personalizado que dibuja el AFD con GDI+: cinco círculos en un anillo
/// abierto, bordes azul marino, estado final verde sólido, indicador "Inicio",
/// transiciones como arcos de Bézier con etiquetas en texto plano y bucles
/// orientados hacia el exterior. Permite desplazar y ampliar el dibujo.
/// </summary>
public sealed class GrafoAutomata : Panel
{
    private const float RadioEstado = 30f;
    private const float SeparacionBucle = 42f;
    private const float LongitudCabeza = 13f;
    private const float LongitudCabezaInicial = 14f;
    private const float CentroY = 18f;

    private const float LongitudFlechaInicial = 38f;
    private const float AnchoBotonInicial = 62f;
    private const float AltoBotonInicial = 28f;

    private static readonly Color ColorNodo = Color.FromArgb(0x1B, 0x4F, 0x72);
    private static readonly Color ColorInicial = Color.FromArgb(0x15, 0x65, 0xC0);
    private static readonly Color ColorFinal = Color.FromArgb(0x14, 0x6C, 0x32);
    private static readonly Color ColorTexto = Color.FromArgb(0x11, 0x18, 0x27);
    private static readonly Color ColorTitulo = Color.FromArgb(0x1E, 0x24, 0x2B);

    private readonly Pen _penEstados = new(ColorNodo, 2.8f);
    private readonly Pen _penFinal = new(ColorFinal, 2.8f);
    private readonly Pen _penTransiciones = new(ColorNodo, 2f);
    private readonly Pen _penInicial = new(ColorNodo, 2.2f);
    private readonly Brush _rellenoEstado = new SolidBrush(Color.White);
    private readonly Brush _rellenoFinal = new SolidBrush(ColorFinal);
    private readonly Brush _rellenoBotonInicial = new SolidBrush(ColorInicial);

    private Automata? _automata;
    private PointF _desplazamiento = PointF.Empty;
    private float _escala = 1.6f;
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
        _escala = 1.6f;
        Invalidate();
        VistaCambiada?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _penEstados.Dispose();
            _penFinal.Dispose();
            _penTransiciones.Dispose();
            _penInicial.Dispose();
            _rellenoEstado.Dispose();
            _rellenoFinal.Dispose();
            _rellenoBotonInicial.Dispose();
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
        g.Clear(Color.White);
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

        using (var fuenteTitulo = new Font("Segoe UI", 13f, FontStyle.Bold))
        using (var brochaTitulo = new SolidBrush(ColorTitulo))
        {
            g.DrawString("Automata finito determinista", fuenteTitulo, brochaTitulo, 14f, 10f);
        }

        g.TranslateTransform(ClientSize.Width / 2f + _desplazamiento.X,
                             ClientSize.Height / 2f + CentroY + _desplazamiento.Y);
        g.ScaleTransform(_escala, _escala);

        Dictionary<string, PointF> posiciones = CalcularPosiciones(_automata);
        DibujarTransiciones(g, _automata, posiciones);
        DibujarEstados(g, _automata, posiciones);
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

        // Anillo pentagonal abierto: s0 a la derecha, s1 arriba ligeramente a
        // la derecha, s2 a media altura a la izquierda y s3/s4 en la base.
        // El radio deja hueco interior para los arcos y exterior para los
        // bucles y el botón Inicio.
        float radio = Math.Max(66f, cantidad * 15f);
        double[]? angulosPersonalizados = cantidad == 5
            ? new[] { 0d, -78d, -162d, -234d, -306d }
            : null;

        for (int i = 0; i < cantidad; i++)
        {
            double angulo = angulosPersonalizados is null
                ? -2 * Math.PI * i / cantidad
                : angulosPersonalizados[i] * Math.PI / 180d;
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
                    bool esInicial = string.Equals(origen, automata.EstadoInicial, StringComparison.Ordinal);
                    DibujarBucle(g, puntoOrigen, etiqueta, esInicial);
                }
                else
                {
                    bool reciprocada = automata.Alfabeto.Any(s => automata.ObtenerDestino(destino, s) == origen);
                    float signo = reciprocada && string.CompareOrdinal(origen, destino) > 0 ? -1f : 1f;
                    DibujarArco(g, puntoOrigen, puntoDestino, etiqueta, signo);
                }
            }
        }
    }

    private void DibujarArco(
        Graphics g, PointF origen, PointF destino, string etiqueta, float signo)
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

        PointF inicio = new(origen.X + ux * (RadioEstado + 1f), origen.Y + uy * (RadioEstado + 1f));
        PointF fin = new(destino.X - ux * (RadioEstado + 2f), destino.Y - uy * (RadioEstado + 2f));

        // Toda transición es curva; los pares recíprocos usan lados opuestos.
        float curva = Math.Min(90f, Math.Max(38f, longitud * 0.3f)) * signo;
        PointF control1 = new(inicio.X + dx / 3f + px * curva, inicio.Y + dy / 3f + py * curva);
        PointF control2 = new(inicio.X + 2f * dx / 3f + px * curva, inicio.Y + 2f * dy / 3f + py * curva);

        g.DrawBezier(_penTransiciones, inicio, control1, control2, fin);
        DibujarCabeza(g, fin, new PointF(fin.X - control2.X, fin.Y - control2.Y), ColorNodo);

        CalcularMitad(out float mx, out float my, control1, control2);
        DibujarTextoPlano(g, etiqueta,
            new PointF(mx + px * signo * 16f, my + py * signo * 16f), 10f);
    }

    private static void CalcularMitad(out float x, out float y, PointF a, PointF b)
    {
        x = (a.X + b.X) / 2f;
        y = (a.Y + b.Y) / 2f;
    }

    private void DibujarBucle(Graphics g, PointF centro, string etiqueta, bool esInicial)
    {
        // Orientación hacia el exterior del anillo. El estado inicial se gira
        // hacia arriba-derecha para no chocar con la flecha de "Inicio".
        float rotacion = AnguloBucle(centro, esInicial);

        PointF inicio = Rotar(new PointF(22f, -22f), rotacion);
        PointF fin = Rotar(new PointF(-22f, -22f), rotacion);
        PointF control1 = Rotar(new PointF(40f, -(RadioEstado + SeparacionBucle)), rotacion);
        PointF control2 = Rotar(new PointF(-40f, -(RadioEstado + SeparacionBucle)), rotacion);
        PointF puntoEtiqueta = Rotar(new PointF(0f, -(RadioEstado + SeparacionBucle + 16f)), rotacion);

        inicio = Sumar(inicio, centro);
        fin = Sumar(fin, centro);
        control1 = Sumar(control1, centro);
        control2 = Sumar(control2, centro);
        puntoEtiqueta = Sumar(puntoEtiqueta, centro);

        g.DrawBezier(_penTransiciones, inicio, control1, control2, fin);
        DibujarCabeza(g, fin, new PointF(fin.X - control2.X, fin.Y - control2.Y), ColorNodo);
        DibujarTextoPlano(g, etiqueta, puntoEtiqueta, 10f);
    }

    private static float AnguloBucle(PointF centro, bool esInicial)
    {
        if (esInicial)
        {
            return 25f;
        }

        double angulo = Math.Atan2(centro.Y, centro.X) * 180d / Math.PI;
        if (angulo < -90d)
        {
            return -50f;   // flanco izquierdo: lazo hacia arriba-izquierda
        }

        if (angulo < 0d)
        {
            return 10f;    // zona superior: lazo hacia arriba
        }

        return 180f;       // zona inferior: lazo hacia abajo
    }

    private static PointF Rotar(PointF punto, float grados)
    {
        double radianes = grados * Math.PI / 180d;
        double coseno = Math.Cos(radianes);
        double seno = Math.Sin(radianes);
        return new PointF(
            (float)(punto.X * coseno - punto.Y * seno),
            (float)(punto.X * seno + punto.Y * coseno));
    }

    private static PointF Sumar(PointF a, PointF b) => new(a.X + b.X, a.Y + b.Y);

    private void DibujarEstados(
        Graphics g,
        Automata automata,
        Dictionary<string, PointF> posiciones)
    {
        foreach (string estado in automata.Estados)
        {
            PointF centro = posiciones[estado];
            bool esInicial = string.Equals(estado, automata.EstadoInicial, StringComparison.Ordinal);
            bool esFinal = automata.EsFinal(estado);

            if (esInicial)
            {
                DibujarIndicadorInicial(g, centro);
            }

            g.FillEllipse(esFinal ? _rellenoFinal : _rellenoEstado,
                centro.X - RadioEstado, centro.Y - RadioEstado, RadioEstado * 2, RadioEstado * 2);
            g.DrawEllipse(esFinal ? _penFinal : _penEstados,
                centro.X - RadioEstado, centro.Y - RadioEstado, RadioEstado * 2, RadioEstado * 2);

            using var fuente = new Font("Segoe UI", 12f, FontStyle.Bold);
            using var brochaTexto = new SolidBrush(esFinal ? Color.White : ColorTexto);
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

    private void DibujarIndicadorInicial(Graphics g, PointF centro)
    {
        float puntaX = centro.X + RadioEstado + 2f;
        float colaX = centro.X + RadioEstado + LongitudFlechaInicial;

        g.DrawLine(_penInicial, new PointF(colaX, centro.Y), new PointF(puntaX, centro.Y));
        DibujarCabeza(g, new PointF(puntaX, centro.Y), new PointF(-1f, 0f), ColorNodo, LongitudCabezaInicial);

        var boton = new RectangleF(colaX, centro.Y - AltoBotonInicial / 2f,
            AnchoBotonInicial, AltoBotonInicial);
        using (var ruta = CrearRectanguloRedondeado(boton, 7f))
        {
            g.FillPath(_rellenoBotonInicial, ruta);
        }

        using var fuente = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        using var brocha = new SolidBrush(Color.White);
        using var formato = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString("Inicio", fuente, brocha, boton, formato);
    }

    private static GraphicsPath CrearRectanguloRedondeado(RectangleF rectangulo, float radio)
    {
        var ruta = new GraphicsPath();
        float diametro = radio * 2f;
        ruta.AddArc(rectangulo.X, rectangulo.Y, diametro, diametro, 180f, 90f);
        ruta.AddArc(rectangulo.Right - diametro, rectangulo.Y, diametro, diametro, 270f, 90f);
        ruta.AddArc(rectangulo.Right - diametro, rectangulo.Bottom - diametro, diametro, diametro, 0f, 90f);
        ruta.AddArc(rectangulo.X, rectangulo.Bottom - diametro, diametro, diametro, 90f, 90f);
        ruta.CloseFigure();
        return ruta;
    }

    private static void DibujarCabeza(
        Graphics g, PointF punta, PointF direccion, Color color, float longitud = LongitudCabeza)
    {
        double angulo = Math.Atan2(direccion.Y, direccion.X);
        const double apertura = 0.36;

        PointF lado1 = new(
            (float)(punta.X - longitud * Math.Cos(angulo - apertura)),
            (float)(punta.Y - longitud * Math.Sin(angulo - apertura)));
        PointF lado2 = new(
            (float)(punta.X - longitud * Math.Cos(angulo + apertura)),
            (float)(punta.Y - longitud * Math.Sin(angulo + apertura)));

        using var brocha = new SolidBrush(color);
        g.FillPolygon(brocha, new[] { punta, lado1, lado2 });
    }

    private static void DibujarTextoPlano(
        Graphics g, string texto, PointF centro, float tamano, Color? colorTexto = null)
    {
        using var fuente = new Font("Segoe UI", tamano, FontStyle.Regular);
        using var brocha = new SolidBrush(colorTexto ?? ColorTexto);
        using var formato = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        SizeF medida = g.MeasureString(texto, fuente);
        var rectangulo = new RectangleF(
            centro.X - medida.Width / 2f,
            centro.Y - medida.Height / 2f,
            medida.Width,
            medida.Height);
        g.DrawString(texto, fuente, brocha, rectangulo, formato);
    }
}
