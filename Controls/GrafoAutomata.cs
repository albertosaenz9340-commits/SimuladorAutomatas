using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using SimuladorAutomatas.Models;

namespace SimuladorAutomatas.Controls;

/// <summary>
/// Motor GDI+ del diagrama del AFD: título centrado, pentágono irregular de
/// cinco estados con relleno azul pastel, finales verdes con doble
/// circunferencia, indicador inicial saliente con etiqueta "Inicio", aristas
/// Bézier, bucles exteriores y etiquetas de texto plano sin superposiciones.
/// La definición matemática se lee siempre de la quíntupla M = (Q, Σ, δ, q0, F).
/// </summary>
public sealed class GrafoAutomata : Panel
{
    public const string TituloDiagrama = "Autómata finito determinista";

    private const float RadioEstado = 30f;
    private const float SeparacionBucle = 22f;
    private const float LongitudCabeza = 12f;
    private const float LongitudCabezaInicial = 13f;
    private const float MargenLienzo = 14f;
    private const float MargenSuperior = 34f;
    private const float MargenInferior = 34f;

    private const float SeparacionInicial = 10f;
    private const float LongitudFlechaInicial = 46f;
    private const float PaddingBotonInicial = 22f;
    private const float AnchoMinimoBotonInicial = 64f;
    private const float AltoMinimoBotonInicial = 24f;
    private const float SeparacionVerticalBoton = 8f;

    // Huecos del pentágono irregular: superior, medio izquierdo, medio derecho,
    // inferior izquierdo e inferior derecho (ver tabla de coordenadas relativas).
    private static readonly float[] CoordenadasX = { 0.45f, 0.23f, 0.66f, 0.31f, 0.58f };
    private static readonly float[] CoordenadasY = { 0.22f, 0.43f, 0.43f, 0.78f, 0.78f };

    private static readonly Color ColorRellenoEstado = Color.FromArgb(0xDC, 0xEA, 0xF8);
    private static readonly Color ColorContornoEstado = Color.FromArgb(0x34, 0x49, 0x5E);
    private static readonly Color ColorTextoEstado = Color.FromArgb(0x17, 0x2B, 0x43);
    private static readonly Color ColorTransicion = Color.FromArgb(0x26, 0x37, 0x47);
    private static readonly Color ColorRellenoFinal = Color.FromArgb(0x15, 0x94, 0x47);
    private static readonly Color ColorContornoFinal = Color.FromArgb(0x08, 0x78, 0x3C);
    private static readonly Color ColorInicial = Color.FromArgb(0x15, 0x65, 0xC0);

    private readonly Pen _penEstados = new(ColorContornoEstado, 2.2f);
    private readonly Pen _penFinalExterior = new(ColorContornoFinal, 2.4f);
    private readonly Pen _penFinalInterior = new(ColorContornoFinal, 1.4f);
    private readonly Pen _penTransiciones = new(ColorTransicion, 1.8f);
    private readonly Pen _penInicial = new(ColorInicial, 2.6f);
    private readonly Brush _rellenoEstado = new SolidBrush(ColorRellenoEstado);
    private readonly Brush _rellenoFinal = new SolidBrush(ColorRellenoFinal);
    private readonly Brush _rellenoBotonInicial = new SolidBrush(ColorInicial);

    private Automata? _automata;
    private PointF _desplazamiento = PointF.Empty;
    private float _escala = 1.6f;
    private bool _arrastrando;
    private Point _ultimoPunto;

    public event EventHandler? VistaCambiada;

    /// <summary>Rectángulos ocupados por las etiquetas en el último dibujo.</summary>
    public IReadOnlyList<RectangleF> UltimasEtiquetas { get; private set; } = Array.Empty<RectangleF>();

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

