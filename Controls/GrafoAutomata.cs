using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using SimuladorAutomatas.Models;

namespace SimuladorAutomatas.Controls;

/// <summary>
/// Motor GDI+ del diagrama del AFD. La geometría se calcula en dos fases: primero
/// las aristas y los bucles (compactos, con extremos sobre el perímetro y
/// orientación elegida por despeje) y después las etiquetas, ancladas al punto
/// medio de su curva. La definición matemática se lee siempre de la quíntupla
/// M = (Q, Σ, δ, q0, F); el motor nunca inventa ni suprime transiciones.
/// </summary>
public sealed class GrafoAutomata : Panel
{
    public const string TituloDiagrama = "Autómata finito determinista";
    public const float RadioEstado = 30f;

    private const float SeparacionBucle = 16f;
    private const float AnchoBucle = 24f;
    private const float AperturaBucleGrados = 36f;
    private const float LongitudCabeza = 11f;
    private const float LongitudCabezaInicial = 13f;
    private const float MargenLienzo = 14f;
    private const float MargenSuperior = 34f;
    private const float MargenInferior = 34f;

    private const float SeparacionInicial = 12f;
    private const float LongitudFlechaInicial = 46f;
    private const float PaddingBotonInicial = 22f;
    private const float AnchoMinimoBotonInicial = 64f;
    private const float AltoMinimoBotonInicial = 24f;
    private const float SeparacionVerticalBoton = 9f;

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

    public readonly record struct CurvaInfo(
        string Origen,
        string Destino,
        string Etiqueta,
        PointF Inicio,
        PointF Control1,
        PointF Control2,
        PointF Fin,
        RectangleF EtiquetaRectangulo);

    public readonly record struct BucleInfo(
        string Estado,
        string Etiqueta,
        PointF Inicio,
        PointF Control1,
        PointF Control2,
        PointF Fin,
        PointF Eje,
        RectangleF EtiquetaRectangulo);

    public sealed class GeometriaInfo
    {
        public required IReadOnlyDictionary<string, PointF> Posiciones { get; init; }
        public required IReadOnlyList<CurvaInfo> Curvas { get; init; }
        public required IReadOnlyList<BucleInfo> Bucles { get; init; }
        public required IReadOnlyList<RectangleF> Etiquetas { get; init; }
        public RectangleF ZonaInicial { get; init; }
    }

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

    /// <summary>Geometría completa del diagrama, expuesta para pruebas y depuración.</summary>
    public static GeometriaInfo ObtenerGeometria(
        Automata automata, Size tamaño, float escala = 1.6f)
    {
        ArgumentNullException.ThrowIfNull(automata);

        using var mapa = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(mapa);
        Geometria geometria = CalcularGeometria(
            graphics, automata, tamaño, Math.Clamp(escala, 0.5f, 2.5f));

        return new GeometriaInfo
        {
            Posiciones = geometria.Diseno.Posiciones,
            ZonaInicial = geometria.Diseno.ZonaInicial,
            Curvas = geometria.Aristas.Select(a => new CurvaInfo(
                a.Origen, a.Destino, a.Etiqueta, a.Inicio, a.Control1, a.Control2, a.Fin,
                a.RectanguloEtiqueta ?? RectangleF.Empty)).ToArray(),
            Bucles = geometria.Bucles.Select(b => new BucleInfo(
                b.Estado, b.Etiqueta, b.Inicio, b.Control1, b.Control2, b.Fin, b.Eje,
                b.RectanguloEtiqueta ?? RectangleF.Empty)).ToArray(),
            Etiquetas = geometria.Etiquetas.ToArray()
        };
    }

    /// <summary>Posiciones de los estados antes de aplicar zoom y desplazamiento.</summary>
    public static IReadOnlyDictionary<string, PointF> ObtenerPosiciones(
        Automata automata, Size tamaño, float escala = 1.6f) =>
        ObtenerGeometria(automata, tamaño, escala).Posiciones;

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
        _escala = Math.Clamp(_escala + (e.Delta > 0 ? 0.1f : -0.1f), 0.5f, 2.5f);
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

