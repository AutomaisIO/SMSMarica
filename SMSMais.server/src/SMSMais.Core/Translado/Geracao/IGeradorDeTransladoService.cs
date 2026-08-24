using SMSMais.Core.Translado.Geracao.Dtos;

namespace SMSMais.Core.Translado.Geracao;

/// <summary>
/// Motor de geração de translado (FT3): distribui os pacientes elegíveis do dia nos carros
/// (capacidade + assento de acompanhante, agrupando por destino) e sequencia as paradas de
/// coleta. v1 usa heurística + distância haversine; Google Routes/Distance Matrix e Claude
/// são evoluções (a chamada externa pluga aqui). Ver ADR-0017.
/// </summary>
public interface IGeradorDeTransladoService
{
    Task<ResultadoGeracaoDto> GerarAsync(GerarTransladoRequest request, CancellationToken ct = default);
}
