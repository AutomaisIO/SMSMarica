using SMSMais.Core.Tratamentos.Dtos;

namespace SMSMais.Core.Translado.Geracao.Dtos;

/// <summary>
/// Gera o translado de um dia. <see cref="Confirmar"/>=false só simula (preview, sem gravar).
/// <see cref="UsarIa"/>=true usa o Claude para distribuir os pacientes nos veículos (com
/// revalidação de capacidade no backend e fallback para a heurística determinística).
/// </summary>
public sealed record GerarTransladoRequest(DateOnly Data, bool Confirmar = false, bool UsarIa = true);

/// <summary>Uma parada da rota gerada. <paramref name="Necessidades"/> e os acompanhantes vêm do
/// atendimento só para leitura de quem confere a rota — a distribuição ainda não os considera.</summary>
public sealed record ParadaGeradaDto(
    int Ordem,
    Guid SessaoId,
    Guid PacienteId,
    string PacienteNome,
    bool ComAcompanhante,
    NecessidadesDto? Necessidades,
    int AcompanhantesPrevistos,
    int LimiteAcompanhantes);

public sealed record RotaGeradaDto(
    Guid? RotaId,
    Guid VeiculoId,
    string VeiculoPlaca,
    Guid MotoristaId,
    string MotoristaNome,
    Guid UnidadeAtendimentoId,
    string UnidadeAtendimentoNome,
    int QtdPacientes,
    int DistanciaTotalMetros,
    int DuracaoEstimadaSegundos,
    IReadOnlyList<ParadaGeradaDto> Paradas);

public sealed record SessaoNaoAlocadaDto(
    Guid SessaoId, Guid PacienteId, string PacienteNome, Guid UnidadeAtendimentoId, string UnidadeAtendimentoNome, string Motivo);

public sealed record ResultadoGeracaoDto(
    DateOnly Data,
    bool Confirmado,
    bool UsouIa,
    bool Aproximado,
    int TotalSessoes,
    int TotalAlocadas,
    IReadOnlyList<RotaGeradaDto> Rotas,
    IReadOnlyList<SessaoNaoAlocadaDto> NaoAlocadas);