        Geometria geometria = CalcularGeometria(g, _automata, ClientSize, _escala);
        g.TranslateTransform(
            ClientSize.Width / 2f - geometria.Diseno.CompensacionX * _escala + _desplazamiento.X,
            ClientSize.Height / 2f - geometria.Diseno.CompensacionY * _escala + _desplazamiento.Y);
        g.ScaleTransform(_escala, _escala);

        DibujarAristas(g, geometria.Aristas);
        DibujarBucles(g, geometria.Bucles);
        DibujarEtiquetas(g, geometria);
        DibujarEstados(g, _automata, geometria.Diseno);
        DibujarIndicadorInicial(g, geometria.Diseno);

        UltimasEtiquetas = geometria.Etiquetas;
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

    private sealed class Diseno
    {
        public Dictionary<string, PointF> Posiciones { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> Slots { get; } = new(StringComparer.Ordinal);
        public RectangleF BotonInicial { get; set; }
        public RectangleF ZonaInicial { get; set; }
        public PointF InicioFlecha { get; set; }
        public PointF FinFlecha { get; set; }
        public float CompensacionX { get; set; }
        public float CompensacionY { get; set; }
    }

    private sealed class Arista
    {
        public string Origen = string.Empty;
        public string Destino = string.Empty;
        public string Etiqueta = string.Empty;
        public PointF Inicio;
        public PointF Control1;
        public PointF Control2;
        public PointF Fin;
        public PointF Normal;
        public float Signo;
        public RectangleF? RectanguloEtiqueta;
    }

    private sealed class Bucle
    {
        public string Estado = string.Empty;
        public string Etiqueta = string.Empty;
        public PointF Inicio;
        public PointF Control1;
        public PointF Control2;
        public PointF Fin;
        public PointF Eje;
        public RectangleF? RectanguloEtiqueta;
    }

    private sealed class Geometria
    {
        public Diseno Diseno { get; } = new();
        public List<Arista> Aristas { get; } = new();
        public List<Bucle> Bucles { get; } = new();
        public List<RectangleF> Etiquetas { get; } = new();
    }

    private static Geometria CalcularGeometria(
        Graphics graphics, Automata automata, Size tamaño, float escala)
    {
        var geometria = new Geometria();
        CalcularDiseno(automata, tamaño, escala, graphics, geometria.Diseno);
        CalcularAristas(automata, geometria.Diseno, geometria.Aristas);
        CalcularBucles(automata, geometria.Diseno, geometria.Aristas, geometria.Bucles);
        ColocarEtiquetas(graphics, geometria);
        return geometria;
    }

