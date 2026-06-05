using SMSMarica.Core.Integracoes.Sisreg.Dtos;

namespace SMSMarica.Core.Integracoes.Sisreg;

/// <summary>
/// As 6 consultas de leitura do Manual da API SISREG v2.1 §4, expostas como métodos
/// tipados. Cada uma monta o filtro de centrais reguladoras a partir da configuração e
/// devolve o DTO do índice correspondente. Só-leitura — o SISREG não aceita escrita.
/// </summary>
public interface ISisregConsultaService
{
    /// <summary>§4.1 Novas solicitações (marcação-ambulatorial) por intervalo de <c>data_solicitacao</c>.</summary>
    Task<SisregBuscaResultado<MarcacaoAmbulatorialSisregDto>> NovasSolicitacoesAmbulatoriaisAsync(
        IntervaloDatas intervalo, int tamanho = 100, CancellationToken cancellationToken = default);

    /// <summary>§4.2 Fila de solicitações pendentes/reenviadas (solicitação-ambulatorial).</summary>
    Task<SisregBuscaResultado<SolicitacaoAmbulatorialSisregDto>> FilaSolicitacoesAmbulatoriaisAsync(
        int tamanho = 100, CancellationToken cancellationToken = default);

    /// <summary>§4.3 Solicitações agendadas (marcação-ambulatorial) por intervalo de <c>data_aprovacao</c>.</summary>
    Task<SisregBuscaResultado<MarcacaoAmbulatorialSisregDto>> SolicitacoesAgendadasAsync(
        IntervaloDatas intervalo, int tamanho = 100, CancellationToken cancellationToken = default);

    /// <summary>§4.4 Solicitações atendidas (marcação-ambulatorial) por intervalo de <c>data_confirmacao</c>.</summary>
    Task<SisregBuscaResultado<MarcacaoAmbulatorialSisregDto>> SolicitacoesAtendidasAsync(
        IntervaloDatas intervalo, int tamanho = 100, CancellationToken cancellationToken = default);

    /// <summary>§4.5 Solicitações canceladas/devolvidas pela CRAM (marcação-ambulatorial).</summary>
    Task<SisregBuscaResultado<MarcacaoAmbulatorialSisregDto>> SolicitacoesCanceladasDevolvidasAsync(
        int tamanho = 100, CancellationToken cancellationToken = default);

    /// <summary>§4.6 Solicitações de internação (solicitação-hospitalar) por intervalo de <c>data_solicitacao</c>.</summary>
    Task<SisregBuscaResultado<SolicitacaoHospitalarSisregDto>> InternacoesHospitalaresAsync(
        IntervaloDatas intervalo, int tamanho = 100, CancellationToken cancellationToken = default);
}