    /// <summary>
    /// Expone el cálculo de posiciones para pruebas y depuración. Los centros se
    /// devuelven en coordenadas del grafo, antes de aplicar zoom y desplazamiento.
    /// </summary>
    public static IReadOnlyDictionary<string, PointF> ObtenerPosiciones(
        Automata automata, Size tamaño, float escala = 1.6f)
    {
        ArgumentNullException.ThrowIfNull(automata);

        using var mapa = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(mapa);
        using var fuente = new Font("Segoe UI", 10f, FontStyle.Bold);
        SizeF medida = graphics.MeasureString("Inicio", fuente);
        float ancho = Math.Max(AnchoMinimoBotonInicial, medida.Width + PaddingBotonInicial);
        float alto = Math.Max(AltoMinimoBotonInicial, medida.Height + 10f);

        return CalcularDiseno(automata, ancho, alto, tamaño,
            Math.Clamp(escala, 0.5f, 2.5f)).Posiciones;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _penEstados.Dispose();
            _penFinalExterior.Dispose();
            _penFinalInterior.Dispose();
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
        _escala = Math.Clamp(nuevo, 0.5f, 2.5f);
        Invalidate();
        VistaCambiada?.Invoke(this, EventArgs.Empty);
    }

    private void Dibujar(object? sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Color.White);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        DibujarTitulo(g);
        UltimasEtiquetas = Array.Empty<RectangleF>();

        if (_automata is null || _automata.Estados.Count == 0)
        {
            DibujarMensajeVacio(g);
            return;
        }

        // Se mide antes de aplicar la transformación: el ancho del rectángulo
        // "Inicio" se deriva siempre del texto, nunca de un valor fijo.
        using var fuenteBoton = new Font("Segoe UI", 10f, FontStyle.Bold);
        SizeF medida = g.MeasureString("Inicio", fuenteBoton);
        float anchoBoton = Math.Max(AnchoMinimoBotonInicial, medida.Width + PaddingBotonInicial);
        float altoBoton = Math.Max(AltoMinimoBotonInicial, medida.Height + 10f);

        Diseno diseno = CalcularDiseno(_automata, anchoBoton, altoBoton, ClientSize, _escala);

        g.TranslateTransform(
            ClientSize.Width / 2f - diseno.CompensacionX * _escala + _desplazamiento.X,
            ClientSize.Height / 2f - diseno.CompensacionY * _escala + _desplazamiento.Y);
        g.ScaleTransform(_escala, _escala);