    private static void CalcularDiseno(
        Automata automata, Size tamaño, float escala, Graphics graphics, Diseno diseno)
    {
        int cantidad = automata.Estados.Count;
        if (cantidad == 0)
        {
            return;
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
            var ocupados = new bool[5];
            int indiceInicial = automata.EsEstadoConocido(automata.EstadoInicial)
                ? automata.ObtenerIndice(automata.EstadoInicial)
                : -1;
            if (indiceInicial >= 0)
            {
                string inicial = automata.Estados[indiceInicial];
                diseno.Slots[inicial] = 2;
                diseno.Posiciones[inicial] = Punto(2);
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
            int indiceInicial = automata.EsEstadoConocido(automata.EstadoInicial)
                ? automata.ObtenerIndice(automata.EstadoInicial)
                : 0;
            for (int i = 0; i < cantidad; i++)
            {
                double angulo = -2d * Math.PI * (i - indiceInicial) / cantidad;
                diseno.Posiciones[automata.Estados[i]] = new PointF(
                    (float)(Math.Cos(angulo) * anchoArea * 0.41d),
                    (float)(Math.Sin(angulo) * altoArea * 0.40d));
            }
        }

        using var fuenteBoton = new Font("Segoe UI", 10f, FontStyle.Bold);
        SizeF medida = graphics.MeasureString("Inicio", fuenteBoton);
        float anchoBoton = Math.Max(AnchoMinimoBotonInicial, medida.Width + PaddingBotonInicial);
        float altoBoton = Math.Max(AltoMinimoBotonInicial, medida.Height + 10f);

        if (diseno.Posiciones.TryGetValue(automata.EstadoInicial, out PointF centro))
        {
            PointF inicioFlecha = new(centro.X + RadioEstado + SeparacionInicial, centro.Y);
            PointF finFlecha = new(inicioFlecha.X + LongitudFlechaInicial, centro.Y);
            diseno.InicioFlecha = inicioFlecha;
            diseno.FinFlecha = finFlecha;
            diseno.BotonInicial = new RectangleF(
                (inicioFlecha.X + finFlecha.X) / 2f - anchoBoton / 2f,
                centro.Y - SeparacionVerticalBoton - altoBoton,
                anchoBoton,
                altoBoton);

            // Zona exclusiva: neither loops nor transition labels may enter it.
            RectangleF boton = RectangleF.Inflate(diseno.BotonInicial, 8f, 8f);
            RectangleF flecha = new(inicioFlecha.X - 6f, centro.Y - 7f,
                finFlecha.X - inicioFlecha.X + LongitudCabezaInicial + 12f, 14f);
            diseno.ZonaInicial = RectangleF.Union(boton, flecha);

            float necesario = RadioEstado + SeparacionInicial +
                LongitudFlechaInicial / 2f + anchoBoton / 2f + 8f;
            diseno.CompensacionX = Math.Max(0f, necesario - (mitadAncho - centro.X));
        }

        float yMin = diseno.Posiciones.Values.Min(p => p.Y);
        float yMax = diseno.Posiciones.Values.Max(p => p.Y);
        float necesarioVertical = RadioEstado + SeparacionBucle + 14f;
        diseno.CompensacionY = Math.Max(
            0f,
            Math.Max(necesarioVertical - (mitadAlto + yMin),
                     necesarioVertical - (mitadAlto - yMax)));
    }

    private static void CalcularAristas(
        Automata automata, Diseno diseno, List<Arista> aristas)
    {
        var dibujadas = new HashSet<(string Origen, string Destino)>();
        var signosReciprocos = new Dictionary<(string Origen, string Destino), float>();

        foreach (string origen in automata.Estados)
        {
            foreach (string destino in automata.Estados)
            {
                if (string.Equals(origen, destino, StringComparison.Ordinal))
                {
                    // Los bucles se calculan en CalcularBucles; dibujarlos aquí
                    // como Bézier degenerada duplicaría aristas y etiquetas.
                    continue;
                }

                List<char> simbolos = automata.Alfabeto
                    .Where(s => automata.ObtenerDestino(origen, s) == destino)
                    .ToList();

                if (simbolos.Count == 0 || !dibujadas.Add((origen, destino)))
                {
                    continue;
                }

                bool reciproca = automata.Alfabeto
                    .Any(s => automata.ObtenerDestino(destino, s) == origen);
                float magnitud = CalcularMagnitud(
                    Distancia(diseno.Posiciones[origen], diseno.Posiciones[destino]), reciproca);

                Arista positiva = ConstruirArista(
                    origen, destino, diseno.Posiciones, magnitud, 1f);
                Arista negativa = ConstruirArista(
                    origen, destino, diseno.Posiciones, magnitud, -1f);

                float signo;
                if (reciproca)
                {
                    var llave = (origen, destino);
                    if (!signosReciprocos.ContainsKey(llave))
                    {
                        // La normal se invierte al invertir la dirección: usar el
                        // mismo signo en ambos sentidos coloca las curvas en lados
                        // opuestos del eje. Signos opuestos las superpondrían.
                        float comboPos = Puntuacion(positiva, diseno, origen, destino) +
                                          Puntuacion(ConstruirArista(destino, origen,
                                              diseno.Posiciones, magnitud, 1f),
                                              diseno, destino, origen);
                        float comboNeg = Puntuacion(negativa, diseno, origen, destino) +
                                         Puntuacion(ConstruirArista(destino, origen,
                                             diseno.Posiciones, magnitud, -1f),
                                             diseno, destino, origen);
                        float baseSigno = comboPos >= comboNeg ? 1f : -1f;
                        signosReciprocos[llave] = baseSigno;
                        signosReciprocos[(destino, origen)] = baseSigno;
                    }

                    signo = signosReciprocos[llave];
                }
                else
                {
                    signo = Puntuacion(positiva, diseno, origen, destino) >=
                            Puntuacion(negativa, diseno, origen, destino) ? 1f : -1f;
                }

                Arista elegida = signo > 0 ? positiva : negativa;
                elegida.Origen = origen;
                elegida.Destino = destino;
                elegida.Etiqueta = string.Join(", ", simbolos);
                aristas.Add(elegida);
            }
        }
    }

    private static float CalcularMagnitud(float distanciaCentros, bool reciproca)
    {
        float util = Math.Max(20f, distanciaCentros - RadioEstado * 2f);
        float magnitud = Math.Clamp(util * 0.12f, 8f, 20f);
        return reciproca ? Math.Min(26f, magnitud * 1.25f) : magnitud;
    }

    /// <summary>
    /// Construye una Bézier compacta. Los extremos se refinan iterativamente para
    /// que caigan sobre el perímetro y la punta de flecha sea tangente a la curva.
    /// </summary>
    private static Arista ConstruirArista(
        string origen,
        string destino,
        Dictionary<string, PointF> posiciones,
        float magnitud,
        float signo)
    {
        PointF origenCentro = posiciones[origen];
        PointF destinoCentro = posiciones[destino];
        float dx = destinoCentro.X - origenCentro.X;
        float dy = destinoCentro.Y - origenCentro.Y;
        float longitud = (float)Math.Sqrt(dx * dx + dy * dy);
        if (longitud < 1f)
        {
            return new Arista
            {
                Inicio = origenCentro, Fin = destinoCentro,
                Control1 = origenCentro, Control2 = destinoCentro,
                Normal = new PointF(0f, -1f), Signo = signo
            };
        }

        PointF u = new(dx / longitud, dy / longitud);
        PointF normal = new(-u.Y, u.X);
        PointF inicio = Sumar(origenCentro, Escalar(u, RadioEstado));
        PointF fin = Restar(destinoCentro, Escalar(u, RadioEstado + 1f));
        float util = Math.Max(10f, longitud - RadioEstado * 2f - 1f);
        PointF control1 = Sumar(Sumar(inicio, Escalar(u, util / 3f)), Escalar(normal, magnitud * signo));
        PointF control2 = Sumar(Sumar(inicio, Escalar(u, util * 2f / 3f)), Escalar(normal, magnitud * signo));

        for (int i = 0; i < 4; i++)
        {
            inicio = Sumar(origenCentro, Escalar(Normalizar(Restar(control1, inicio)), RadioEstado));
            fin = Restar(destinoCentro, Escalar(Normalizar(Restar(fin, control2)), RadioEstado + 1f));

            float nx = fin.X - inicio.X;
            float ny = fin.Y - inicio.Y;
            float largo = (float)Math.Sqrt(nx * nx + ny * ny);
            if (largo < 1f)
            {
                break;
            }

            PointF nuevoU = new(nx / largo, ny / largo);
            PointF nuevaNormal = new(-nuevoU.Y, nuevoU.X);
            control1 = Sumar(Sumar(inicio, Escalar(nuevoU, largo / 3f)),
                Escalar(nuevaNormal, magnitud * signo));
            control2 = Sumar(Sumar(inicio, Escalar(nuevoU, largo * 2f / 3f)),
                Escalar(nuevaNormal, magnitud * signo));
            normal = nuevaNormal;
        }

        return new Arista
        {
            Inicio = inicio,
            Control1 = control1,
            Control2 = control2,
            Fin = fin,
            Normal = normal,
            Signo = signo
        };
    }

    private static float Puntuacion(Arista arista, Diseno diseno, string origen, string destino)
    {
        float puntuacion = float.MaxValue;
        for (int i = 1; i < 9; i++)
        {
            PointF punto = PuntoBezier(arista.Inicio, arista.Control1, arista.Control2, arista.Fin, i / 9f);
            if (diseno.ZonaInicial.Contains(punto))
            {
                puntuacion = -100f;
            }

            foreach ((string estado, PointF centro) in diseno.Posiciones)
            {
                if (estado == origen || estado == destino)
                {
                    continue;
                }

                puntuacion = Math.Min(puntuacion, Distancia(punto, centro) - RadioEstado - 4f);
            }
        }

        return puntuacion;
    }

    private static void CalcularBucles(
        Automata automata,
        Diseno diseno,
        List<Arista> aristas,
        List<Bucle> bucles)
    {
        foreach (string estado in automata.Estados)
        {
            string? etiqueta = ObtenerEtiquetaBucle(automata, estado);
            if (etiqueta is null)
            {
                continue;
            }

            PointF centro = diseno.Posiciones[estado];
            float[] candidatos = AngulosBucle(diseno, estado);
            Bucle? mejor = null;
            float mejorPuntuacion = float.MinValue;

            foreach (float angulo in candidatos)
            {
                Bucle candidato = ConstruirBucle(centro, angulo);
                float puntuacion = PuntuacionBucle(candidato, centro, estado, diseno, aristas, bucles);
                if (puntuacion > mejorPuntuacion)
                {
                    mejor = candidato;
                    mejorPuntuacion = puntuacion;
                }
            }

            if (mejor is not null)
            {
                mejor.Estado = estado;
                mejor.Etiqueta = etiqueta;
                bucles.Add(mejor);
            }
        }
    }

    private static string? ObtenerEtiquetaBucle(Automata automata, string estado)
    {
        List<char> simbolos = automata.Alfabeto
            .Where(s => automata.ObtenerDestino(estado, s) == estado)
            .ToList();
        return simbolos.Count == 0 ? null : string.Join(", ", simbolos);
    }

    private static float[] AngulosBucle(Diseno diseno, string estado)
    {
        if (diseno.Slots.TryGetValue(estado, out int hueco))
        {
            return hueco switch
            {
                0 => new[] { 0f, -20f, 20f },
                1 => new[] { -50f, -70f, -30f },
                2 => new[] { -45f, -30f, -60f, -15f },
                3 => new[] { 180f, 160f, 200f },
                4 => new[] { 180f, 160f, 200f },
                _ => new[] { 0f, -45f, 180f }
            };
        }

        PointF centro = diseno.Posiciones[estado];
        double angulo = Math.Atan2(centro.Y, centro.X) * 180d / Math.PI;
        if (angulo > 20d && angulo < 160d)
        {
            return new[] { 180f, 160f, 200f };
        }

        if (centro.X < 0f)
        {
            return new[] { -50f, -70f, -30f };
        }

        return new[] { -45f, -30f, -60f };
    }

    private static Bucle ConstruirBucle(PointF centro, float grados)
    {
        PointF eje = Direccion(grados);
        PointF perpendicular = new(-eje.Y, eje.X);
        double apertura = AperturaBucleGrados * Math.PI / 180d;
        float coseno = (float)Math.Cos(apertura);
        float seno = (float)Math.Sin(apertura);

        PointF inicio = Sumar(centro,
            Sumar(Escalar(eje, RadioEstado * coseno), Escalar(perpendicular, -RadioEstado * seno)));
        PointF fin = Sumar(centro,
            Sumar(Escalar(eje, RadioEstado * coseno), Escalar(perpendicular, RadioEstado * seno)));
        float altura = RadioEstado + SeparacionBucle;

        return new Bucle
        {
            Inicio = inicio,
            Fin = fin,
            Control1 = Sumar(centro, Sumar(Escalar(eje, altura), Escalar(perpendicular, -AnchoBucle))),
            Control2 = Sumar(centro, Sumar(Escalar(eje, altura), Escalar(perpendicular, AnchoBucle))),
            Eje = eje
        };
    }

    private static float PuntuacionBucle(
        Bucle bucle,
        PointF centro,
        string estado,
        Diseno diseno,
        List<Arista> aristas,
        List<Bucle> bucles)
    {
        float puntuacion = float.MaxValue;
        for (int i = 1; i < 9; i++)
        {
            PointF punto = PuntoBezier(bucle.Inicio, bucle.Control1, bucle.Control2, bucle.Fin, i / 9f);
            if (diseno.ZonaInicial.Contains(punto))
            {
                puntuacion = -100f;
            }

            foreach ((string otro, PointF centroOtro) in diseno.Posiciones)
            {
                if (otro == estado)
                {
                    continue;
                }

                puntuacion = Math.Min(puntuacion, Distancia(punto, centroOtro) - RadioEstado - 4f);
            }

            foreach (Arista arista in aristas)
            {
                puntuacion = Math.Min(puntuacion, DistanciaAArista(punto, arista) - 5f);
            }

            foreach (Bucle otro in bucles)
            {
                puntuacion = Math.Min(puntuacion, DistanciaABucle(punto, otro) - 5f);
            }
        }

        return puntuacion;
    }

    private static void ColocarEtiquetas(Graphics graphics, Geometria geometria)
    {
        using var fuente = new Font("Segoe UI", 10f, FontStyle.Regular);

        foreach (Arista arista in geometria.Aristas)
        {
            SizeF medida = graphics.MeasureString(arista.Etiqueta, fuente);
            arista.RectanguloEtiqueta = Colocar(
                graphics, arista.Etiqueta, medida, fuente, geometria,
                PuntoBezier(arista.Inicio, arista.Control1, arista.Control2, arista.Fin, 0.5f),
                arista.Normal, arista.Signo, arista, null);
            if (arista.RectanguloEtiqueta is RectangleF rect)
            {
                geometria.Etiquetas.Add(rect);
            }
        }

        foreach (Bucle bucle in geometria.Bucles)
        {
            SizeF medida = graphics.MeasureString(bucle.Etiqueta, fuente);
            bucle.RectanguloEtiqueta = Colocar(
                graphics, bucle.Etiqueta, medida, fuente, geometria,
                PuntoBezier(bucle.Inicio, bucle.Control1, bucle.Control2, bucle.Fin, 0.5f),
                bucle.Eje, 1f, null, bucle);
            if (bucle.RectanguloEtiqueta is RectangleF rect)
            {
                geometria.Etiquetas.Add(rect);
            }
        }
    }

    private static RectangleF Colocar(
        Graphics graphics,
        string texto,
        SizeF medida,
        Font fuente,
        Geometria geometria,
        PointF puntoCurva,
        PointF normal,
        float ladoPreferido,
        Arista? arista,
        Bucle? bucle)
    {
        float ancho = Math.Max(medida.Width, 8f);
        float alto = Math.Max(medida.Height, 10f);
        float baseSeparacion = alto / 2f + 7f;
        float[] separaciones =
        {
            baseSeparacion, baseSeparacion + 6f, baseSeparacion + 12f,
            baseSeparacion + 20f, baseSeparacion + 30f, baseSeparacion + 42f
        };
        float[] desplazamientos = { 0f, ancho * 0.6f, -ancho * 0.6f, ancho * 1.2f, -ancho * 1.2f };
        PointF tangente = new(-normal.Y, normal.X);

        RectangleF mejor = RectangleF.Empty;
        float mejorPenalizacion = float.MaxValue;

        foreach (float separacion in separaciones)
        {
            foreach (float lado in new[] { ladoPreferido, -ladoPreferido })
            {
                foreach (float desplazamiento in desplazamientos)
                {
                    PointF centro = Sumar(
                        Sumar(puntoCurva, Escalar(normal, separacion * lado)),
                        Escalar(tangente, desplazamiento));
                    var rectangulo = new RectangleF(
                        centro.X - ancho / 2f, centro.Y - alto / 2f, ancho, alto);

                    if (RectanguloLibre(rectangulo, geometria, arista, bucle))
                    {
                        return rectangulo;
                    }

                    float penalizacion = Penalizar(rectangulo, geometria, arista, bucle) +
                        separacion + Math.Abs(desplazamiento) * 0.5f +
                        (lado == ladoPreferido ? 0f : 10f);
                    if (penalizacion < mejorPenalizacion)
                    {
                        mejor = rectangulo;
                        mejorPenalizacion = penalizacion;
                    }
                }
            }
        }

        // último recurso: el candidato menos malo, nunca una posición sin evaluar
        return mejor;
    }

    private static float Penalizar(
        RectangleF rectangulo, Geometria geometria, Arista? arista, Bucle? bucle)
    {
        float penalizacion = 0f;
        PointF centro = new(rectangulo.X + rectangulo.Width / 2f, rectangulo.Y + rectangulo.Height / 2f);
        float radioSeguro = RadioEstado + Math.Max(rectangulo.Width, rectangulo.Height) / 2f + 5f;

        foreach (PointF nodo in geometria.Diseno.Posiciones.Values)
        {
            if (Distancia(centro, nodo) < radioSeguro)
            {
                penalizacion += 500f;
            }
        }

        if (RectangleF.Inflate(geometria.Diseno.ZonaInicial, 2f, 2f).IntersectsWith(rectangulo))
        {
            penalizacion += 400f;
        }

        foreach (Arista otra in geometria.Aristas)
        {
            if (!ReferenceEquals(otra, arista) && RectanguloCercaDeArista(rectangulo, otra))
            {
                penalizacion += 200f;
            }
        }

        foreach (Bucle otro in geometria.Bucles)
        {
            if (!ReferenceEquals(otro, bucle) && RectanguloCercaDeBucle(rectangulo, otro))
            {
                penalizacion += 200f;
            }
        }

        foreach (RectangleF otra in geometria.Etiquetas)
        {
            if (RectangleF.Inflate(otra, 2f, 2f).IntersectsWith(rectangulo))
            {
                penalizacion += 1000f;
            }
        }

        return penalizacion;
    }

    private static bool RectanguloLibre(
        RectangleF rectangulo, Geometria geometria, Arista? arista, Bucle? bucle)
    {
        PointF centroEtiqueta = new(
            rectangulo.X + rectangulo.Width / 2f,
            rectangulo.Y + rectangulo.Height / 2f);
        float radioSeguro = RadioEstado +
            Math.Max(rectangulo.Width, rectangulo.Height) / 2f + 5f;

        foreach (PointF centro in geometria.Diseno.Posiciones.Values)
        {
            if (Distancia(centroEtiqueta, centro) < radioSeguro)
            {
                return false;
            }
        }

        if (RectangleF.Inflate(geometria.Diseno.ZonaInicial, 2f, 2f).IntersectsWith(rectangulo))
        {
            return false;
        }

        foreach (Arista otra in geometria.Aristas)
        {
            if (ReferenceEquals(otra, arista))
            {
                continue;
            }

            if (RectanguloCercaDeArista(rectangulo, otra))
            {
                return false;
            }
        }

        foreach (Bucle otro in geometria.Bucles)
        {
            if (ReferenceEquals(otro, bucle))
            {
                continue;
            }

            if (RectanguloCercaDeBucle(rectangulo, otro))
            {
                return false;
            }
        }

        foreach (RectangleF otra in geometria.Etiquetas)
        {
            if (RectangleF.Inflate(otra, 2f, 2f).IntersectsWith(rectangulo))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RectanguloCercaDeArista(RectangleF rectangulo, Arista arista)
    {
        for (int i = 0; i <= 8; i++)
        {
            PointF punto = PuntoBezier(arista.Inicio, arista.Control1, arista.Control2, arista.Fin, i / 8f);
            if (DistanciaARectangulo(punto, rectangulo) < 3f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool RectanguloCercaDeBucle(RectangleF rectangulo, Bucle bucle)
    {
        for (int i = 0; i <= 8; i++)
        {
            PointF punto = PuntoBezier(bucle.Inicio, bucle.Control1, bucle.Control2, bucle.Fin, i / 8f);
            if (DistanciaARectangulo(punto, rectangulo) < 3f)
            {
                return true;
            }
        }

        return false;
    }

    private static float DistanciaAArista(PointF punto, Arista arista)
    {
        float minima = float.MaxValue;
        for (int i = 0; i <= 8; i++)
        {
            minima = Math.Min(minima,
                Distancia(punto, PuntoBezier(
                    arista.Inicio, arista.Control1, arista.Control2, arista.Fin, i / 8f)));
        }

        return minima;
    }

    private static float DistanciaABucle(PointF punto, Bucle bucle)
    {
        float minima = float.MaxValue;
        for (int i = 0; i <= 8; i++)
        {
            minima = Math.Min(minima,
                Distancia(punto, PuntoBezier(
                    bucle.Inicio, bucle.Control1, bucle.Control2, bucle.Fin, i / 8f)));
        }

        return minima;
    }

    private static float DistanciaARectangulo(PointF punto, RectangleF rectangulo)
    {
        float dx = Math.Max(rectangulo.Left - punto.X, Math.Max(punto.X - rectangulo.Right, 0f));
        float dy = Math.Max(rectangulo.Top - punto.Y, Math.Max(punto.Y - rectangulo.Bottom, 0f));
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private void DibujarAristas(Graphics g, List<Arista> aristas)
    {
        foreach (Arista arista in aristas)
        {
            g.DrawBezier(_penTransiciones, arista.Inicio, arista.Control1, arista.Control2, arista.Fin);
            DibujarCabeza(g, arista.Fin, Restar(arista.Fin, arista.Control2), ColorTransicion);
        }
    }

    private void DibujarBucles(Graphics g, List<Bucle> bucles)
    {
        foreach (Bucle bucle in bucles)
        {
            g.DrawBezier(_penTransiciones, bucle.Inicio, bucle.Control1, bucle.Control2, bucle.Fin);
            DibujarCabeza(g, bucle.Fin, Restar(bucle.Fin, bucle.Control2), ColorTransicion);
        }
    }

    private void DibujarEtiquetas(Graphics g, Geometria geometria)
    {
        using var fuente = new Font("Segoe UI", 10f, FontStyle.Regular);
        foreach (Arista arista in geometria.Aristas)
        {
            if (arista.RectanguloEtiqueta is RectangleF rect)
            {
                DibujarTextoPlano(g, arista.Etiqueta, rect, fuente);
            }
        }

        foreach (Bucle bucle in geometria.Bucles)
        {
            if (bucle.RectanguloEtiqueta is RectangleF rect)
            {
                DibujarTextoPlano(g, bucle.Etiqueta, rect, fuente);
            }
        }
    }

    private void DibujarEstados(Graphics g, Automata automata, Diseno diseno)
    {
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

    private void DibujarIndicadorInicial(Graphics g, Diseno diseno)
    {
        g.DrawLine(_penInicial, diseno.InicioFlecha, diseno.FinFlecha);
        DibujarCabeza(g, diseno.FinFlecha, new PointF(1f, 0f),
            ColorInicial, LongitudCabezaInicial);

        using (var ruta = CrearRectanguloRedondeado(diseno.BotonInicial, 5f))
        {
            g.FillPath(_rellenoBotonInicial, ruta);
        }

        using var fuente = new Font("Segoe UI", 10f, FontStyle.Bold);
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

    private static PointF Sumar(PointF a, PointF b) => new(a.X + b.X, a.Y + b.Y);

    private static PointF Restar(PointF a, PointF b) => new(a.X - b.X, a.Y - b.Y);

    private static PointF Escalar(PointF a, float factor) => new(a.X * factor, a.Y * factor);

    private static float Distancia(PointF a, PointF b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private static PointF Normalizar(PointF a)
    {
        float longitud = (float)Math.Sqrt(a.X * a.X + a.Y * a.Y);
        return longitud < 0.0001f
            ? new PointF(1f, 0f)
            : new PointF(a.X / longitud, a.Y / longitud);
    }

    private static PointF Direccion(float grados)
    {
        double radianes = grados * Math.PI / 180d;
        return new PointF((float)Math.Sin(radianes), -(float)Math.Cos(radianes));
    }

    private static PointF PuntoBezier(PointF p0, PointF p1, PointF p2, PointF p3, float t)
    {
        float u = 1f - t;
        return new PointF(
            u * u * u * p0.X + 3f * u * u * t * p1.X + 3f * u * t * t * p2.X + t * t * t * p3.X,
            u * u * u * p0.Y + 3f * u * u * t * p1.Y + 3f * u * t * t * p2.Y + t * t * t * p3.Y);
    }
}
