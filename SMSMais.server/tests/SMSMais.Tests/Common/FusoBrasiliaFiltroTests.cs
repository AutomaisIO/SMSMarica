using SMSMais.Core.Common.Tempo;

namespace SMSMais.Tests.Common;

/// <summary>
/// Limite de filtro da tela para coluna de instante. A Mensageria manda <c>?de=2026-10-02T00:00:00</c>
/// (sem fuso) e o Npgsql recusa <c>Kind=Unspecified</c> — a tela caía com 500 (ERRO-Y6K93P, 02/10/2026).
/// </summary>
public class FusoBrasiliaFiltroTests
{
    [Fact]
    public void Sem_fuso_e_dia_de_Brasilia_e_vira_UTC()
    {
        var limite = FusoBrasilia.LimiteDeFiltroParaUtc(new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Unspecified));

        Assert.Equal(DateTimeKind.Utc, limite.Kind);
        Assert.Equal(new DateTime(2026, 10, 2, 3, 0, 0, DateTimeKind.Utc), limite);
    }

    [Fact]
    public void Ja_em_UTC_passa_intacto()
    {
        var utc = new DateTime(2026, 10, 2, 3, 0, 0, DateTimeKind.Utc);

        Assert.Equal(utc, FusoBrasilia.LimiteDeFiltroParaUtc(utc));
    }

    [Fact]
    public void Local_e_normalizado_para_UTC()
    {
        var local = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Local);
        var limite = FusoBrasilia.LimiteDeFiltroParaUtc(local);

        Assert.Equal(DateTimeKind.Utc, limite.Kind);
        Assert.Equal(local.ToUniversalTime(), limite);
    }
}
