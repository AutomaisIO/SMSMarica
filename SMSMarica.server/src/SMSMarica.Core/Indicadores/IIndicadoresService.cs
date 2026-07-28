using SMSMarica.Core.Indicadores.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Indicadores;

public interface IIndicadoresService
{
    /// <summary>Unidades disponíveis no filtro. Hoje só o HMCML (hospital 1 na base Salux).</summary>
    IReadOnlyList<UnidadeIndicadorDto> Unidades();

    /// <summary>Bases de dados onde um motor pode rodar.</summary>
    Task<IReadOnlyList<FonteIndicadorDto>> ListarFontesAsync(CancellationToken ct = default);

    /// <summary>
    /// Indicadores de uma aba, já com o resultado do período quando existir execução gravada.
    /// Não dispara consulta na base de origem — a apuração é sob demanda.
    /// </summary>
    Task<IReadOnlyList<IndicadorResumoDto>> ListarAsync(
        AbaIndicador aba, FiltroIndicadorDto filtro, CancellationToken ct = default);

    Task<IndicadorDetalheDto> ObterAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<IndicadorVersaoDto>> ListarVersoesAsync(Guid id, CancellationToken ct = default);

    /// <summary>Cadastra um indicador novo (a planilha contratual pode ganhar linhas).</summary>
    Task<IndicadorDetalheDto> CriarAsync(SalvarIndicadorDto dto, CancellationToken ct = default);

    /// <summary>
    /// Grava o cadastro inteiro — meta, peso, memória de cálculo e SQL. Se o SQL mudou,
    /// versiona antes de sobrescrever.
    /// </summary>
    Task<IndicadorDetalheDto> AtualizarAsync(
        Guid id, SalvarIndicadorDto dto, CancellationToken ct = default);

    Task ExcluirAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Executa o indicador contra a base de origem e grava o resultado.
    /// <paramref name="persistir"/> falso = prévia da tela de edição (não guarda histórico).
    /// </summary>
    Task<ResultadoIndicadorDto> ApurarAsync(
        Guid id, FiltroIndicadorDto filtro, bool persistir = true, CancellationToken ct = default);

    /// <summary>Apura todos os indicadores com motor de uma aba, em sequência (não paraleliza no Oracle).</summary>
    Task<IReadOnlyList<IndicadorResumoDto>> ApurarAbaAsync(
        AbaIndicador aba, FiltroIndicadorDto filtro, CancellationToken ct = default);

    /// <summary>
    /// Executa o SQL analítico e devolve a evidência linha a linha do período: os registros que
    /// entraram na conta e os que foram excluídos, com o motivo. Nada é persistido — é sempre
    /// uma leitura nova da base de origem, feita na hora da exportação.
    /// </summary>
    Task<AnaliticoIndicadorDto> AnaliticoAsync(
        Guid id, FiltroIndicadorDto filtro, CancellationToken ct = default);
}
