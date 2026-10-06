using SMSMais.Core.Integracoes.Sisreg.Base.Dtos;

namespace SMSMais.Core.Integracoes.Sisreg.Base;

/// <summary>
/// Consulta dos agendamentos do SISREG <b>na nossa base</b> (tabela <c>solicitacao</c>, alimentada
/// pela importação e pelo sincronismo diário) — sem tocar no SISREG, sem gastar o orçamento
/// anti-robô do operador. Respeita o escopo de unidade do usuário.
/// </summary>
public interface IConsultaBaseSisregService
{
    Task<ConsultaBaseSisregResultado> BuscarAsync(ConsultaBaseSisregFiltro filtro, CancellationToken ct = default);

    Task<OpcoesConsultaBaseSisregDto> OpcoesAsync(
        DateOnly inicio, DateOnly fim, EixoDataConsultaSisreg eixo, CancellationToken ct = default);

    /// <summary>O resultado inteiro (sem paginação) em PDF nominal, ordenado por paciente.</summary>
    Task<byte[]> PdfAsync(ConsultaBaseSisregFiltro filtro, CancellationToken ct = default);
}
