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

    /// <summary>Terna M = (Q, Σ, δ) construida, o null si todavía no existe.</summary>
    public Automata? Actual { get; private set; }

    /// <summary>Configuración de aceptación (q0, F), externa a la terna.</summary>
    public ConfiguracionAceptacion? ConfiguracionActual { get; private set; }

    public bool HayAutomata => Actual is not null && ConfiguracionActual is not null;

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

        Actual = new Automata(nombre, estados, alfabeto, transiciones);
        ConfiguracionActual = new ConfiguracionAceptacion(estadoInicial, estadosFinales);
        AutomataCambiado?.Invoke(this, EventArgs.Empty);
        return resultado;
    }

    /// <summary>Fuerza una definición ya validada (usada por los casos de prueba).</summary>
    public void Establecer(Automata automata, ConfiguracionAceptacion configuracion)
    {
        Actual = automata ?? throw new ArgumentNullException(nameof(automata));
        ConfiguracionActual = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
        AutomataCambiado?.Invoke(this, EventArgs.Empty);
    }

    public void Limpiar()
    {
        Actual = null;
        ConfiguracionActual = null;
        AutomataCambiado?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Obtiene la terna actual o lanza una excepción clara si no existe.</summary>
    public Automata ObtenerActualOExcepcion()
    {
        return Actual ?? throw new InvalidOperationException(
            "Todavía no se ha construido ningún autómata. Use el Módulo 1 para crearlo.");
    }

    /// <summary>Obtiene la configuración de aceptación actual o lanza una excepción clara.</summary>
    public ConfiguracionAceptacion ObtenerConfiguracionOExcepcion()
    {
        return ConfiguracionActual ?? throw new InvalidOperationException(
            "Todavía no se ha definido el estado inicial ni los estados finales. Use el Módulo 1.");
    }
}
