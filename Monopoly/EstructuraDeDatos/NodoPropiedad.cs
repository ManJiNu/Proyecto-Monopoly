public class NodoPropiedad
{
    public Propiedad PropiedadActual {set;get;}
    public NodoPropiedad Siguiente {set;get;}
    public NodoPropiedad(Propiedad propiedad)
    {
        PropiedadActual = propiedad;
        Siguiente = null;
    }
}