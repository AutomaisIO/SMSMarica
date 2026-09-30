using SMSMais.Core.Integracoes.EsusSgWeb;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Integracoes.SernitWeb;

namespace SMSMais.Core.Regulacao.Estatisticas;

/// <summary>Qual fila espelhada as estatísticas leem: SER (SES-RJ) ou SERNIT (Niterói).</summary>
public enum FonteEstatisticaExterna
{
    Ser = 1,
    Sernit = 2,

    /// <summary>ESUS de São Gonçalo (ADR-0063): trilha montada em <c>esussg_evento</c> com os mesmos
    /// nomes de coluna — quem incluiu na fila (Maricá) e quem agendou (São Gonçalo).</summary>
    EsusSg = 3,
}

/// <summary>
/// O que muda entre SER e SERNIT para as estatísticas: o prefixo das tabelas
/// (<c>ser_evento</c>/<c>sernit_evento</c>, <c>ser_solicitacao</c>/<c>sernit_solicitacao</c>), o
/// provedor da credencial onde a lista de habilitados mora, e o rótulo. O resto — colunas, verbo
/// tipado, trilha — é idêntico (ADR-0042: o SERNIT é o irmão do SER em tabelas próprias).
/// </summary>
public static class FonteEstatisticaExternaExtensoes
{
    public static string PrefixoTabela(this FonteEstatisticaExterna fonte) => fonte switch
    {
        FonteEstatisticaExterna.Ser => "ser",
        FonteEstatisticaExterna.Sernit => "sernit",
        FonteEstatisticaExterna.EsusSg => "esussg",
        _ => throw new ArgumentOutOfRangeException(nameof(fonte), fonte, null),
    };

    public static string Provedor(this FonteEstatisticaExterna fonte) => fonte switch
    {
        FonteEstatisticaExterna.Ser => SerWebSessao.Provedor,
        FonteEstatisticaExterna.Sernit => SernitWebSessao.Provedor,
        FonteEstatisticaExterna.EsusSg => EsusSgSessao.Provedor,
        _ => throw new ArgumentOutOfRangeException(nameof(fonte), fonte, null),
    };

    public static string Rotulo(this FonteEstatisticaExterna fonte) => fonte switch
    {
        FonteEstatisticaExterna.Ser => "SER",
        FonteEstatisticaExterna.Sernit => "SERNIT",
        FonteEstatisticaExterna.EsusSg => "ESUS São Gonçalo",
        _ => throw new ArgumentOutOfRangeException(nameof(fonte), fonte, null),
    };
}
