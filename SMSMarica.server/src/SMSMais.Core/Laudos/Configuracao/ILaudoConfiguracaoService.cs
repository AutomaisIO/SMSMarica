using SMSMais.Core.Laudos.Configuracao.Dtos;

namespace SMSMais.Core.Laudos.Configuracao;

public interface ILaudoConfiguracaoService
{
    /// <summary>Obtém a configuração global (cria vazia na primeira chamada).</summary>
    Task<LaudoConfiguracaoDto> ObterAsync(CancellationToken cancellationToken = default);

    /// <summary>Só as regras de iniciar o laudo (leitura leve, sem cabeçalho/rodapé).</summary>
    Task<RegrasIniciarLaudoDto> ObterRegrasAsync(CancellationToken cancellationToken = default);

    /// <summary>Salva (upsert) o cabeçalho/rodapé global. HTML é sanitizado.</summary>
    Task<LaudoConfiguracaoDto> SalvarAsync(
        Guid usuarioId,
        SalvarLaudoConfiguracaoRequest request,
        CancellationToken cancellationToken = default);
}
