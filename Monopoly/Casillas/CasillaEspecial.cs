public class CasillaEspecial : Casilla
{
    public CasillaEspecial(int id, string nombre) : base(id, nombre) { }

    public override void EjecutarEfecto(Jugador jugador)
    {
        // El efecto depende de cuál casilla especial sea (se distingue por el nombre)
        switch (Nombre)
        {
            case "Salida":
                // El bono por pasar/caer en Salida normalmente ya se paga en otra
                // parte del motor del juego (cuando el jugador completa una vuelta)
                Console.WriteLine($"{jugador.Nombre} cayó en Salida.");
                break;

            case "Ir a la Cárcel":
                jugador.EstaEnCarcel = true;
                Console.WriteLine($"{jugador.Nombre} fue enviado a la Cárcel.");
                break;

            case "Cárcel (De visita)":
                // Si no está preso, solo está de visita: no pasa nada
                Console.WriteLine($"{jugador.Nombre} está de visita en la Cárcel.");
                break;

            case "Parqueo Gratis":
                // No tiene ningún efecto sobre el jugador
                Console.WriteLine($"{jugador.Nombre} descansa en el Parqueo Gratis.");
                break;

            default:
                Console.WriteLine($"{jugador.Nombre} cayó en la casilla especial \"{Nombre}\".");
                break;
        }
    }
}
