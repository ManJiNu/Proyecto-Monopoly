using System;

public static class Protocolo
{
    public const int LongitudMaximaMensaje = 512;
    public const int LongitudMaximaNombre = 40;

    public const string Conectar = "CONECTAR";
    public const string TirarDados = "TIRAR_DADOS";
    public const string ComprarPropiedad = "COMPRAR_PROPIEDAD";
    public const string NoComprar = "NO_COMPRAR";
    public const string TerminarTurno = "TERMINAR_TURNO";
    public const string ConsultarEstado = "CONSULTAR_ESTADO";
    public const string ConsultarTransacciones = "CONSULTAR_TRANSACCIONES";

    public static string[] Separar(string mensaje)
    {
        return mensaje.Split('|');
    }

    public static bool NombreValido(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return false;

        string limpio = nombre.Trim();
        if (limpio.Length > LongitudMaximaNombre)
            return false;

        return !limpio.Contains('|') && !limpio.Contains('\r') && !limpio.Contains('\n');
    }

    public static string LimpiarTexto(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
            return string.Empty;

        return texto
            .Replace('|', '/')
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    public static string Error(string tipo, string descripcion)
    {
        return $"ERROR|{tipo}|{LimpiarTexto(descripcion)}";
    }
}
