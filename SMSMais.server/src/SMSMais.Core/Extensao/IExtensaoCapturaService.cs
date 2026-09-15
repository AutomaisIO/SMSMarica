using SMSMais.Core.Extensao.Dtos;

namespace SMSMais.Core.Extensao;

public interface IExtensaoCapturaService
{
    /// <summary>Grava um lote de capturas do SISREG vindo da extensão. Só inclusão.</summary>
    Task<CapturaLoteResultado> ReceberAsync(CapturaLoteRequest lote, CancellationToken ct = default);

    /// <summary>Panorama do que já chegou (contagens/metadados, sem PII de paciente).</summary>
    Task<CapturaResumoDto> ObterResumoAsync(CancellationToken ct = default);

    /// <summary>Estrutura das ações de escrita (nomes de campos do envio + rótulos da resposta),
    /// sem PII — para decidir o que dá para montar na base.</summary>
    Task<CapturaEstruturaDto> ObterEstruturaAsync(CancellationToken ct = default);

    /// <summary>Trechos redigidos da resposta de uma marcação (número/chave/procedimento/unidade),
    /// com CPF/CNS/telefone mascarados — para escrever o parser sem expor PII.</summary>
    Task<CapturaAmostraRespostaDto> ObterAmostraRespostaMarcacaoAsync(CancellationToken ct = default);
}
