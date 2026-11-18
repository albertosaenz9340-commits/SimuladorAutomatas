using System;

namespace SimuladorAutomatas.Models;

/// <summary>
/// Representa una transición determinista δ(origen, símbolo) = destino.
/// </summary>
public sealed class Transicion : IEquatable<Transicion>
{
    public string Origen { get; }
    public char Simbolo { get; }
    public string Destino { get; }

    public Transicion(string origen, char simbolo, string destino)
    {
        if (string.IsNullOrWhiteSpace(origen))
        {
            throw new ArgumentException("El estado de origen no puede estar vacío.", nameof(origen));
        }

        if (string.IsNullOrWhiteSpace(destino))
        {
            throw new ArgumentException("El estado de destino no puede estar vacío.", nameof(destino));
        }

        Origen = origen.Trim();
        Simbolo = simbolo;
        Destino = destino.Trim();
    }

    public bool Equals(Transicion? otra) =>
        otra is not null &&
        string.Equals(Origen, otra.Origen, StringComparison.Ordinal) &&
        Simbolo == otra.Simbolo &&
        string.Equals(Destino, otra.Destino, StringComparison.Ordinal);

    public override bool Equals(object? objeto) => objeto is Transicion otra && Equals(otra);

    public override int GetHashCode() => HashCode.Combine(
        StringComparer.Ordinal.GetHashCode(Origen), Simbolo,
        StringComparer.Ordinal.GetHashCode(Destino));

    public override string ToString() => $"δ({Origen}, {Simbolo}) = {Destino}";
}
