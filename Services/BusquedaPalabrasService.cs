using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimuladorAutomatas.Models;

namespace SimuladorAutomatas.Services;

/// <summary>Una palabra aceptada junto con el recorrido que la acepta.</summary>
public sealed class PalabraEncontrada
{
    public string Palabra { get; }
    public int Longitud => Palabra.Length;
    public IReadOnlyList<string> Recorrido { get; }
    public string EstadoFinal => Recorrido[^1];

    public PalabraEncontrada(string palabra, IReadOnlyList<string> recorrido)
    {
        Palabra = palabra;
        Recorrido = recorrido;
    }
}

/// <summary>Resultado de la búsqueda de las palabras más cortas.</summary>
public sealed class ResultadoBusqueda
{
    public IReadOnlyList<PalabraEncontrada> Palabras { get; }
    public int LongitudMaxima { get; }

    public ResultadoBusqueda(IReadOnlyList<PalabraEncontrada> palabras, int longitudMaxima)
    {
        Palabras = palabras;
        LongitudMaxima = longitudMaxima;
    }
}

/// <summary>
/// Busca las cinco palabras aceptadas más cortas (longitud ≤ 10) usando
/// programación dinámica sobre el AFD: para cada (estado, longitud) se cuenta
/// cuántas palabras aceptadas salen de él. Esa información permite enumerar
/// sólo las ramas que efectivamente producen palabras aceptadas, en orden de
/// longitud y de símbolos, sin descartar palabras distintas que llegan al
/// mismo estado ni generar todo Σ* por fuerza bruta.
/// </summary>
public sealed class BusquedaPalabrasService
{
    public const int LongitudMaxima = 10;
    public const int LimitePalabras = 5;

    public ResultadoBusqueda Buscar(Automata automata, ConfiguracionAceptacion configuracion)
    {
        ArgumentNullException.ThrowIfNull(automata);
        ArgumentNullException.ThrowIfNull(configuracion);

        char[] simbolos = automata.Alfabeto.ToArray();
        int cantidadEstados = automata.Estados.Count;
        int indiceInicial = automata.ObtenerIndice(configuracion.EstadoInicial);

        var siguiente = new int[cantidadEstados, simbolos.Length];
        for (int i = 0; i < cantidadEstados; i++)
        {
            string estado = automata.Estados[i];
            for (int s = 0; s < simbolos.Length; s++)
            {
                string destino = automata.ObtenerDestino(estado, simbolos[s])!;
                siguiente[i, s] = automata.ObtenerIndice(destino);
            }
        }

        // conteo[estado, n] = cantidad de palabras de longitud n aceptadas
        // desde ese estado, saturada en 5 porque nunca necesitamos más.
        var conteo = new int[cantidadEstados, LongitudMaxima + 1];
        for (int i = 0; i < cantidadEstados; i++)
        {
            conteo[i, 0] = configuracion.EsFinal(automata.Estados[i]) ? 1 : 0;
        }

        for (int longitud = 1; longitud <= LongitudMaxima; longitud++)
        {
            for (int i = 0; i < cantidadEstados; i++)
            {
                int total = 0;
                for (int s = 0; s < simbolos.Length; s++)
                {
                    total += conteo[siguiente[i, s], longitud - 1];
                    if (total >= LimitePalabras)
                    {
                        total = LimitePalabras;
                        break;
                    }
                }

                conteo[i, longitud] = total;
            }
        }

        var palabras = new List<PalabraEncontrada>();
        var construccion = new StringBuilder();
        var recorrido = new List<string>();

        for (int longitud = 0; longitud <= LongitudMaxima && palabras.Count < LimitePalabras; longitud++)
        {
            if (conteo[indiceInicial, longitud] == 0)
            {
                continue;
            }

            construccion.Clear();
            recorrido.Clear();
            recorrido.Add(configuracion.EstadoInicial);

            Generar(automata, simbolos, siguiente, conteo, indiceInicial, longitud,
                    construccion, recorrido, palabras);
        }

        return new ResultadoBusqueda(palabras, LongitudMaxima);
    }

    private static void Generar(
        Automata automata,
        char[] simbolos,
        int[,] siguiente,
        int[,] conteo,
        int estadoActual,
        int restante,
        StringBuilder construccion,
        List<string> recorrido,
        List<PalabraEncontrada> palabras)
    {
        if (palabras.Count >= LimitePalabras)
        {
            return;
        }

        if (restante == 0)
        {
            palabras.Add(new PalabraEncontrada(construccion.ToString(), recorrido.ToArray()));
            return;
        }

        for (int s = 0; s < simbolos.Length && palabras.Count < LimitePalabras; s++)
        {
            int destino = siguiente[estadoActual, s];
            if (conteo[destino, restante - 1] == 0)
            {
                continue;
            }

            construccion.Append(simbolos[s]);
            recorrido.Add(automata.Estados[destino]);

            Generar(automata, simbolos, siguiente, conteo, destino, restante - 1,
                    construccion, recorrido, palabras);

            recorrido.RemoveAt(recorrido.Count - 1);
            construccion.Length--;
        }
    }
}
