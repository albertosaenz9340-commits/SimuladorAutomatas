using System;
using System.Collections.Generic;
using System.Linq;
using SimuladorAutomatas.Models;

namespace SimuladorAutomatas.Services;

/// <summary>Definición de un autómata de prueba listo para cargar desde la interfaz.</summary>
public sealed class CasoPrueba
{
    public string Nombre { get; }
    public string Descripcion { get; }
    public IReadOnlyList<string> Estados { get; }
    public IReadOnlyList<char> Alfabeto { get; }
    public string EstadoInicial { get; }
    public IReadOnlyList<string> EstadosFinales { get; }
    public IReadOnlyList<Transicion> Transiciones { get; }

    public CasoPrueba(
        string nombre,
        string descripcion,
        string[] estados,
        char[] alfabeto,
        string estadoInicial,
        string[] estadosFinales,
        Transicion[] transiciones)
    {
        Nombre = nombre;
        Descripcion = descripcion;
        Estados = estados;
        Alfabeto = alfabeto;
        EstadoInicial = estadoInicial;
        EstadosFinales = estadosFinales;
        Transiciones = transiciones;
    }

    /// <summary>Construye la terna M = (Q, Σ, δ) comprobando antes que sea válida.</summary>
    public Automata CrearAutomata()
    {
        Validar();
        return new Automata(Nombre, Estados, Alfabeto, Transiciones);
    }

    /// <summary>Construye la configuración de aceptación (q0, F) del caso.</summary>
    public ConfiguracionAceptacion CrearConfiguracion() => new(EstadoInicial, EstadosFinales);

    /// <summary>Construye la terna y su configuración de aceptación.</summary>
    public (Automata Automata, ConfiguracionAceptacion Configuracion) CrearDefinicion() =>
        (CrearAutomata(), CrearConfiguracion());

    private void Validar()
    {
        ResultadoValidacion validacion = new ValidacionAutomataService().Validar(
            Estados, Alfabeto, EstadoInicial, EstadosFinales, Transiciones);

        if (!validacion.EsValido)
        {
            throw new InvalidOperationException(
                $"El caso de prueba '{Nombre}' no es válido: {string.Join(" ", validacion.Errores)}");
        }
    }
}

/// <summary>Cuatro autómatas de prueba, todos con exactamente cinco estados.</summary>
public static class CasosPruebaService
{
    private static Transicion T(string origen, char simbolo, string destino) => new(origen, simbolo, destino);

    public static IReadOnlyList<CasoPrueba> ObtenerCasos()
    {
        return new[]
        {
            new CasoPrueba(
                "Cantidad de ceros múltiplo de 5",
                "Acepta las palabras con un número de símbolos 0 múltiplo de 5.",
                new[] { "q0", "q1", "q2", "q3", "q4" },
                new[] { '0', '1' },
                "q0",
                new[] { "q0" },
                new[]
                {
                    T("q0", '0', "q1"), T("q0", '1', "q0"),
                    T("q1", '0', "q2"), T("q1", '1', "q1"),
                    T("q2", '0', "q3"), T("q2", '1', "q2"),
                    T("q3", '0', "q4"), T("q3", '1', "q3"),
                    T("q4", '0', "q0"), T("q4", '1', "q4")
                }),

            new CasoPrueba(
                "Palabras que terminan en abab",
                "Acepta exactamente las cadenas sobre {a, b} cuyo sufijo es abab.",
                new[] { "s0", "s1", "s2", "s3", "s4" },
                new[] { 'a', 'b' },
                "s0",
                new[] { "s4" },
                new[]
                {
                    T("s0", 'a', "s1"), T("s0", 'b', "s0"),
                    T("s1", 'a', "s1"), T("s1", 'b', "s2"),
                    T("s2", 'a', "s3"), T("s2", 'b', "s0"),
                    T("s3", 'a', "s1"), T("s3", 'b', "s4"),
                    T("s4", 'a', "s3"), T("s4", 'b', "s0")
                }),

            new CasoPrueba(
                "Únicamente ab y ba",
                "Acepta sólo las palabras ab y ba; cualquier otra va al estado sumidero m.",
                new[] { "p0", "p1", "p2", "p3", "m" },
                new[] { 'a', 'b' },
                "p0",
                new[] { "p3" },
                new[]
                {
                    T("p0", 'a', "p1"), T("p0", 'b', "p2"),
                    T("p1", 'a', "m"),  T("p1", 'b', "p3"),
                    T("p2", 'a', "p3"), T("p2", 'b', "m"),
                    T("p3", 'a', "m"),  T("p3", 'b', "m"),
                    T("m", 'a', "m"),   T("m", 'b', "m")
                }),

            new CasoPrueba(
                "Lenguaje vacío (final inalcanzable)",
                "Los estados finales q1 y q2 no son alcanzables desde q0, por lo que L(M) = ∅.",
                new[] { "q0", "q1", "q2", "q3", "q4" },
                new[] { 'a' },
                "q0",
                new[] { "q1", "q2" },
                new[]
                {
                    T("q0", 'a', "q0"),
                    T("q1", 'a', "q1"),
                    T("q2", 'a', "q2"),
                    T("q3", 'a', "q3"),
                    T("q4", 'a', "q4")
                })
        };
    }
}
