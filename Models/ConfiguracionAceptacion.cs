using System;
using System.Collections.Generic;
using System.Linq;

namespace SimuladorAutomatas.Models;

/// <summary>
/// Datos de aceptación q0 y F. Se mantienen fuera de la terna M = (Q, Σ, δ)
/// porque determinan el lenguaje reconocido, no la estructura de transiciones.
/// </summary>
public sealed class ConfiguracionAceptacion
{
    private readonly HashSet<string> _finales;

    /// <summary>Estado inicial q0.</summary>
    public string EstadoInicial { get; }

    /// <summary>Conjunto F de estados finales, en el orden introducido.</summary>
    public IReadOnlyList<string> EstadosFinales { get; }

    public ConfiguracionAceptacion(string estadoInicial, IEnumerable<string> estadosFinales)
    {
        EstadoInicial = (estadoInicial ?? string.Empty).Trim();
        EstadosFinales = estadosFinales.Select(e => e.Trim()).ToArray();
        _finales = new HashSet<string>(EstadosFinales, StringComparer.Ordinal);
    }

    public bool EsFinal(string estado) => _finales.Contains(estado);

    public bool AceptaPalabraVacia => EsFinal(EstadoInicial);

    public override string ToString() =>
        $"q0 = {EstadoInicial}, F = {{{string.Join(", ", EstadosFinales)}}}";
}
