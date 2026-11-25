using System.Drawing;
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
ConfiguracionAceptacion c1 = casos[0].CrearConfiguracion();
ConfiguracionAceptacion c2 = casos[1].CrearConfiguracion();
ConfiguracionAceptacion c3 = casos[2].CrearConfiguracion();
ConfiguracionAceptacion c4 = casos[3].CrearConfiguracion();

Console.WriteLine("=== MÓDULO 2: búsqueda de palabras ===");

Automata a1 = casos[0].CrearAutomata();
ResultadoBusqueda r1 = buscador.Buscar(a1, c1);
ComprobarSecuencia("Autómata 1 — cinco palabras más cortas",
    r1.Palabras.Select(p => p.Palabra), new[] { "", "1", "11", "111", "1111" });
ComprobarSecuencia("Autómata 1 — longitudes",
    r1.Palabras.Select(p => p.Longitud.ToString()), new[] { "0", "1", "2", "3", "4" });
ComprobarSecuencia("Autómata 1 — recorrido de 00000",
    reconocedor.Procesar(a1, c1, "00000").RecorridoEstados,
    new[] { "q0", "q1", "q2", "q3", "q4", "q0" });

Automata a2 = casos[1].CrearAutomata();
ResultadoBusqueda r2 = buscador.Buscar(a2, c2);
ComprobarSecuencia("Autómata 2 — cinco palabras más cortas",
    r2.Palabras.Select(p => p.Palabra), new[] { "abab", "aabab", "babab", "aaabab", "ababab" });
ComprobarSecuencia("Autómata 2 — recorrido de abab",
    r2.Palabras.First().Recorrido, new[] { "s0", "s1", "s2", "s3", "s4" });

Automata a3 = casos[2].CrearAutomata();
ResultadoBusqueda r3 = buscador.Buscar(a3, c3);
ComprobarSecuencia("Autómata 3 — sólo ab y ba",
    r3.Palabras.Select(p => p.Palabra), new[] { "ab", "ba" });
Comprobar("Autómata 3 — no acepta 'aabb'",
    !reconocedor.Procesar(a3, c3, "aabb").Aceptada);
Comprobar("Autómata 3 — no acepta ε",
    !reconocedor.Procesar(a3, c3, "ε").Aceptada);

Automata a4 = casos[3].CrearAutomata();
ResultadoBusqueda r4 = buscador.Buscar(a4, c4);
Comprobar("Autómata 4 — sin palabras aceptadas", r4.Palabras.Count == 0,
    $"se obtuvieron {r4.Palabras.Count} palabras");

Console.WriteLine("=== MÓDULO 3: reconocimiento ===");

void Reconocimiento(Automata automata, ConfiguracionAceptacion configuracion, string palabra, bool esperado, string nombre)
{
    ResultadoReconocimiento resultado = reconocedor.Procesar(automata, configuracion, palabra);
    Comprobar($"{nombre}: {Visible(palabra)} → {(esperado ? "ACEPTADA" : "RECHAZADA")}",
        resultado.EntradaValida && resultado.Aceptada == esperado,
        $"entrada válida={resultado.EntradaValida}, aceptada={resultado.Aceptada}");
}

Reconocimiento(a1, c1, "00000", true, "Autómata 1");
Reconocimiento(a1, c1, "0000", false, "Autómata 1");
Reconocimiento(a1, c1, "ε", true, "Autómata 1");
Reconocimiento(a1, c1, "1111", true, "Autómata 1");
Reconocimiento(a2, c2, "abab", true, "Autómata 2");
Reconocimiento(a2, c2, "aba", false, "Autómata 2");
Reconocimiento(a2, c2, "babab", true, "Autómata 2");
Reconocimiento(a2, c2, "bb", false, "Autómata 2");
Reconocimiento(a2, c2, "", false, "Autómata 2");
Reconocimiento(a4, c4, "aaa", false, "Autómata 4");

ResultadoReconocimiento invalida = reconocedor.Procesar(a2, c2, "abc");
Comprobar("Símbolo inválido 'c' en posición 3",
    !invalida.EntradaValida && invalida.SimboloInvalido == 'c' && invalida.PosicionInvalida == 3,
    $"símbolo={invalida.SimboloInvalido}, posición={invalida.PosicionInvalida}");

ComprobarSecuencia("Recorrido de 00000 en el autómata 1",
    reconocedor.Procesar(a1, c1, "00000").RecorridoEstados,
    new[] { "q0", "q1", "q2", "q3", "q4", "q0" });

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
servicio.Establecer(a1, c1);
Comprobar("El servicio conserva la terna actual", ReferenceEquals(servicio.Actual, a1));
Comprobar("El servicio conserva la configuración de aceptación", ReferenceEquals(servicio.ConfiguracionActual, c1));
servicio.Establecer(a2, c2);
Comprobar("Una nueva terna reemplaza a la anterior", ReferenceEquals(servicio.Actual, a2));
servicio.Establecer(a3, c3);
Comprobar("Los tres módulos comparten la misma definición",
    ReferenceEquals(servicio.Actual, a3) &&
    buscador.Buscar(servicio.Actual!, servicio.ConfiguracionActual!).Palabras.Count == 2);
servicio.Limpiar();
Comprobar("Limpiar deja el servicio sin autómata", !servicio.HayAutomata && servicio.ConfiguracionActual is null);

Console.WriteLine("=== INTERFAZ (pruebas de humo) ===");

servicio.Establecer(a1, c1);
try
{
    using var menu = new MenuPrincipal();
    Comprobar("Menú principal construido", menu.Text.Contains("Simulador"));
    using var constructor = new ConstructorAutomata();
    Comprobar("Constructor de autómatas construido", constructor.Text.Contains("Módulo 1"));
    constructor.CargarCaso(casos[1]);
    Comprobar("Cargar caso desde el constructor publica el AFD",
        servicio.Actual is not null && servicio.ConfiguracionActual?.EstadoInicial == "s0" && servicio.Actual.Estados.Count == 5);
    constructor.CargarCaso(casos[3]);
    Comprobar("Un nuevo caso reemplaza al autómata anterior",
        servicio.Actual is not null && servicio.ConfiguracionActual?.EstadosFinales.SequenceEqual(new[] { "q1", "q2" }) == true);
    using var busqueda = new BusquedaPalabras();
    Comprobar("Formulario de búsqueda construido", busqueda.Text.Contains("Módulo 2"));
    using var reconocimiento = new ReconocimientoPalabras();
    Comprobar("Formulario de reconocimiento construido", reconocimiento.Text.Contains("Módulo 3"));

    using var mapa = new Bitmap(500, 360);
    using var grafo = new GrafoAutomata { Automata = a2, Configuracion = c2, Size = new Size(500, 360) };
    grafo.DrawToBitmap(mapa, new Rectangle(0, 0, 500, 360));
    Comprobar("Diagrama GDI+ dibujado sin excepciones", mapa.Width == 500 && mapa.Height == 360);

    Console.WriteLine("=== AUDITORÍA DE TEXTOS ===");
    servicio.Establecer(a1, c1);
    VerificarPlaceholderDiagrama();
    VerificarCajaEntradaReconocimiento();
    VerificarTextos(() => new MenuPrincipal(), "Menú principal");
    VerificarTextos(() => new ConstructorAutomata(), "Constructor de autómatas");
    VerificarTextos(() => new BusquedaPalabras(), "Búsqueda de palabras");
    VerificarTextos(() => new ReconocimientoPalabras(), "Reconocimiento de palabras");

    Console.WriteLine("=== REDIMENSIONADO REAL DE INTERFAZ ===");
    servicio.Establecer(a1, c1);
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
