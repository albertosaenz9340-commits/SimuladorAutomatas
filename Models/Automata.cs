using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimuladorAutomatas.Models;

/// <summary>
/// Autómata finito determinista M = (Q, Σ, δ, q0, F).
/// Las colecciones se copian al construir la instancia para garantizar que
/// los tres módulos trabajen siempre sobre la misma definición inmutable.
/// </summary>
public sealed class Automata
{
    private readonly Dictionary<(string Estado, char Simbolo), string> _tablaTransiciones;
    private readonly HashSet<string> _conjuntoFinales;
    private readonly HashSet<char> _conjuntoAlfabeto;
    private readonly Dictionary<string, int> _indiceEstados;

    public string Nombre { get; }

    /// <summary>Conjunto Q de estados, en el orden introducido por el usuario.</summary>
    public IReadOnlyList<string> Estados { get; }

    /// <summary>Alfabeto Σ, en el orden introducido por el usuario.</summary>
    public IReadOnlyList<char> Alfabeto { get; }

    /// <summary>Estado inicial q0.</summary>
    public string EstadoInicial { get; }

    /// <summary>Conjunto F de estados finales.</summary>
    public IReadOnlyList<string> EstadosFinales { get; }

    /// <summary>Función de transición δ completa.</summary>
    public IReadOnlyList<Transicion> Transiciones { get; }

    public Automata(
        string nombre,
        IEnumerable<string> estados,
        IEnumerable<char> alfabeto,
        string estadoInicial,
        IEnumerable<string> estadosFinales,
        IEnumerable<Transicion> transiciones)
    {
        Nombre = string.IsNullOrWhiteSpace(nombre) ? "Autómata" : nombre.Trim();
        Estados = estados.Select(e => e.Trim()).ToArray();
        Alfabeto = alfabeto.ToArray();
        EstadoInicial = estadoInicial.Trim();
        EstadosFinales = estadosFinales.Select(e => e.Trim()).ToArray();
        Transiciones = transiciones
            .Select(t => new Transicion(t.Origen, t.Simbolo, t.Destino))
            .ToArray();

        _conjuntoFinales = new HashSet<string>(EstadosFinales, StringComparer.Ordinal);
        _conjuntoAlfabeto = new HashSet<char>(Alfabeto);
        _indiceEstados = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < Estados.Count; i++)
        {
            _indiceEstados[Estados[i]] = i;
        }

        _tablaTransiciones = new Dictionary<(string, char), string>();
        foreach (Transicion transicion in Transiciones)
        {
            _tablaTransiciones[(transicion.Origen, transicion.Simbolo)] = transicion.Destino;
        }
    }

    public int CantidadEstados => Estados.Count;

    public int CantidadTransiciones => Transiciones.Count;

    public bool EsFinal(string estado) => _conjuntoFinales.Contains(estado);

    public bool EsEstadoConocido(string estado) => _indiceEstados.ContainsKey(estado);

    public bool PerteneceAlAlfabeto(char simbolo) => _conjuntoAlfabeto.Contains(simbolo);

    public bool AceptaPalabraVacia => EsFinal(EstadoInicial);

    public int ObtenerIndice(string estado) => _indiceEstados[estado];

    /// <summary>
    /// Aplica δ a un par estado-símbolo. Devuelve false sólo si la transición
    /// no existe (lo cual no ocurre en un AFD validado).
    /// </summary>
    public bool IntentarTransitar(string estado, char simbolo, out string destino)
    {
        return _tablaTransiciones.TryGetValue((estado, simbolo), out destino!);
    }

    public string? ObtenerDestino(string estado, char simbolo) =>
        _tablaTransiciones.TryGetValue((estado, simbolo), out string? destino) ? destino : null;

    /// <summary>Representación completa de la quíntupla y de la función δ.</summary>
    public string RepresentacionFormal
    {
        get
        {
            var texto = new StringBuilder();
            texto.AppendLine($"M = ({{{string.Join(", ", Estados)}}}, {{{string.Join(", ", Alfabeto)}}}, δ, {EstadoInicial}, {{{string.Join(", ", EstadosFinales)}}})");
            texto.AppendLine();
            texto.AppendLine($"Q  = {{{string.Join(", ", Estados)}}}");
            texto.AppendLine($"Σ  = {{{string.Join(", ", Alfabeto)}}}");
            texto.AppendLine($"q0 = {EstadoInicial}");
            texto.AppendLine($"F  = {{{string.Join(", ", EstadosFinales)}}}");
            texto.AppendLine();
            texto.AppendLine("δ: Q × Σ → Q");
            foreach (Transicion transicion in Transiciones)
            {
                texto.AppendLine($"  {transicion}");
            }

            texto.AppendLine();
            texto.AppendLine($"|Q| = {Estados.Count}   |Σ| = {Alfabeto.Count}   |Q| × |Σ| = {Estados.Count * Alfabeto.Count} transiciones");
            texto.AppendLine($"ε {(AceptaPalabraVacia ? "∈" : "∉")} L(M)  (ε es aceptada si y sólo si q0 ∈ F)");
            return texto.ToString();
        }
    }

    public override string ToString() => $"{Nombre} — M = (Q, Σ, δ, {EstadoInicial}, F)";
}
