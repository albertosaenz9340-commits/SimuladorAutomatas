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

    /// <summary>Construye el AFD comprobando antes que su definición sea válida.</summary>
    public Automata CrearAutomata()
    {
        var validacion = new ValidacionAutomataService().Validar(
            Estados, Alfabeto, EstadoInicial, EstadosFinales, Transiciones);

        if (!validacion.EsValido)
        {
            throw new InvalidOperationException(
                $"El caso de prueba '{Nombre}' no es válido: {string.Join(" ", validacion.Errores)}");
        }

        return new Automata(Nombre, Estados, Alfabeto, EstadoInicial, EstadosFinales, Transiciones);
    }
}

/// <summary>Cuatro autómatas de prueba solicitados en la especificación.</summary>
public static class CasosPruebaService
{
    private static Transicion T(string origen, char simbolo, string destino) => new(origen, simbolo, destino);

    public static IReadOnlyList<CasoPrueba> ObtenerCasos()
    {
        return new[]
        {
            new CasoPrueba(
                "Cantidad par de ceros",
                "Acepta las palabras con un número par de símbolos 0.",
                new[] { "q0", "q1" },
                new[] { '0', '1' },
                "q0",
                new[] { "q0" },
                new[]
                {
                    T("q0", '0', "q1"), T("q0", '1', "q0"),
                    T("q1", '0', "q0"), T("q1", '1', "q1")
                }),

            new CasoPrueba(
                "Palabras que terminan en ab",
                "Acepta exactamente las cadenas sobre {a, b} cuyo sufijo es ab.",
                new[] { "s0", "s1", "s2" },
                new[] { 'a', 'b' },
                "s0",
                new[] { "s2" },
                new[]
                {
                    T("s0", 'a', "s1"), T("s0", 'b', "s0"),
                    T("s1", 'a', "s1"), T("s1", 'b', "s2"),
                    T("s2", 'a', "s1"), T("s2", 'b', "s0")
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
                "El estado final q1 no es alcanzable desde q0, por lo que L(M) = ∅.",
                new[] { "q0", "q1" },
                new[] { 'a' },
                "q0",
                new[] { "q1" },
                new[]
                {
                    T("q0", 'a', "q0"),
                    T("q1", 'a', "q1")
                })
        };
    }
}
