using System;

namespace SimuladorAutomatas.Models;

/// <summary>
/// Representa un estado del autómata. Su identidad viene dada por su nombre,
/// comparado de forma exacta (distingue mayúsculas y minúsculas).
/// </summary>
public sealed class Estado : IEquatable<Estado>
{
    public string Nombre { get; }

    public Estado(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException("El nombre del estado no puede estar vacío.", nameof(nombre));
        }

        Nombre = nombre.Trim();
    }

    public bool Equals(Estado? otro) =>
        otro is not null && string.Equals(Nombre, otro.Nombre, StringComparison.Ordinal);

    public override bool Equals(object? objeto) => objeto is Estado otro && Equals(otro);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Nombre);

    public override string ToString() => Nombre;
}
