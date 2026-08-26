using SMSMais.Core.RoboAtendimento.Dtos;

namespace SMSMais.Core.RoboAtendimento;

public interface IRoboConfiguracaoService
{
    Task<RoboConfiguracaoDto> ObterAsync(CancellationToken ct = default);

    Task SalvarAsync(SalvarRoboConfiguracaoRequest request, CancellationToken ct = default);
}