        var etiquetas = new List<RectangleF>();
        DibujarTransiciones(g, _automata, diseno, etiquetas);
        DibujarEstados(g, _automata, diseno, fuenteBoton);
        UltimasEtiquetas = etiquetas;
    }

    private void DibujarTitulo(Graphics g)
    {
        using var fuente = new Font("Segoe UI", 15f, FontStyle.Regular);
        using var brocha = new SolidBrush(Color.Black);
        using var formato = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString(TituloDiagrama, fuente, brocha,
            new RectangleF(0f, 6f, ClientSize.Width, 32f), formato);
    }

    private void DibujarMensajeVacio(Graphics g)
    {
        using var fuente = new Font("Segoe UI", 10f);
        using var brocha = new SolidBrush(Color.FromArgb(120, 132, 148));
        using var formato = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.Word
        };
        g.DrawString("Construya un autómata para visualizar su diagrama.", fuente, brocha,
            new RectangleF(MargenLienzo, MargenLienzo,
                Math.Max(0, ClientSize.Width - MargenLienzo * 2),
                Math.Max(0, ClientSize.Height - MargenLienzo * 2)), formato);
    }

    private static Diseno CalcularDiseno(
        Automata automata, float anchoBoton, float altoBoton, Size tamaño, float escala)
    {
        var diseno = new Diseno();
        int cantidad = automata.Estados.Count;
        if (cantidad == 0)
        {
            return diseno;
        }

        float mitadAncho = Math.Max(40f, (tamaño.Width / 2f - MargenLienzo) / escala);
        float mitadAlto = Math.Max(40f,
            (tamaño.Height / 2f - MargenSuperior - MargenInferior) / escala);
        float anchoArea = mitadAncho * 2f;
        float altoArea = mitadAlto * 2f;

        PointF Punto(int hueco) => new(
            (CoordenadasX[hueco] - 0.5f) * anchoArea,
            (CoordenadasY[hueco] - 0.5f) * altoArea);

        if (cantidad == 5)
        {
            // Asignación por papel, nunca por nombre: q0 ocupa el flanco derecho
            // y el único estado final el inferior derecho, como en la referencia.
            var ocupados = new bool[5];
            int indiceInicial = automata.EsEstadoConocido(automata.EstadoInicial)
                ? automata.ObtenerIndice(automata.EstadoInicial)
                : -1;
            if (indiceInicial >= 0)
            {
                string estadoInicial = automata.Estados[indiceInicial];
                diseno.Slots[estadoInicial] = 2;
                diseno.Posiciones[estadoInicial] = Punto(2);
                ocupados[2] = true;
            }

            if (automata.EstadosFinales.Count == 1 &&
                automata.EsEstadoConocido(automata.EstadosFinales[0]) &&
                !diseno.Posiciones.ContainsKey(automata.EstadosFinales[0]))
            {
                string final = automata.EstadosFinales[0];
                diseno.Slots[final] = 4;
                diseno.Posiciones[final] = Punto(4);
                ocupados[4] = true;
            }

            int hueco = 0;
            foreach (string estado in automata.Estados)
            {
                if (diseno.Posiciones.ContainsKey(estado))
                {
                    continue;
                }

                while (hueco < 5 && ocupados[hueco])
                {
                    hueco++;
                }

                if (hueco >= 5)
                {
                    break;
                }

                diseno.Slots[estado] = hueco;
                diseno.Posiciones[estado] = Punto(hueco);
                ocupados[hueco] = true;
            }
        }
        else
        {
            // Distribución alternativa determinista: el estado inicial arranca
            // a la derecha y el resto se reparte por igual en una elipse.
            int indiceInicial = automata.EsEstadoConocido(automata.EstadoInicial)
                ? automata.ObtenerIndice(automata.EstadoInicial)
                : 0;
            for (int i = 0; i < cantidad; i++)
            {
                string estado = automata.Estados[i];
                double angulo = -2d * Math.PI * (i - indiceInicial) / cantidad;
                diseno.Posiciones[estado] = new PointF(
                    (float)(Math.Cos(angulo) * anchoArea * 0.41d),
                    (float)(Math.Sin(angulo) * altoArea * 0.40d));
            }
        }

        if (diseno.Posiciones.TryGetValue(automata.EstadoInicial, out PointF centroInicial))
        {
            PointF inicioFlecha = new(centroInicial.X + RadioEstado + SeparacionInicial, centroInicial.Y);
            PointF finFlecha = new(inicioFlecha.X + LongitudFlechaInicial, centroInicial.Y);
            diseno.InicioFlecha = inicioFlecha;
            diseno.FinFlecha = finFlecha;
            diseno.BotonInicial = new RectangleF(
                (inicioFlecha.X + finFlecha.X) / 2f - anchoBoton / 2f,
                centroInicial.Y - SeparacionVerticalBoton - altoBoton,
                anchoBoton,
                altoBoton);
            diseno.CorredorFlechaInicial = new RectangleF(
                inicioFlecha.X, centroInicial.Y - 4f,
                finFlecha.X - inicioFlecha.X + LongitudCabezaInicial, 8f);

            float necesario = RadioEstado + SeparacionInicial +
                LongitudFlechaInicial / 2f + anchoBoton / 2f + 8f;
            float disponible = mitadAncho - centroInicial.X;
            diseno.CompensacionX = Math.Max(0f, necesario - disponible);
        }

        float yMin = diseno.Posiciones.Values.Min(p => p.Y);
        float yMax = diseno.Posiciones.Values.Max(p => p.Y);
        float necesarioVertical = RadioEstado + SeparacionBucle + 14f;
        float faltaArriba = Math.Max(0f, necesarioVertical - (mitadAlto + yMin));
        float faltaAbajo = Math.Max(0f, necesarioVertical - (mitadAlto - yMax));
        diseno.CompensacionY = Math.Max(faltaArriba, faltaAbajo);

        return diseno;
    }

    private sealed class Diseno
    {
        public Dictionary<string, PointF> Posiciones { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> Slots { get; } = new(StringComparer.Ordinal);
        public RectangleF BotonInicial { get; set; }
        public RectangleF CorredorFlechaInicial { get; set; }
        public PointF InicioFlecha { get; set; }
        public PointF FinFlecha { get; set; }
        public float CompensacionX { get; set; }
        public float CompensacionY { get; set; }
    }

    private void DibujarTransiciones(
        Graphics g, Automata automata, Diseno diseno, List<RectangleF> etiquetas)
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
                if (string.Equals(origen, destino, StringComparison.Ordinal))
                {
                    DibujarBucle(g, diseno, origen, etiqueta, etiquetas);
                }
                else
                {
                    DibujarCurva(g, automata, diseno, origen, destino, etiqueta, etiquetas);
                }
            }
        }
    }

    private void DibujarCurva(
        Graphics g,
        Automata automata,
        Diseno diseno,
        string origen,
        string destino,
        string etiqueta,
        List<RectangleF> etiquetas)
    {
        PointF origenPunto = diseno.Posiciones[origen];
        PointF destinoPunto = diseno.Posiciones[destino];
        float dx = destinoPunto.X - origenPunto.X;
        float dy = destinoPunto.Y - origenPunto.Y;
        float longitud = (float)Math.Sqrt(dx * dx + dy * dy);
        if (longitud < 1f)
        {
            return;
        }

        float ux = dx / longitud;
        float uy = dy / longitud;
        PointF normal = new(-uy, ux);

        PointF inicio = new(origenPunto.X + ux * (RadioEstado + 1f),
            origenPunto.Y + uy * (RadioEstado + 1f));
        PointF fin = new(destinoPunto.X - ux * (RadioEstado + 2f),
            destinoPunto.Y - uy * (RadioEstado + 2f));

        bool reciprocada = automata.Alfabeto
            .Any(s => automata.ObtenerDestino(destino, s) == origen);
        float magnitud = Math.Clamp(longitud * 0.24f, 28f, 76f);
        if (reciprocada)
        {
            magnitud *= 1.2f;
        }

        var (controlPos, control2Pos) = Controles(inicio, fin, dx, dy, normal, magnitud);
        var (controlNeg, control2Neg) = Controles(inicio, fin, dx, dy, normal, -magnitud);

        float signo;
        if (reciprocada)
        {
            signo = string.CompareOrdinal(origen, destino) > 0 ? -1f : 1f;
        }
        else
        {
            float despejePos = Despeje(inicio, controlPos, control2Pos, fin, diseno, origen, destino);
            float despejeNeg = Despeje(inicio, controlNeg, control2Neg, fin, diseno, origen, destino);
            signo = despejePos >= despejeNeg ? 1f : -1f;
        }

        (PointF control1, PointF control2) = signo > 0
            ? (controlPos, control2Pos)
            : (controlNeg, control2Neg);

        g.DrawBezier(_penTransiciones, inicio, control1, control2, fin);
        DibujarCabeza(g, fin, new PointF(fin.X - control2.X, fin.Y - control2.Y),
            ColorTransicion, LongitudCabeza);

        PointF mitad = PuntoBezier(inicio, control1, control2, fin, 0.5f);
        DibujarEtiquetaSegura(g, etiqueta, mitad,
            new PointF(normal.X * signo, normal.Y * signo),
            new PointF(ux, uy), diseno, etiquetas);
    }

    private static (PointF Control1, PointF Control2) Controles(
        PointF inicio, PointF fin, float dx, float dy, PointF normal, float curva)
    {
        return (
            new PointF(inicio.X + dx / 3f + normal.X * curva,
                       inicio.Y + dy / 3f + normal.Y * curva),
            new PointF(inicio.X + 2f * dx / 3f + normal.X * curva,
                       inicio.Y + 2f * dy / 3f + normal.Y * curva));
    }

    private static float Despeje(
        PointF inicio, PointF control1, PointF control2, PointF fin,
        Diseno diseno, string origen, string destino)
    {
        float despeje = float.MaxValue;
        for (int i = 1; i < 8; i++)
        {
            PointF punto = PuntoBezier(inicio, control1, control2, fin, i / 8f);
            foreach ((string estado, PointF centro) in diseno.Posiciones)
            {
                if (estado == origen || estado == destino)
                {
                    continue;
                }

                float distancia = Distancia(punto, centro) - RadioEstado;
                despeje = Math.Min(despeje, distancia);
            }
        }

        return despeje;
    }

    private static PointF PuntoBezier(
        PointF a, PointF b, PointF c, PointF d, float t)
    {
        float u = 1f - t;
        return new PointF(
            u * u * u * a.X + 3f * u * u * t * b.X + 3f * u * t * t * c.X + t * t * t * d.X,
            u * u * u * a.Y + 3f * u * u * t * b.Y + 3f * u * t * t * c.Y + t * t * t * d.Y);
    }

    private static float Distancia(PointF a, PointF b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private void DibujarBucle(
        Graphics g, Diseno diseno, string estado, string etiqueta, List<RectangleF> etiquetas)
    {
        PointF centro = diseno.Posiciones[estado];
        (float angulo, bool haciaAbajo) = OrientacionBucle(diseno, estado, centro);

        float signoY = haciaAbajo ? 1f : -1f;
        float altura = RadioEstado + SeparacionBucle;

        PointF inicio = Sumar(Rotar(new PointF(-22f, 22f * signoY), angulo), centro);
        PointF fin = Sumar(Rotar(new PointF(22f, 22f * signoY), angulo), centro);
        PointF control1 = Sumar(Rotar(new PointF(-40f, altura * signoY), angulo), centro);
        PointF control2 = Sumar(Rotar(new PointF(40f, altura * signoY), angulo), centro);

        g.DrawBezier(_penTransiciones, inicio, control1, control2, fin);
        DibujarCabeza(g, fin, new PointF(fin.X - control2.X, fin.Y - control2.Y),
            ColorTransicion, LongitudCabeza);

        PointF mitad = PuntoBezier(inicio, control1, control2, fin, 0.5f);
        PointF normal = Rotar(new PointF(0f, signoY), angulo);
        PointF tangente = Rotar(new PointF(1f, 0f), angulo);
        DibujarEtiquetaSegura(g, etiqueta, mitad, normal, tangente, diseno, etiquetas);
    }

    private static (float Angulo, bool HaciaAbajo) OrientacionBucle(
        Diseno diseno, string estado, PointF centro)
    {
        if (diseno.Slots.TryGetValue(estado, out int hueco))
        {
            return hueco switch
            {
                0 => (0f, false),    // superior: arco por encima
                1 => (-50f, false),  // medio izquierdo: arriba-izquierda
                2 => (35f, false),   // medio derecho: arriba-derecha, lejos de "Inicio"
                3 => (0f, true),     // inferior izquierdo: arco por debajo
                4 => (0f, true),     // inferior derecho: arco por debajo
                _ => (0f, false)
            };
        }

        double angulo = Math.Atan2(centro.Y, centro.X) * 180d / Math.PI;
        if (angulo > 20d && angulo < 160d)
        {
            return (0f, true);
        }

        if (angulo < -20d && angulo > -160d)
        {
            return (0f, false);
        }

        return (centro.X < 0f ? -50f : 35f, false);
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

    private void DibujarEtiquetaSegura(
        Graphics g,
        string texto,
        PointF puntoBase,
        PointF normal,
        PointF tangente,
        Diseno diseno,
        List<RectangleF> etiquetas)
    {
        using var fuente = new Font("Segoe UI", 10f, FontStyle.Regular);
        SizeF medida = g.MeasureString(texto, fuente);
        float ancho = Math.Max(medida.Width, 8f);
        float alto = Math.Max(medida.Height, 10f);

        float[] separaciones = { 16f, 24f, 34f, 46f, 60f };
        float[] desplazamientos = { 0f, ancho * 0.65f, -ancho * 0.65f, ancho * 1.3f, -ancho * 1.3f };

        foreach (float separacion in separaciones)
        {
            foreach (float lado in new[] { 1f, -1f })
            {
                foreach (float desplazamiento in desplazamientos)
                {
                    PointF punto = new(
                        puntoBase.X + normal.X * separacion * lado + tangente.X * desplazamiento,
                        puntoBase.Y + normal.Y * separacion * lado + tangente.Y * desplazamiento);
                    var rectangulo = new RectangleF(
                        punto.X - ancho / 2f, punto.Y - alto / 2f, ancho, alto);

                    if (!EsLibre(rectangulo, diseno, etiquetas))
                    {
                        continue;
                    }

                    DibujarTextoPlano(g, texto, rectangulo, fuente);
                    etiquetas.Add(rectangulo);
                    return;
                }
            }
        }

        // Último recurso: la mayor separación sobre el lado natural de la curva.
        PointF reserva = new(
            puntoBase.X + normal.X * separaciones[^1],
            puntoBase.Y + normal.Y * separaciones[^1]);
        var rectanguloReserva = new RectangleF(
            reserva.X - ancho / 2f, reserva.Y - alto / 2f, ancho, alto);
        DibujarTextoPlano(g, texto, rectanguloReserva, fuente);
        etiquetas.Add(rectanguloReserva);
    }

    private static bool EsLibre(
        RectangleF rectangulo, Diseno diseno, List<RectangleF> etiquetas)
    {
        PointF centroEtiqueta = new(
            rectangulo.X + rectangulo.Width / 2f,
            rectangulo.Y + rectangulo.Height / 2f);
        float radioSeguro = RadioEstado + Math.Max(rectangulo.Width, rectangulo.Height) / 2f + 6f;

        foreach (PointF centro in diseno.Posiciones.Values)
        {
            if (Distancia(centroEtiqueta, centro) < radioSeguro)
            {
                return false;
            }
        }

        if (RectangleF.Inflate(diseno.BotonInicial, 6f, 6f).IntersectsWith(rectangulo) ||
            RectangleF.Inflate(diseno.CorredorFlechaInicial, 4f, 4f).IntersectsWith(rectangulo))
        {
            return false;
        }

        foreach (RectangleF otra in etiquetas)
        {
            if (RectangleF.Inflate(otra, 4f, 4f).IntersectsWith(rectangulo))
            {
                return false;
            }
        }

        return true;
    }

    private void DibujarEstados(
        Graphics g, Automata automata, Diseno diseno, Font fuenteBoton)
    {
        if (automata.EsEstadoConocido(automata.EstadoInicial))
        {
            DibujarIndicadorInicial(g, diseno, fuenteBoton);
        }

        foreach (string estado in automata.Estados)
        {
            PointF centro = diseno.Posiciones[estado];
            bool esFinal = automata.EsFinal(estado);
            var circulo = new RectangleF(
                centro.X - RadioEstado, centro.Y - RadioEstado, RadioEstado * 2f, RadioEstado * 2f);

            g.FillEllipse(esFinal ? _rellenoFinal : _rellenoEstado, circulo);

            if (esFinal)
            {
                g.DrawEllipse(_penFinalExterior, circulo);
                float radioInterior = RadioEstado - 5f;
                g.DrawEllipse(_penFinalInterior, new RectangleF(
                    centro.X - radioInterior, centro.Y - radioInterior,
                    radioInterior * 2f, radioInterior * 2f));
            }
            else
            {
                g.DrawEllipse(_penEstados, circulo);
            }

            using var fuente = new Font("Segoe UI", 12f, FontStyle.Bold);
            using var brocha = new SolidBrush(esFinal ? Color.White : ColorTextoEstado);
            using var formato = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(estado, fuente, brocha, circulo, formato);
        }
    }

    private void DibujarIndicadorInicial(Graphics g, Diseno diseno, Font fuente)
    {
        g.DrawLine(_penInicial, diseno.InicioFlecha, diseno.FinFlecha);
        DibujarCabeza(g, diseno.FinFlecha, new PointF(1f, 0f),
            ColorInicial, LongitudCabezaInicial);

        using (var ruta = CrearRectanguloRedondeado(diseno.BotonInicial, 5f))
        {
            g.FillPath(_rellenoBotonInicial, ruta);
        }

        using var brocha = new SolidBrush(Color.White);
        using var formato = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString("Inicio", fuente, brocha, diseno.BotonInicial, formato);
    }

    private static GraphicsPath CrearRectanguloRedondeado(RectangleF rectangulo, float radio)
    {
        var ruta = new GraphicsPath();
        float diametro = Math.Min(radio * 2f, Math.Min(rectangulo.Width, rectangulo.Height));
        ruta.AddArc(rectangulo.X, rectangulo.Y, diametro, diametro, 180f, 90f);
        ruta.AddArc(rectangulo.Right - diametro, rectangulo.Y, diametro, diametro, 270f, 90f);
        ruta.AddArc(rectangulo.Right - diametro, rectangulo.Bottom - diametro, diametro, diametro, 0f, 90f);
        ruta.AddArc(rectangulo.X, rectangulo.Bottom - diametro, diametro, diametro, 90f, 90f);
        ruta.CloseFigure();
        return ruta;
    }

    private static void DibujarCabeza(
        Graphics g, PointF punta, PointF direccion, Color color, float longitud)
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

    private static void DibujarTextoPlano(Graphics g, string texto, RectangleF rectangulo, Font fuente)
    {
        using var brocha = new SolidBrush(Color.FromArgb(0x26, 0x37, 0x47));
        using var formato = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString(texto, fuente, brocha, rectangulo, formato);
    }
}
