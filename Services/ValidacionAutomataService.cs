using System;
using System.Collections.Generic;
using System.Linq;
using SimuladorAutomatas.Models;

namespace SimuladorAutomatas.Services;

/// <summary>Resultado de validar una definición de AFD.</summary>
public sealed class ResultadoValidacion
{
    public IReadOnlyList<string> Errores { get; }

    public bool EsValido => Errores.Count == 0;

    public ResultadoValidacion(IEnumerable<string> errores)
    {
        Errores = errores.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static ResultadoValidacion Correcto() => new(Array.Empty<string>());
}

/// <summary>
/// Comprueba que la terna M = (Q, Σ, δ) sea un AFD completo con exactamente
/// cinco estados en Q, y que la configuración de aceptación (q0, F) referencie
/// estados de Q.
/// </summary>
public sealed class ValidacionAutomataService
{
    /// <summary>Cantidad de estados (nodos) exigida en Q.</summary>
    public const int CantidadEstadosRequerida = 5;

    /// <summary>Valida únicamente la terna M = (Q, Σ, δ).</summary>
    public ResultadoValidacion ValidarDefinicion(
        IReadOnlyList<string> estados,
        IReadOnlyList<char> alfabeto,
        IReadOnlyList<Transicion> transiciones)
    {
        var errores = new List<string>();

        if (estados.Count != CantidadEstadosRequerida)
        {
            errores.Add($"Debe definir exactamente {CantidadEstadosRequerida} estados (Q debe tener 5 nodos).");
        }

        if (alfabeto.Count == 0)
        {
            errores.Add("Debe definir al menos un símbolo (Σ no puede estar vacío).");
        }

        var conjuntoEstados = new HashSet<string>(StringComparer.Ordinal);
        foreach (string estado in estados)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                errores.Add("Existe un estado con nombre vacío.");
                continue;
            }

            string nombre = estado.Trim();
            if (!conjuntoEstados.Add(nombre))
            {
                errores.Add($"Estado duplicado: '{nombre}'.");
            }
        }

        var conjuntoSimbolos = new HashSet<char>();
        foreach (char simbolo in alfabeto)
        {
            if (!conjuntoSimbolos.Add(simbolo))
            {
                errores.Add($"Símbolo duplicado: '{simbolo}'.");
            }
        }

        var paresDefinidos = new HashSet<(string Estado, char Simbolo)>();
        foreach (Transicion transicion in transiciones)
        {
            if (!conjuntoEstados.Contains(transicion.Origen))
            {
                errores.Add($"La transición δ({transicion.Origen}, {transicion.Simbolo}) tiene un origen inexistente.");
            }

            if (!conjuntoSimbolos.Contains(transicion.Simbolo))
            {
                errores.Add($"La transición δ({transicion.Origen}, {transicion.Simbolo}) usa un símbolo que no está en Σ.");
            }

            if (!conjuntoEstados.Contains(transicion.Destino))
            {
                errores.Add($"La transición δ({transicion.Origen}, {transicion.Simbolo}) conduce al estado inexistente '{transicion.Destino}'.");
            }

            if (!paresDefinidos.Add((transicion.Origen, transicion.Simbolo)))
            {
                errores.Add($"Transición duplicada: δ({transicion.Origen}, {transicion.Simbolo}).");
            }
        }

        foreach (string estado in conjuntoEstados)
        {
            foreach (char simbolo in conjuntoSimbolos)
            {
                if (!paresDefinidos.Contains((estado, simbolo)))
                {
                    errores.Add($"Falta la transición δ({estado}, {simbolo}). La función δ debe ser completa.");
                }
            }
        }

        return new ResultadoValidacion(errores);
    }

    /// <summary>Valida la terna y, además, el estado inicial y los finales.</summary>
    public ResultadoValidacion Validar(
        IReadOnlyList<string> estados,
        IReadOnlyList<char> alfabeto,
        string estadoInicial,
        IReadOnlyList<string> estadosFinales,
        IReadOnlyList<Transicion> transiciones)
    {
        var errores = new List<string>(ValidarDefinicion(estados, alfabeto, transiciones).Errores);

        var conjuntoEstados = new HashSet<string>(
            estados.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()),
            StringComparer.Ordinal);

        string inicial = estadoInicial?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(inicial))
        {
            errores.Add("Debe indicar el estado inicial q0.");
        }
        else if (conjuntoEstados.Count > 0 && !conjuntoEstados.Contains(inicial))
        {
            errores.Add($"El estado inicial '{inicial}' no pertenece a Q.");
        }

        foreach (string final in estadosFinales)
        {
            string nombre = final?.Trim() ?? string.Empty;
            if (!conjuntoEstados.Contains(nombre))
            {
                errores.Add($"El estado final '{nombre}' no pertenece a Q.");
            }
        }

        return new ResultadoValidacion(errores);
    }
}
