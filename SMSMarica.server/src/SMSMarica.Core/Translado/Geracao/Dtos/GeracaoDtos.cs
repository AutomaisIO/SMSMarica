namespace SMSMarica.Core.Translado.Geracao.Dtos;

/// <summary>Gera o translado de um dia. <see cref="Confirmar"/>=false só simula (preview, sem gravar).</summary>
public sealed record GerarTransladoRequest(DateOnly Data, bool Confirmar = false);

public sealed record ParadaGeradaDto(int Ordem, Guid SessaoId, Guid PacienteId, string PacienteNome, bool ComAcompanhante);

public sealed record RotaGeradaDto(
    Guid? RotaId,
    Guid VeiculoId,
    string VeiculoPlaca,
    Guid MotoristaId,
    string MotoristaNome,
    Guid UnidadeId,
    string UnidadeNome,
    int QtdPacientes,
    int DistanciaTotalMetros,
    int DuracaoEstimadaSegundos,
    IReadOnlyList<ParadaGeradaDto> Paradas);

public sealed record SessaoNaoAlocadaDto(
    Guid SessaoId, Guid PacienteId, string PacienteNome, Guid UnidadeId, string UnidadeNome, string Motivo);

public sealed record ResultadoGeracaoDto(
    DateOnly Data,
    bool Confirmado,
    bool UsouIa,
    bool Aproximado,
    int TotalSessoes,
    int TotalAlocadas,
    IReadOnlyList<RotaGeradaDto> Rotas,
    IReadOnlyList<SessaoNaoAlocadaDto> NaoAlocadas);
