using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using SimuladorAutomatas.Controls;
using SimuladorAutomatas.Forms;
using SimuladorAutomatas.Models;
using SimuladorAutomatas.Services;

int comprobaciones = 0;
int fallos = 0;

void Comprobar(string nombre, bool condicion, string detalle = "")
{
    comprobaciones++;
    if (condicion)
    {
        Console.WriteLine($"  OK   {nombre}");
    }
    else
    {
        fallos++;
        Console.WriteLine($"  FALLO {nombre}  {detalle}");
    }
}

void ComprobarSecuencia(string nombre, IEnumerable<string> obtenido, string[] esperado)
{
    string[] actual = obtenido.ToArray();
    bool iguales = actual.SequenceEqual(esperado);
    Comprobar(nombre, iguales,
        $"esperado [{string.Join(" | ", esperado.Select(v => v.Length == 0 ? "ε" : v))}] " +
        $"obtenido [{string.Join(" | ", actual.Select(v => v.Length == 0 ? "ε" : v))}]");
}

string Visible(string palabra) => palabra.Length == 0 ? "ε" : palabra;

// Auditía que ningún texto queda recortado en el tamaño mínimo de ventana,
// que es el caso más desfavorable (por ejemplo con escalado al 150 %).
void VerificarTextos(Func<Form> fabrica, string nombre)
{
    using Form formulario = fabrica();
    var recortes = new List<string>();
    const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    void Revisar(Control padre)
    {
        foreach (Control c in padre.Controls)
        {
            if (c is Button boton)
            {
                Size necesario = TextRenderer.MeasureText(boton.Text, boton.Font,
                    new Size(int.MaxValue, int.MaxValue), flags);
                if (necesario.Width > boton.ClientSize.Width - 10 || necesario.Height > boton.ClientSize.Height)
                {
                    recortes.Add($"'{boton.Text}' necesita {necesario.Width}x{necesario.Height} y tiene {boton.ClientSize.Width - 10}x{boton.ClientSize.Height}");
                }
            }
            else if (c is Label etiqueta && !etiqueta.AutoSize)
            {
                Size necesario = TextRenderer.MeasureText(etiqueta.Text, etiqueta.Font,
                    new Size(int.MaxValue, int.MaxValue), flags);
                if (necesario.Width > etiqueta.ClientSize.Width || necesario.Height > etiqueta.ClientSize.Height)
                {
                    recortes.Add($"'{etiqueta.Text}' necesita {necesario.Width}x{necesario.Height} y tiene {etiqueta.ClientSize.Width}x{etiqueta.ClientSize.Height}");
                }
            }
            else if (c is DataGridView tabla &&
                     tabla.ColumnHeadersHeightSizeMode != DataGridViewColumnHeadersHeightSizeMode.AutoSize)
            {
                Font fuente = tabla.ColumnHeadersDefaultCellStyle.Font ?? tabla.Font;
                foreach (DataGridViewColumn columna in tabla.Columns)
                {
                    Size necesario = TextRenderer.MeasureText(columna.HeaderText, fuente,
                        new Size(int.MaxValue, int.MaxValue), flags);
                    if (necesario.Width > columna.Width - 10)
                    {
                        recortes.Add($"cabecera '{columna.HeaderText}' necesita {necesario.Width} y tiene {columna.Width - 10}");
                    }
                }
            }

            Revisar(c);
        }
    }

    Size tamanoOriginal = formulario.Size;
    formulario.StartPosition = FormStartPosition.Manual;
    formulario.Location = new Point(-3000, -3000);
    formulario.Size = formulario.MinimumSize;
    formulario.Show();
    Application.DoEvents();
    Revisar(formulario);
    formulario.Close();
    Application.DoEvents();
    formulario.Size = tamanoOriginal;

    Comprobar($"Textos sin recortes: {nombre}", recortes.Count == 0, string.Join(" | ", recortes.Take(3)));
}

// Muestra cada formulario, fuerza varios redimensionados reales (tamaño mínimo,
// maximizado y tamaño grande) y lo cierra. Si cualquier evento Layout/Resize
// lanzara una excepción, la prueba la captura y la reporta como fallo.
void ProbarRedimensionado(Form formulario, string nombre)
{
    try
    {
        formulario.StartPosition = FormStartPosition.Manual;
        formulario.Location = new Point(-3000, -3000);
        formulario.Show();
        Application.DoEvents();

        Size tamanoOriginal = formulario.Size;

        formulario.Size = new Size(
            Math.Max(formulario.MinimumSize.Width, 640),
            Math.Max(formulario.MinimumSize.Height, 480));
        Application.DoEvents();

        formulario.WindowState = FormWindowState.Maximized;
        Application.DoEvents();

        formulario.WindowState = FormWindowState.Normal;
        formulario.Size = new Size(1600, 1000);
        Application.DoEvents();

        formulario.Size = tamanoOriginal;
        Application.DoEvents();

        formulario.Close();
        Application.DoEvents();

        Comprobar($"Redimensionado sin excepciones: {nombre}", true);
    }
    catch (Exception ex)
    {
        Comprobar($"Redimensionado sin excepciones: {nombre}", false, ex.Message);
        try { formulario.Close(); Application.DoEvents(); } catch { /* ignore */ }
    }
}

// Comprueba que el lienzo del diagrama ocupa toda la celda y que el texto
// indicador inicial se dibuja con margen seguro y sin recortes.
void VerificarPlaceholderDiagrama()
{
    using var formulario = new ConstructorAutomata();
    using var mapa = new Bitmap(600, 400);

    GrafoAutomata? grafo = null;
    void Buscar(Control padre)
    {
        foreach (Control c in padre.Controls)
        {
            if (c is GrafoAutomata g) grafo = g;
            Buscar(c);
        }
    }
    Buscar(formulario);

    if (grafo is null)
    {
        Comprobar("Placeholder del diagrama", false, "No se encontró el control GrafoAutomata");
        return;
    }

    bool acoplado = grafo.Dock == DockStyle.Fill;
    Comprobar("Diagrama ocupa toda la celda (Dock=Fill)", acoplado,
        $"Dock={grafo.Dock}");

    // Instancia independiente para medir el dibujo sin que el acoplamiento
    // del formulario fuerce el tamaño del control.
    using var lienzo = new GrafoAutomata
    {
        Dock = DockStyle.None,
        Size = new Size(600, 400),
        Automata = null
    };
    lienzo.DrawToBitmap(mapa, new Rectangle(0, 0, 600, 400));

    int minX = int.MaxValue, maxX = -1;
    for (int y = 0; y < 120; y++)
    {
        for (int x = 0; x < 600; x++)
        {
            Color c = mapa.GetPixel(x, y);
            if (c.R < 210 && c.G < 210 && c.B < 210)
            {
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
            }
        }
    }

    bool margenSeguro = minX >= 10;
    bool sinRecorteDerecho = maxX < 590;
    Comprobar("Texto inicial del diagrama con margen izquierdo", margenSeguro,
        $"primera tinta en x={minX}");
    Comprobar("Texto inicial del diagrama sin recorte", sinRecorteDerecho,
        $"última tinta en x={maxX} de 600");
}

