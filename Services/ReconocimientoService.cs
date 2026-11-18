using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimuladorAutomatas.Models;

namespace SimuladorAutomatas.Services;

/// <summary>Resultado detallado del procesamiento de una palabra.</summary>
public sealed class ResultadoReconocimiento
{
    public string Palabra { get; }
    public bool EntradaValida { get; }
    public bool Aceptada { get; }
    public char? SimboloInvalido { get; }
    public int? PosicionInvalida { get; }
    public IReadOnlyList<char> SimbolosProcesados { get; }
    public IReadOnlyList<string> RecorridoEstados { get; }
    public string? EstadoFinal { get; }
    public string Mensaje { get; }

    public ResultadoReconocimiento(
        string palabra,
        bool entradaValida,
        bool aceptada,
        char? simboloInvalido,
        int? posicionInvalida,
        IReadOnlyList<char> simbolosProcesados,
        IReadOnlyList<string> recorridoEstados,
        string? estadoFinal,
        string mensaje)
    {
        Palabra = palabra;
        EntradaValida = entradaValida;
        Aceptada = aceptada;
        SimboloInvalido = simboloInvalido;
        PosicionInvalida = posicionInvalida;
        SimbolosProcesados = simbolosProcesados;
        RecorridoEstados = recorridoEstados;
        EstadoFinal = estadoFinal;
        Mensaje = mensaje;
    }
}

/// <summary>
/// Servicio compartido de reconocimiento. Valida los símbolos, aplica δ en orden
/// y registra el recorrido completo de estados. No duplica su lógica en los formularios.
/// </summary>
public sealed class ReconocimientoService
{
    public ResultadoReconocimiento Procesar(Automata automata, string? entrada)
    {
        ArgumentNullException.ThrowIfNull(automata);

        string palabra = Normalizar(entrada);
        var simbolos = new List<char>();
        var recorrido = new List<string> { automata.EstadoInicial };

        for (int i = 0; i < palabra.Length; i++)
        {
            char simbolo = palabra[i];
            if (!automata.PerteneceAlAlfabeto(simbolo))
            {
                string mensaje = $"Símbolo inválido '{simbolo}' en la posición {i + 1}. " +
                                 $"El alfabeto es {{{string.Join(", ", automata.Alfabeto)}}}.";
                return new ResultadoReconocimiento(
                    palabra, false, false, simbolo, i + 1, simbolos, recorrido, null, mensaje);
            }

            simbolos.Add(simbolo);
            if (!automata.IntentarTransitar(recorrido[^1], simbolo, out string destino))
            {
                string mensajeError = $"No existe la transición δ({recorrido[^1]}, {simbolo}).";
                return new ResultadoReconocimiento(
                    palabra, false, false, simbolo, i + 1, simbolos, recorrido, null, mensajeError);
            }

            recorrido.Add(destino);
        }

        string estadoFinal = recorrido[^1];
        bool aceptada = automata.EsFinal(estadoFinal);
        string mensajeFinal = palabra.Length == 0
            ? $"Palabra vacía ε: {(aceptada ? "ACEPTADA" : "RECHAZADA")} porque q0 " +
              $"{(automata.AceptaPalabraVacia ? "pertenece" : "no pertenece")} a F."
            : $"La palabra '{palabra}' es {(aceptada ? "ACEPTADA" : "RECHAZADA")}: " +
              $"el recorrido termina en {estadoFinal}, " +
              $"{(aceptada ? "que pertenece" : "que no pertenece")} a F.";

        return new ResultadoReconocimiento(
            palabra, true, aceptada, null, null, simbolos, recorrido, estadoFinal, mensajeFinal);
    }

    /// <summary>ε y λ representan la palabra vacía; también se admite el texto vacío.</summary>
    private static string Normalizar(string? entrada)
    {
        string texto = (entrada ?? string.Empty).Trim();
        return texto is "ε" or "λ" ? string.Empty : texto;
    }
}
