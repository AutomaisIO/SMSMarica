using SMSMais.Core.Common.Excecoes;

namespace SMSMais.Core.Common.ValueObjects;

/// <summary>
/// Helper de validação de coordenadas. O tipo de persistência é <c>SMSMais.Data.Entities.Gps</c>.
/// </summary>
public static class Gps
{
    public static void Validar(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ValidacaoException(nameof(latitude), "Latitude deve estar entre -90 e 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ValidacaoException(nameof(longitude), "Longitude deve estar entre -180 e 180.");
        }
    }
}
