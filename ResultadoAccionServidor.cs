using System;

public sealed class ResultadoAccionServidor
{
    public string? RespuestaPrivada { get; set; }
    public string? MensajeBroadcast { get; set; }
    public Jugador? JugadorRegistrado { get; set; }

    public void AgregarRespuesta(string mensaje)
    {
        if (string.IsNullOrEmpty(RespuestaPrivada))
            RespuestaPrivada = mensaje;
        else
            RespuestaPrivada += Environment.NewLine + mensaje;
    }

    public void AgregarBroadcast(string mensaje)
    {
        if (string.IsNullOrEmpty(MensajeBroadcast))
            MensajeBroadcast = mensaje;
        else
            MensajeBroadcast += Environment.NewLine + mensaje;
    }
}
