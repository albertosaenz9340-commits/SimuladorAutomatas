using System;
using System.Collections.Generic;
using SimuladorAutomatas.Models;

namespace SimuladorAutomatas.Services;

/// <summary>
/// Servicio central que conserva la única instancia del autómata compartida
/// por los tres módulos. Los formularios no guardan copias propias del AFD.
/// </summary>
public sealed class AutomataService
{
    private static readonly Lazy<AutomataService> InstanciaUnique =
        new(() => new AutomataService(new ValidacionAutomataService()));

    private readonly ValidacionAutomataService _validador;

    public static AutomataService Instancia => InstanciaUnique.Value;

    /// <summary>Autómata actualmente construido, o null si todavía no existe.</summary>
    public Automata? Actual { get; private set; }

    public bool HayAutomata => Actual is not null;

    public event EventHandler? AutomataCambiado;

    public AutomataService(ValidacionAutomataService validador)
    {
        _validador = validador ?? throw new ArgumentNullException(nameof(validador));
    }

    /// <summary>
    /// Valida la definición y, si es correcta, reemplaza el autómata actual.
    /// </summary>
    public ResultadoValidacion Establecer(
        string nombre,
        IReadOnlyList<string> estados,
        IReadOnlyList<char> alfabeto,
        string estadoInicial,
        IReadOnlyList<string> estadosFinales,
        IReadOnlyList<Transicion> transiciones)
    {
        ResultadoValidacion resultado = _validador.Validar(
            estados, alfabeto, estadoInicial, estadosFinales, transiciones);

        if (!resultado.EsValido)
        {
            return resultado;
        }

        Actual = new Automata(nombre, estados, alfabeto, estadoInicial, estadosFinales, transiciones);
        AutomataCambiado?.Invoke(this, EventArgs.Empty);
        return resultado;
    }

    /// <summary>Fuerza una instancia ya validada (usada por los casos de prueba).</summary>
    public void Establecer(Automata automata)
    {
        Actual = automata ?? throw new ArgumentNullException(nameof(automata));
        AutomataCambiado?.Invoke(this, EventArgs.Empty);
    }

    public void Limpiar()
    {
        Actual = null;
        AutomataCambiado?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Obtiene el AFD actual o lanza una excepción clara si no existe.</summary>
    public Automata ObtenerActualOExcepcion()
    {
        return Actual ?? throw new InvalidOperationException(
            "Todavía no se ha construido ningún autómata. Use el Módulo 1 para crearlo.");
    }
}
