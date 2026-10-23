using SMSMais.Core.FaturamentoProducao.Dtos;

namespace SMSMais.Core.FaturamentoProducao;

/// <summary>
/// Faturamento de Produção (BPA de procedimentos/consultas a partir da nossa base — SISREG, com
/// comparecimento confirmado). Gera o arquivo BPA Magnético e o preview linha a linha + estatísticas.
/// </summary>
public interface IBpaProducaoService
{
    /// <summary>Unidades liberadas para o usuário final gerar BPA (habilitadas ∩ escopo). Ação Consulta.</summary>
    Task<IReadOnlyList<UnidadeHabilitadaDto>> ListarUnidadesHabilitadasAsync(CancellationToken ct = default);

    /// <summary>Todas as unidades do escopo + flag de habilitada (tela de Configurações). Ação Edição.</summary>
    Task<IReadOnlyList<ConfigUnidadeDto>> ListarConfiguracaoAsync(CancellationToken ct = default);

    /// <summary>Liga/desliga uma unidade para o faturamento de produção. Ação Edição.</summary>
    Task DefinirHabilitadaAsync(Guid unidadeId, bool habilitada, CancellationToken ct = default);

    /// <summary>Preview da competência: linhas (válidas + pendências) e estatísticas de qualidade.</summary>
    Task<BpaPreviewDto> PreviewAsync(Guid unidadeId, int competencia, CancellationToken ct = default);

    /// <summary>Gera o arquivo BPA (só as linhas válidas). Devolve os bytes e o nome do arquivo.</summary>
    Task<(byte[] Conteudo, string NomeArquivo)> GerarArquivoAsync(Guid unidadeId, int competencia, CancellationToken ct = default);
}