// Verifica la identidad visual académica del diagrama: pentágono irregular,
// colores, doble circunferencia final, indicador inicial saliente, curvas,
// bucles, etiquetas sin solapes y conservación de zoom/desplazamiento.
void VerificarEstiloGrafico()
{
    static bool Cerca(Color c, int r, int g, int b, int tolerancia) =>
        Math.Abs(c.R - r) <= tolerancia &&
        Math.Abs(c.G - g) <= tolerancia &&
        Math.Abs(c.B - b) <= tolerancia;

    static (Bitmap Mapa, IReadOnlyList<RectangleF> Etiquetas) Renderizar(
        Automata automata, int ancho, int alto)
    {
        var mapa = new Bitmap(ancho, alto);
        using var lienzo = new GrafoAutomata
        {
            Dock = DockStyle.None,
            Size = new Size(ancho, alto),
            Automata = automata
        };
        lienzo.DrawToBitmap(mapa, new Rectangle(0, 0, ancho, alto));
        return (mapa, lienzo.UltimasEtiquetas);
    }

    static int Contar(Bitmap mapa, RectangleF area, int r, int g, int b, int tolerancia)
    {
        int x0 = Math.Max(0, (int)area.Left);
        int y0 = Math.Max(0, (int)area.Top);
        int x1 = Math.Min(mapa.Width, (int)Math.Ceiling(area.Right));
        int y1 = Math.Min(mapa.Height, (int)Math.Ceiling(area.Bottom));
        int cantidad = 0;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                if (Cerca(mapa.GetPixel(x, y), r, g, b, tolerancia))
                {
                    cantidad++;
                }
            }
        }
        return cantidad;
    }

    Transicion T(string origen, char simbolo, string destino) => new(origen, simbolo, destino);
    string[] q5 = { "s0", "s1", "s2", "s3", "s4" };

    // Geometría y transiciones de la referencia, respetando el determinismo:
    // several símbolos que comparten destino se agrupan en una sola arista.
    var referencia = new Automata("Referencia", q5, new[] { 'a', 'b', 'c' }, "s0", new[] { "s4" }, new[]
    {
        T("s0", 'a', "s1"), T("s0", 'b', "s2"), T("s0", 'c', "s4"),
        T("s1", 'a', "s1"), T("s1", 'b', "s0"), T("s1", 'c', "s0"),
        T("s2", 'a', "s4"), T("s2", 'b', "s3"), T("s2", 'c', "s1"),
        T("s3", 'a', "s4"), T("s3", 'b', "s3"), T("s3", 'c', "s0"),
        T("s4", 'a', "s3"), T("s4", 'b', "s4"), T("s4", 'c', "s2")
    });

    var vista = Renderizar(referencia, 900, 700);
    Bitmap mapa = vista.Mapa;
    var posiciones = GrafoAutomata.ObtenerPosiciones(referencia, new Size(900, 700));

    // 1. Diseño de cinco estados.
    Comprobar("Diseño de cinco estados", posiciones.Count == 5,
        $"centros calculados: {posiciones.Count}");

    // 2. Pentágono irregular con las coordenadas relativas de la referencia.
    float anchoRelativo = (posiciones["s0"].X - posiciones["s2"].X) / 0.43f;
    float altoRelativo = (posiciones["s4"].Y - posiciones["s1"].Y) / 0.56f;
    float[,] esperado =
    {
        { 0.66f, 0.43f }, { 0.45f, 0.22f }, { 0.23f, 0.43f },
        { 0.31f, 0.78f }, { 0.58f, 0.78f }
    };
    bool coordenadas = true;
    for (int i = 0; i < q5.Length; i++)
    {
        float xr = posiciones[q5[i]].X / anchoRelativo + 0.5f;
        float yr = posiciones[q5[i]].Y / altoRelativo + 0.5f;
        coordenadas &= Math.Abs(xr - esperado[i, 0]) < 0.02f &&
                       Math.Abs(yr - esperado[i, 1]) < 0.02f;
    }
    Comprobar("Pentágono irregular con coordenadas relativas exactas", coordenadas);

    // 3. Final con doble circunferencia (dos bandas de contorno verde oscuro).
    PointF centroFinal = new(450f + posiciones["s4"].X * 1.6f,
        350f + posiciones["s4"].Y * 1.6f);
    Color rellenoFinal = mapa.GetPixel((int)centroFinal.X, (int)centroFinal.Y + 24);
    int bandas = 0;
    bool enBanda = false;
    for (int radio = 34; radio <= 56; radio++)
    {
        Color c = mapa.GetPixel((int)(centroFinal.X + radio), (int)centroFinal.Y);
        bool oscuro = c.G < 135 && c.B < 95;
        if (oscuro && !enBanda)
        {
            bandas++;
        }
        enBanda = oscuro;
    }
    Comprobar("Estados finales con doble circunferencia",
        bandas >= 2 && Cerca(rellenoFinal, 0x15, 0x94, 0x47, 18),
        $"bandas={bandas}, relleno={rellenoFinal.R},{rellenoFinal.G},{rellenoFinal.B}");

    // 4. Estados ordinarios azul pastel con contorno azul grisáceo.
    PointF centroInicial = new(450f + posiciones["s0"].X * 1.6f,
        350f + posiciones["s0"].Y * 1.6f);
    Color rellenoNormal = mapa.GetPixel((int)centroInicial.X + 18, (int)centroInicial.Y);
    Comprobar("Nodos normales azul pastel", Cerca(rellenoNormal, 0xDC, 0xEA, 0xF8, 14),
        $"{rellenoNormal.R},{rellenoNormal.G},{rellenoNormal.B}");
    Comprobar("Contorno de estados azul oscuro",
        Contar(mapa, new RectangleF(centroInicial.X + 44, centroInicial.Y - 4, 10, 8),
            0x34, 0x49, 0x5E, 20) > 0);

    // 5. Título con tilde, centrado y sin negrita excesiva (fuente Regular).
    int tituloMinX = int.MaxValue, tituloMaxX = -1;
    for (int y = 0; y < 44; y++)
    {
        for (int x = 0; x < 900; x++)
        {
            Color c = mapa.GetPixel(x, y);
            if (c.R < 120 && c.G < 120 && c.B < 120)
            {
                tituloMinX = Math.Min(tituloMinX, x);
                tituloMaxX = Math.Max(tituloMaxX, x);
            }
        }
    }
    float centroTitulo = (tituloMinX + tituloMaxX) / 2f;
    Comprobar("Título centrado y con tilde",
        GrafoAutomata.TituloDiagrama == "Autómata finito determinista" &&
        Math.Abs(centroTitulo - 450f) < 14f && tituloMaxX - tituloMinX > 120,
        $"centro={centroTitulo:0} ancho={tituloMaxX - tituloMinX}");

    // 6. Indicador inicial saliente hacia la derecha con etiqueta "Inicio".
    int azulMinX = int.MaxValue, azulMaxX = -1, azulMinY = int.MaxValue, azulMaxY = -1;
    for (int y = 0; y < 700; y++)
    {
        for (int x = 0; x < 900; x++)
        {
            if (Cerca(mapa.GetPixel(x, y), 0x15, 0x65, 0xC0, 22))
            {
                azulMinX = Math.Min(azulMinX, x);
                azulMaxX = Math.Max(azulMaxX, x);
                azulMinY = Math.Min(azulMinY, y);
                azulMaxY = Math.Max(azulMaxY, y);
            }
        }
    }
    Comprobar("Indicador Inicio sale a la derecha con etiqueta completa",
        azulMaxX > centroInicial.X + 60f &&
        azulMaxX - azulMinX > 90f &&
        azulMinY < centroInicial.Y - 8f,
        $"x={azulMinX}..{azulMaxX}, y={azulMinY}..{azulMaxY}, nodoX={centroInicial.X:0}");

    // 7. Estado inicial distinto de s0: se coloca en el flanco derecho por q0.
    var otroInicial = new Automata("Otro", new[] { "a", "b", "c", "d", "e" },
        new[] { '0' }, "c", new[] { "e" }, Array.Empty<Transicion>());
    var posOtro = GrafoAutomata.ObtenerPosiciones(otroInicial, new Size(900, 700));
    Comprobar("Estado inicial distinto de s0 se adapta",
        posOtro["c"].X == posOtro.Values.Max(p => p.X) && posOtro.Count == 5);

    // 8. Múltiples estados finales conservan relleno verde y doble circunferencia.
    var multifinal = new Automata("Multi", q5, new[] { '0' }, "s0",
        new[] { "s2", "s3" }, Array.Empty<Transicion>());
    var vistaMulti = Renderizar(multifinal, 900, 700);
    var posMulti = GrafoAutomata.ObtenerPosiciones(multifinal, new Size(900, 700));
    bool ambosVerdes = true;
    foreach (string final in new[] { "s2", "s3" })
    {
        var centro = new PointF(450f + posMulti[final].X * 1.6f,
            350f + posMulti[final].Y * 1.6f);
        ambosVerdes &= Cerca(vistaMulti.Mapa.GetPixel((int)centro.X, (int)centro.Y + 24),
            0x15, 0x94, 0x47, 18);
    }
    Comprobar("Múltiples estados finales destacados", ambosVerdes);
    vistaMulti.Mapa.Dispose();

    // 9. Bucles exteriores: arco sobre el nodo superior y bajo los inferiores.
    PointF centroSuperior = new(450f + posiciones["s1"].X * 1.6f,
        350f + posiciones["s1"].Y * 1.6f);
    Color colorTrazo = Color.FromArgb(0x26, 0x37, 0x47);
    int bucleArriba = Contar(mapa, new RectangleF(
        centroSuperior.X - 44, centroSuperior.Y - 104, 88, 46),
        colorTrazo.R, colorTrazo.G, colorTrazo.B, 16);
    bool buclesAbajo = true;
    foreach (string estado in new[] { "s3", "s4" })
    {
        var centro = new PointF(450f + posiciones[estado].X * 1.6f,
            350f + posiciones[estado].Y * 1.6f);
        buclesAbajo &= Contar(mapa, new RectangleF(
            centro.X - 44, centro.Y + 56, 88, 46),
            colorTrazo.R, colorTrazo.G, colorTrazo.B, 16) > 15;
    }
    Comprobar("Bucles superiores e inferiores", bucleArriba > 15 && buclesAbajo,
        $"superior={bucleArriba}");

    // 10. Transiciones curvas y diagonales: trazos en el interior del pentágono.
    int trazosInteriores = Contar(mapa, new RectangleF(320, 210, 330, 210),
        colorTrazo.R, colorTrazo.G, colorTrazo.B, 16);
    Comprobar("Transiciones curvas y diagonales en el interior", trazosInteriores > 60,
        $"píxeles de trazo={trazosInteriores}");

    // 11. Sentidos opuestos: dos aristas y dos etiquetas separadas.
    var bidireccional = new Automata("Bidireccional", new[] { "a", "b" }, new[] { '0', '1' },
        "a", new[] { "b" }, new[] { T("a", '0', "b"), T("b", '0', "a") });
    var vistaBi = Renderizar(bidireccional, 900, 700);
    Comprobar("Transiciones bidireccionales con arcos separados",
        vistaBi.Etiquetas.Count == 2 &&
        bidireccional.ObtenerDestino("a", '0') == "b" &&
        bidireccional.ObtenerDestino("b", '0') == "a");
    vistaBi.Mapa.Dispose();

    // 12. Varios símbolos con el mismo origen y destino: una etiqueta agrupada.
    Comprobar("Etiquetas agrupadas por origen-destino",
        referencia.ObtenerDestino("s1", 'b') == "s0" &&
        referencia.ObtenerDestino("s1", 'c') == "s0" &&
        vista.Etiquetas.Count == 14,
        $"etiquetas={vista.Etiquetas.Count}");

    // 13. Las etiquetas no se superponen entre sí.
    bool sinSolapes = true;
    for (int i = 0; i < vista.Etiquetas.Count && sinSolapes; i++)
    {
        for (int j = i + 1; j < vista.Etiquetas.Count; j++)
        {
            if (RectangleF.Inflate(vista.Etiquetas[i], 1f, 1f)
                .IntersectsWith(vista.Etiquetas[j]))
            {
                sinSolapes = false;
                break;
            }
        }
    }
    Comprobar("Prevención de superposición de etiquetas", sinSolapes);

    // 14. Redimensionamiento sin deformar la distribución relativa.
    var posPequenas = GrafoAutomata.ObtenerPosiciones(referencia, new Size(600, 420));
    var posGrandes = GrafoAutomata.ObtenerPosiciones(referencia, new Size(1100, 760));
    bool estable = posPequenas.Count == 5 && posGrandes.Count == 5;
    foreach (string estado in q5)
    {
        float xrP = posPequenas[estado].X /
            (posPequenas["s0"].X - posPequenas["s2"].X) * 0.43f + 0.5f;
        float xrG = posGrandes[estado].X /
            (posGrandes["s0"].X - posGrandes["s2"].X) * 0.43f + 0.5f;
        estable &= Math.Abs(xrP - xrG) < 0.02f;
    }
    Comprobar("Distribución estable al redimensionar", estable);

    // 15 y 16. Zoom uniforme y desplazamiento conservados.
    using var lienzoInteraccion = new GrafoAutomata
    {
        Dock = DockStyle.None,
        Size = new Size(600, 400)
    };
    MethodInfo cambiarZoom = typeof(GrafoAutomata)
        .GetMethod("CambiarEscala", BindingFlags.Instance | BindingFlags.NonPublic)!;
    float escalaInicial = lienzoInteraccion.Escala;
    cambiarZoom.Invoke(lienzoInteraccion,
        new object?[] { lienzoInteraccion, new MouseEventArgs(MouseButtons.None, 1, 0, 0, 120) });
    bool zoomSube = lienzoInteraccion.Escala > escalaInicial;
    cambiarZoom.Invoke(lienzoInteraccion,
        new object?[] { lienzoInteraccion, new MouseEventArgs(MouseButtons.None, 1, 0, 0, -120) });
    lienzoInteraccion.RestablecerVista();
    Comprobar("Zoom uniforme y Restablecer vista",
        zoomSube && Math.Abs(lienzoInteraccion.Escala - 1.6f) < 0.001f);

    int eventosVista = 0;
    lienzoInteraccion.VistaCambiada += (_, _) => eventosVista++;
    typeof(GrafoAutomata).GetMethod("IniciarArrastre", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(lienzoInteraccion, new object?[]
        {
            lienzoInteraccion, new MouseEventArgs(MouseButtons.Left, 1, 100, 100, 0)
        });
    typeof(GrafoAutomata).GetMethod("Arrastrar", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(lienzoInteraccion, new object?[]
        {
            lienzoInteraccion, new MouseEventArgs(MouseButtons.Left, 1, 130, 115, 0)
        });
    typeof(GrafoAutomata).GetMethod("TerminarArrastre", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(lienzoInteraccion, new object?[] { lienzoInteraccion, EventArgs.Empty });
    Comprobar("Desplazamiento con el ratón", eventosVista >= 1,
        $"eventos={eventosVista}");

    // 17. Actualización en caliente del autómata dibujado.
    using (var lienzoActualizacion = new GrafoAutomata
           { Dock = DockStyle.None, Size = new Size(900, 700) })
    using (var temporal = new Bitmap(900, 700))
    {
        lienzoActualizacion.Automata = referencia;
        lienzoActualizacion.DrawToBitmap(temporal, new Rectangle(0, 0, 900, 700));
        int etiquetasConAutoma = lienzoActualizacion.UltimasEtiquetas.Count;
        lienzoActualizacion.Automata = multifinal;
        lienzoActualizacion.DrawToBitmap(temporal, new Rectangle(0, 0, 900, 700));
        Comprobar("Actualización del autómata en el lienzo",
            etiquetasConAutoma == 14 && lienzoActualizacion.UltimasEtiquetas.Count == 0);
    }

    // 18. Lenguaje vacío: no aparece ningún trazo ni etiqueta de transición.
    var vacio = new Automata("Vacio", q5, Array.Empty<char>(), "s0",
        new[] { "s4" }, Array.Empty<Transicion>());
    var vistaVacia = Renderizar(vacio, 900, 700);
    int trazosVacios = Contar(vistaVacia.Mapa, new RectangleF(0, 0, 900, 700),
        colorTrazo.R, colorTrazo.G, colorTrazo.B, 8);
    Comprobar("Lenguaje vacío sin transiciones",
        vistaVacia.Etiquetas.Count == 0 && trazosVacios == 0,
        $"trazos={trazosVacios}");
    vistaVacia.Mapa.Dispose();

    // 19. El motor no inventa transiciones ausentes en δ.
    var dosEstados = new Automata("Dos", new[] { "a", "b" }, new[] { '0' },
        "a", new[] { "b" }, new[] { T("a", '0', "b") });
    var vistaDos = Renderizar(dosEstados, 900, 700);
    Comprobar("Ausencia de transiciones ficticias",
        !dosEstados.IntentarTransitar("b", '0', out _) && vistaDos.Etiquetas.Count == 1,
        $"etiquetas={vistaDos.Etiquetas.Count}");
    vistaDos.Mapa.Dispose();

    mapa.Dispose();
}

// Comprueba las correcciones geométricas de los defectos A-F: curvas locales,
// zona exclusiva de Inicio, etiquetas ancladas, bucles compactos, puntas
// tangentes en el perímetro y aprovechamiento del lienzo sin recortes.
void VerificarCorreccionesGeometricas()
{
    static PointF Bezier(GrafoAutomata.CurvaInfo c, float t)
    {
        float u = 1f - t;
        return new PointF(
            u * u * u * c.Inicio.X + 3f * u * u * t * c.Control1.X +
            3f * u * t * t * c.Control2.X + t * t * t * c.Fin.X,
            u * u * u * c.Inicio.Y + 3f * u * u * t * c.Control1.Y +
            3f * u * t * t * c.Control2.Y + t * t * t * c.Fin.Y);
    }

    static PointF BezierBucle(GrafoAutomata.BucleInfo b, float t)
    {
        float u = 1f - t;
        return new PointF(
            u * u * u * b.Inicio.X + 3f * u * u * t * b.Control1.X +
            3f * u * t * t * b.Control2.X + t * t * t * b.Fin.X,
            u * u * u * b.Inicio.Y + 3f * u * u * t * b.Control1.Y +
            3f * u * t * t * b.Control2.Y + t * t * t * b.Fin.Y);
    }

    static float Dist(PointF a, PointF b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    static float DistanciaALinea(PointF p, PointF a, PointF b)
    {
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;
        float largo = (float)Math.Sqrt(dx * dx + dy * dy);
        return largo < 1f ? Dist(p, a) : Math.Abs(dy * p.X - dx * p.Y + b.X * a.Y - b.Y * a.X) / largo;
    }

    static IEnumerable<PointF> Muestra(GrafoAutomata.CurvaInfo c)
    {
        for (int i = 1; i < 20; i++)
        {
            yield return Bezier(c, i / 20f);
        }
    }

    static IEnumerable<PointF> MuestraBucle(GrafoAutomata.BucleInfo b)
    {
        for (int i = 1; i < 20; i++)
        {
            yield return BezierBucle(b, i / 20f);
        }
    }

    Transicion T(string origen, char simbolo, string destino) => new(origen, simbolo, destino);
    Automata caso = CasosPruebaService.ObtenerCasos()[0].CrearAutomata();
    var geometria = GrafoAutomata.ObtenerGeometria(caso, new Size(900, 620));

    // A. Curvas compactas: puntos de control cerca de la cuerda y trayectoria
    // dentro de la caja de sus extremos, sin envolver otros estados.
    bool compactas = true;
    float maxDesvioControl = 0f;
    foreach (GrafoAutomata.CurvaInfo curva in geometria.Curvas)
    {
        maxDesvioControl = Math.Max(maxDesvioControl,
            DistanciaALinea(curva.Control1, curva.Inicio, curva.Fin));
        maxDesvioControl = Math.Max(maxDesvioControl,
            DistanciaALinea(curva.Control2, curva.Inicio, curva.Fin));

        float minX = Math.Min(curva.Inicio.X, curva.Fin.X) - 40f;
        float maxX = Math.Max(curva.Inicio.X, curva.Fin.X) + 40f;
        float minY = Math.Min(curva.Inicio.Y, curva.Fin.Y) - 40f;
        float maxY = Math.Max(curva.Inicio.Y, curva.Fin.Y) + 40f;
        foreach (PointF punto in Muestra(curva))
        {
            compactas &= punto.X >= minX && punto.X <= maxX &&
                         punto.Y >= minY && punto.Y <= maxY;
            foreach ((string estado, PointF centro) in geometria.Posiciones)
            {
                if (estado != curva.Origen && estado != curva.Destino)
                {
                    compactas &= Dist(punto, centro) >= GrafoAutomata.RadioEstado - 3f;
                }
            }
        }
    }
    Comprobar("A. Curvas Bézier compactas y locales",
        compactas && maxDesvioControl <= 34f,
        $"desvío máximo={maxDesvioControl:0.0}");

    // B. Zona exclusiva a la derecha de q0: ninguna curva ni bucle la invade.
    RectangleF zona = RectangleF.Inflate(geometria.ZonaInicial, 1f, 1f);
    bool zonaLibre = true;
    foreach (GrafoAutomata.CurvaInfo curva in geometria.Curvas)
    {
        foreach (PointF punto in Muestra(curva))
        {
            zonaLibre &= !zona.Contains(punto);
        }
    }
    foreach (GrafoAutomata.BucleInfo bucle in geometria.Bucles)
    {
        foreach (PointF punto in MuestraBucle(bucle))
        {
            zonaLibre &= !zona.Contains(punto);
        }
    }
    GrafoAutomata.BucleInfo bucleQ0 = geometria.Bucles.First(b => b.Estado == caso.EstadoInicial);
    foreach (PointF punto in MuestraBucle(bucleQ0))
    {
        zonaLibre &= !zona.Contains(punto);
    }
    Comprobar("B. Inicio separado de bucles y transiciones", zonaLibre,
        $"zona=({zona.X:0},{zona.Y:0},{zona.Width:0},{zona.Height:0})");

    // C. Etiquetas ancladas al punto medio de su curva, con separación pequeña.
    bool ancladas = true;
    float maximaDistancia = 0f;
    foreach (GrafoAutomata.CurvaInfo curva in geometria.Curvas)
    {
        if (curva.EtiquetaRectangulo == RectangleF.Empty)
        {
            ancladas = false;
            continue;
        }

        PointF medio = Bezier(curva, 0.5f);
        PointF centroEtiqueta = new(
            curva.EtiquetaRectangulo.X + curva.EtiquetaRectangulo.Width / 2f,
            curva.EtiquetaRectangulo.Y + curva.EtiquetaRectangulo.Height / 2f);
        float distancia = Dist(medio, centroEtiqueta);
        maximaDistancia = Math.Max(maximaDistancia, distancia);
        ancladas &= distancia >= 3f && distancia <= 40f &&
                    curva.EtiquetaRectangulo.Height <= 20f;
    }
    foreach (GrafoAutomata.BucleInfo bucle in geometria.Bucles)
    {
        if (bucle.EtiquetaRectangulo == RectangleF.Empty)
        {
            ancladas = false;
            continue;
        }

        PointF medio = BezierBucle(bucle, 0.5f);
        PointF centroEtiqueta = new(
            bucle.EtiquetaRectangulo.X + bucle.EtiquetaRectangulo.Width / 2f,
            bucle.EtiquetaRectangulo.Y + bucle.EtiquetaRectangulo.Height / 2f);
        float distancia = Dist(medio, centroEtiqueta);
        maximaDistancia = Math.Max(maximaDistancia, distancia);
        ancladas &= distancia >= 3f && distancia <= 40f;
    }
    Comprobar("C. Etiquetas ancladas al punto medio de la Bézier",
        ancladas, $"distancia máxima={maximaDistancia:0.0}");

    // D. Bucles compactos: entrada/salida sobre el perímetro y radio controlado.
    bool buclesCompactos = true;
    float maxRadio = 0f;
    foreach (GrafoAutomata.BucleInfo bucle in geometria.Bucles)
    {
        PointF centro = geometria.Posiciones[bucle.Estado];
        float radioInicio = Dist(bucle.Inicio, centro);
        float radioFin = Dist(bucle.Fin, centro);
        buclesCompactos &= Math.Abs(radioInicio - GrafoAutomata.RadioEstado) < 0.6f &&
                           Math.Abs(radioFin - GrafoAutomata.RadioEstado) < 0.6f;
        foreach (PointF punto in MuestraBucle(bucle))
        {
            float radio = Dist(punto, centro);
            maxRadio = Math.Max(maxRadio, radio);
            buclesCompactos &= radio >= GrafoAutomata.RadioEstado - 2f && radio <= 52f;
        }
    }
    Comprobar("D. Bucles compactos con extremos en el perímetro",
        buclesCompactos, $"radio máximo={maxRadio:0.0}");

    // E. Puntas de flecha: extremo en el perímetro destino y tangente entrante.
    bool puntasCorrectas = true;
    foreach (GrafoAutomata.CurvaInfo curva in geometria.Curvas)
    {
        PointF destino = geometria.Posiciones[curva.Destino];
        float radioFin = Dist(curva.Fin, destino);
        PointF direccion = new(curva.Fin.X - curva.Control2.X, curva.Fin.Y - curva.Control2.Y);
        PointF haciaCentro = new(destino.X - curva.Fin.X, destino.Y - curva.Fin.Y);
        float longitudDireccion = Dist(direccion, PointF.Empty);
        float longitudCentro = Dist(haciaCentro, PointF.Empty);
        float coseno = longitudDireccion > 0f && longitudCentro > 0f
            ? (direccion.X * haciaCentro.X + direccion.Y * haciaCentro.Y) /
              (longitudDireccion * longitudCentro)
            : -1f;
        puntasCorrectas &= Math.Abs(radioFin - (GrafoAutomata.RadioEstado + 1f)) < 0.6f &&
                           coseno > 0.55f;
    }
    foreach (GrafoAutomata.BucleInfo bucle in geometria.Bucles)
    {
        PointF centro = geometria.Posiciones[bucle.Estado];
        float radioFin = Dist(bucle.Fin, centro);
        PointF direccion = new(bucle.Fin.X - bucle.Control2.X, bucle.Fin.Y - bucle.Control2.Y);
        PointF haciaCentro = new(centro.X - bucle.Fin.X, centro.Y - bucle.Fin.Y);
        float longitudDireccion = Dist(direccion, PointF.Empty);
        float longitudCentro = Dist(haciaCentro, PointF.Empty);
        float coseno = longitudDireccion > 0f && longitudCentro > 0f
            ? (direccion.X * haciaCentro.X + direccion.Y * haciaCentro.Y) /
              (longitudDireccion * longitudCentro)
            : -1f;
        puntasCorrectas &= Math.Abs(radioFin - GrafoAutomata.RadioEstado) < 0.6f &&
                           coseno > 0.55f;
    }
    Comprobar("E. Puntas tangentes en el perímetro destino", puntasCorrectas);

    // F. Ningún trazo se recorta en el borde del lienzo.
    using (var mapa = new Bitmap(900, 620))
    using (var lienzo = new GrafoAutomata
           {
               Automata = caso,
               Dock = DockStyle.None,
               Size = new Size(900, 620)
           })
    {
        lienzo.DrawToBitmap(mapa, new Rectangle(0, 0, 900, 620));
        int borde = 0;
        for (int y = 0; y < 620; y++)
        {
            for (int x = 0; x < 900; x++)
            {
                bool perimetro = x < 6 || x > 893 || y < 6 || y > 613;
                if (!perimetro)
                {
                    continue;
                }

                Color c = mapa.GetPixel(x, y);
                if (c.R < 170 && c.G < 170 && c.B < 170)
                {
                    borde++;
                }
            }
        }
        Comprobar("F. Diagrama sin recortes en los bordes", borde == 0,
            $"píxeles oscuros en el borde={borde}");
    }

    // Sentidos opuestos separados sin arcos desproporcionados.
    var bidireccional = new Automata("Bidireccional", new[] { "a", "b" },
        new[] { '0', '1' }, "a", new[] { "b" },
        new[] { T("a", '0', "b"), T("b", '0', "a") });
    var geoBi = GrafoAutomata.ObtenerGeometria(bidireccional, new Size(900, 620));
    var ida = geoBi.Curvas.First(c => c.Origen == "a");
    var vuelta = geoBi.Curvas.First(c => c.Origen == "b");
    float separacion = Dist(Bezier(ida, 0.5f), Bezier(vuelta, 0.5f));
    Comprobar("Sentidos opuestos separados con curvatura moderada",
        geoBi.Curvas.Count == 2 && separacion >= 12f && separacion <= 70f,
        $"separación={separacion:0.0}");

    // La geometría sigue siendo compacta tras redimensionar y hacer zoom.
    bool adaptable = true;
    foreach (Size tamaño in new[] { new Size(600, 420), new Size(1100, 760) })
    {
        var geo = GrafoAutomata.ObtenerGeometria(caso, tamaño, 2f);
        foreach (GrafoAutomata.CurvaInfo curva in geo.Curvas)
        {
            adaptable &= DistanciaALinea(curva.Control1, curva.Inicio, curva.Fin) <= 34f &&
                         DistanciaALinea(curva.Control2, curva.Inicio, curva.Fin) <= 34f;
        }
    }
    Comprobar("Geometría compacta al redimensionar y ampliar", adaptable);
}

// Comprueba que el texto escrito en el cuadro de entrada del Módulo 3 se ve
// completo (el problema era una fila fija que recortaba la parte inferior).
void VerificarCajaEntradaReconocimiento()
{
    using var formulario = new ReconocimientoPalabras();
    formulario.StartPosition = FormStartPosition.Manual;
    formulario.Location = new Point(50, 50);
    formulario.ShowInTaskbar = false;
    formulario.Show();
    Application.DoEvents();

    TextBox? caja = null;
    void Buscar(Control padre)
    {
        foreach (Control c in padre.Controls)
        {
            if (c is TextBox t && !t.ReadOnly && !t.Multiline) caja = t;
            Buscar(c);
        }
    }
    Buscar(formulario);

    if (caja is null)
    {
        Comprobar("Caja de entrada del Módulo 3", false, "No se encontró el cuadro de texto");
        return;
    }

    caja.Text = "00";
    Application.DoEvents();

    using var mapa = new Bitmap(formulario.Width, formulario.Height);
    formulario.DrawToBitmap(mapa, new Rectangle(0, 0, formulario.Width, formulario.Height));

    int rx = 0, ry = 0;
    for (Control? c = caja; c is not null && c != formulario; c = c.Parent)
    {
        rx += c.Left;
        ry += c.Top;
    }

    int minY = int.MaxValue, maxY = -1;
    for (int y = 1; y < caja.Height - 1; y++)
    {
        for (int x = 2; x < 80 && x < caja.Width - 2; x++)
        {
            Color pixel = mapa.GetPixel(rx + x, ry + y);
            if (pixel.R < 100 && pixel.G < 100 && pixel.B < 100)
            {
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }
    }

    bool visible = minY >= 1 && maxY >= 0 && maxY <= caja.Height - 2;
    Comprobar("Texto '00' visible completo en el Módulo 3", visible,
        $"tinta en y={minY}..{maxY} de {caja.Height}px");

    formulario.Close();
    Application.DoEvents();
}

var buscador = new BusquedaPalabrasService();
var reconocedor = new ReconocimientoService();
var validador = new ValidacionAutomataService();
IReadOnlyList<CasoPrueba> casos = CasosPruebaService.ObtenerCasos();

Console.WriteLine("=== MÓDULO 2: búsqueda de palabras ===");

Automata a1 = casos[0].CrearAutomata();
Comprobar("Representación formal de la 5-tupla",
    a1.RepresentacionFormal.Contains("M = ({q0, q1, q2, q3, q4}, {0, 1}, δ, q0, {q4})"));
ResultadoBusqueda r1 = buscador.Buscar(a1);
ComprobarSecuencia("Autómata 1 — cinco palabras más cortas",
    r1.Palabras.Select(p => p.Palabra), new[] { "0000", "00001", "00010", "00100", "01000" });
ComprobarSecuencia("Autómata 1 — longitudes",
    r1.Palabras.Select(p => p.Longitud.ToString()), new[] { "4", "5", "5", "5", "5" });
ComprobarSecuencia("Autómata 1 — recorrido de 0000",
    reconocedor.Procesar(a1, "0000").RecorridoEstados,
    new[] { "q0", "q1", "q2", "q3", "q4" });

Automata a2 = casos[1].CrearAutomata();
ResultadoBusqueda r2 = buscador.Buscar(a2);
ComprobarSecuencia("Autómata 2 — cinco palabras más cortas",
    r2.Palabras.Select(p => p.Palabra), new[] { "abab", "aabab", "babab", "aaabab", "ababab" });
ComprobarSecuencia("Autómata 2 — recorrido de abab",
    r2.Palabras.First().Recorrido, new[] { "s0", "s1", "s2", "s3", "s4" });

Automata a3 = casos[2].CrearAutomata();
ResultadoBusqueda r3 = buscador.Buscar(a3);
ComprobarSecuencia("Autómata 3 — sólo ab y ba",
    r3.Palabras.Select(p => p.Palabra), new[] { "ab", "ba" });
Comprobar("Autómata 3 — no acepta 'aabb'",
    !reconocedor.Procesar(a3, "aabb").Aceptada);
Comprobar("Autómata 3 — no acepta ε",
    !reconocedor.Procesar(a3, "ε").Aceptada);

Automata a4 = casos[3].CrearAutomata();
ResultadoBusqueda r4 = buscador.Buscar(a4);
Comprobar("Autómata 4 — sin palabras aceptadas", r4.Palabras.Count == 0,
    $"se obtuvieron {r4.Palabras.Count} palabras");

Console.WriteLine("=== MÓDULO 3: reconocimiento ===");

void Reconocimiento(Automata automata, string palabra, bool esperado, string nombre)
{
    ResultadoReconocimiento resultado = reconocedor.Procesar(automata, palabra);
    Comprobar($"{nombre}: {Visible(palabra)} → {(esperado ? "ACEPTADA" : "RECHAZADA")}",
        resultado.EntradaValida && resultado.Aceptada == esperado,
        $"entrada válida={resultado.EntradaValida}, aceptada={resultado.Aceptada}");
}

Reconocimiento(a1, "00000", false, "Autómata 1");
Reconocimiento(a1, "0000", true, "Autómata 1");
Reconocimiento(a1, "ε", false, "Autómata 1");
Reconocimiento(a1, "00001", true, "Autómata 1");
Reconocimiento(a2, "abab", true, "Autómata 2");
Reconocimiento(a2, "aba", false, "Autómata 2");
Reconocimiento(a2, "babab", true, "Autómata 2");
Reconocimiento(a2, "bb", false, "Autómata 2");
Reconocimiento(a2, "", false, "Autómata 2");
Reconocimiento(a4, "aaa", false, "Autómata 4");

ResultadoReconocimiento invalida = reconocedor.Procesar(a2, "abc");
Comprobar("Símbolo inválido 'c' en posición 3",
    !invalida.EntradaValida && invalida.SimboloInvalido == 'c' && invalida.PosicionInvalida == 3,
    $"símbolo={invalida.SimboloInvalido}, posición={invalida.PosicionInvalida}");

ComprobarSecuencia("Recorrido de 0000 en el autómata 1",
    reconocedor.Procesar(a1, "0000").RecorridoEstados,
    new[] { "q0", "q1", "q2", "q3", "q4" });

Console.WriteLine("=== VALIDACIONES ===");

string[] cincoEstados = { "q0", "q1", "q2", "q3", "q4" };
Transicion[] transicionesUnSimbolo = cincoEstados.Select(e => new Transicion(e, '0', e)).ToArray();

ResultadoValidacion sinEstados = validador.Validar(Array.Empty<string>(), new[] { '0' }, "", Array.Empty<string>(), Array.Empty<Transicion>());
Comprobar("Q vacía detectada", !sinEstados.EsValido && sinEstados.Errores.Any(e => e.Contains("exactamente 5 estados")));

ResultadoValidacion cantidadIncorrecta = validador.Validar(
    new[] { "q0", "q1", "q2", "q3" }, new[] { '0' }, "q0", Array.Empty<string>(),
    new[] { new Transicion("q0", '0', "q0"), new Transicion("q1", '0', "q1"),
            new Transicion("q2", '0', "q2"), new Transicion("q3", '0', "q3") });
Comprobar("Q con menos de 5 nodos detectada",
    cantidadIncorrecta.Errores.Any(e => e.Contains("exactamente 5 estados")));

ResultadoValidacion sinAlfabeto = validador.Validar(cincoEstados, Array.Empty<char>(), "q0", Array.Empty<string>(), Array.Empty<Transicion>());
Comprobar("Σ vacío detectado", !sinAlfabeto.EsValido && sinAlfabeto.Errores.Any(e => e.Contains("al menos un símbolo")));

ResultadoValidacion duplicados = validador.Validar(
    new[] { "q0", "q0", "q1", "q2", "q3" }, new[] { '0', '0' }, "q0", new[] { "q0" },
    transicionesUnSimbolo);
Comprobar("Estados duplicados detectados", duplicados.Errores.Any(e => e.Contains("Estado duplicado")));
Comprobar("Símbolos duplicados detectados", duplicados.Errores.Any(e => e.Contains("Símbolo duplicado")));

ResultadoValidacion inicialIncorrecta = validador.Validar(
    cincoEstados, new[] { '0' }, "qx", Array.Empty<string>(), transicionesUnSimbolo);
Comprobar("Estado inicial fuera de Q detectado",
    inicialIncorrecta.Errores.Any(e => e.Contains("inicial") && e.Contains("no pertenece")));

ResultadoValidacion finalIncorrecto = validador.Validar(
    cincoEstados, new[] { '0' }, "q0", new[] { "qx" }, transicionesUnSimbolo);
Comprobar("Estado final fuera de Q detectado",
    finalIncorrecto.Errores.Any(e => e.Contains("final") && e.Contains("no pertenece")));

ResultadoValidacion incompleta = validador.Validar(
    cincoEstados, new[] { '0', '1' }, "q0", new[] { "q0" },
    new[] { new Transicion("q0", '0', "q0") });
Comprobar("Transiciones incompletas detectadas",
    incompleta.Errores.Count(e => e.Contains("Falta la transición")) == 9);

ResultadoValidacion duplicada = validador.Validar(
    cincoEstados, new[] { '0' }, "q0", new[] { "q0" },
    new[] { new Transicion("q0", '0', "q0"), new Transicion("q0", '0', "q0"),
            new Transicion("q1", '0', "q1"), new Transicion("q2", '0', "q2"),
            new Transicion("q3", '0', "q3"), new Transicion("q4", '0', "q4") });
Comprobar("Transición duplicada detectada", duplicada.Errores.Any(e => e.Contains("duplicada")));

ResultadoValidacion destinoInexistente = validador.Validar(
    cincoEstados, new[] { '0' }, "q0", new[] { "q0" },
    new[] { new Transicion("q0", '0', "qx"), new Transicion("q1", '0', "q1"),
            new Transicion("q2", '0', "q2"), new Transicion("q3", '0', "q3"),
            new Transicion("q4", '0', "q4") });
Comprobar("Destino inexistente detectado",
    destinoInexistente.Errores.Any(e => e.Contains("inexistente")));

Console.WriteLine("=== SERVICIO COMPARTIDO ===");

AutomataService servicio = AutomataService.Instancia;
servicio.Establecer(a1);
Comprobar("El servicio conserva el autómata actual", ReferenceEquals(servicio.Actual, a1));
servicio.Establecer(a2);
Comprobar("Un nuevo autómata reemplaza al anterior", ReferenceEquals(servicio.Actual, a2));
servicio.Establecer(a3);
Comprobar("Los tres módulos comparten la misma definición",
    ReferenceEquals(servicio.Actual, a3) &&
    buscador.Buscar(servicio.Actual!).Palabras.Count == 2);
servicio.Limpiar();
Comprobar("Limpiar deja el servicio sin autómata", !servicio.HayAutomata && servicio.Actual is null);

Console.WriteLine("=== INTERFAZ (pruebas de humo) ===");

servicio.Establecer(a1);
try
{
    using var menu = new MenuPrincipal();
    Comprobar("Menú principal construido", menu.Text.Contains("Simulador"));
    using var constructor = new ConstructorAutomata();
    Comprobar("Constructor de autómatas construido", constructor.Text.Contains("Módulo 1"));
    constructor.CargarCaso(casos[1]);
    Comprobar("Cargar caso desde el constructor publica el AFD",
        servicio.Actual is not null && servicio.Actual?.EstadoInicial == "s0" && servicio.Actual.Estados.Count == 5);
    constructor.CargarCaso(casos[3]);
    Comprobar("Un nuevo caso reemplaza al autómata anterior",
        servicio.Actual is not null && servicio.Actual?.EstadosFinales.SequenceEqual(new[] { "q1", "q2" }) == true);
    using var busqueda = new BusquedaPalabras();
    Comprobar("Formulario de búsqueda construido", busqueda.Text.Contains("Módulo 2"));
    using var reconocimiento = new ReconocimientoPalabras();
    Comprobar("Formulario de reconocimiento construido", reconocimiento.Text.Contains("Módulo 3"));

    using var mapa = new Bitmap(500, 360);
    using var grafo = new GrafoAutomata { Automata = a2, Size = new Size(500, 360) };
    grafo.DrawToBitmap(mapa, new Rectangle(0, 0, 500, 360));
    Comprobar("Diagrama GDI+ dibujado sin excepciones", mapa.Width == 500 && mapa.Height == 360);

    Console.WriteLine("=== IDENTIDAD VISUAL DEL DIAGRAMA ===");
    VerificarEstiloGrafico();

    Console.WriteLine("=== CORRECCIONES GEOMÉTRICAS (A-F) ===");
    VerificarCorreccionesGeometricas();

    Console.WriteLine("=== AUDITORÍA DE TEXTOS ===");
    servicio.Establecer(a1);
    VerificarPlaceholderDiagrama();
    VerificarCajaEntradaReconocimiento();
    VerificarTextos(() => new MenuPrincipal(), "Menú principal");
    VerificarTextos(() => new ConstructorAutomata(), "Constructor de autómatas");
    VerificarTextos(() => new BusquedaPalabras(), "Búsqueda de palabras");
    VerificarTextos(() => new ReconocimientoPalabras(), "Reconocimiento de palabras");

    Console.WriteLine("=== REDIMENSIONADO REAL DE INTERFAZ ===");
    servicio.Establecer(a1);
    ProbarRedimensionado(menu, "Menú principal");
    ProbarRedimensionado(constructor, "Constructor de autómatas");
    ProbarRedimensionado(busqueda, "Búsqueda de palabras");
    ProbarRedimensionado(reconocimiento, "Reconocimiento de palabras");
}
catch (Exception ex)
{
    Comprobar("Pruebas de humo de interfaz", false, ex.Message);
}

Console.WriteLine();
Console.WriteLine($"Comprobaciones: {comprobaciones}. Fallos: {fallos}.");
Environment.ExitCode = fallos == 0 ? 0 : 1;
