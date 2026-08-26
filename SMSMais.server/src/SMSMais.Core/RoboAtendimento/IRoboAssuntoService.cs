using SMSMais.Core.RoboAtendimento.Dtos;

namespace SMSMais.Core.RoboAtendimento;

public interface IRoboAssuntoService
{
    Task<IReadOnlyList<RoboAssuntoListItemDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);

    Task<RoboAssuntoDto> ObterAsync(Guid id, CancellationToken ct = default);

    Task<Guid> CriarAsync(SalvarRoboAssuntoRequest request, CancellationToken ct = default);

    Task AtualizarAsync(Guid id, SalvarRoboAssuntoRequest request, CancellationToken ct = default);

    Task ExcluirAsync(Guid id, CancellationToken ct = default);

    /// <summary>Catálogo fixo dos comandos que podem ser habilitados por assunto.</summary>
    IReadOnlyList<ComandoRoboCatalogoDto> ListarCatalogoComandos();
}
