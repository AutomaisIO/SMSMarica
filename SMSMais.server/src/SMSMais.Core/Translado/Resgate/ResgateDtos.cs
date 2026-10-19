using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Translado.Resgate;

/// <summary>Se o paciente já pode ser resgatado.</summary>
public enum DisponibilidadeResgate
{
    /// <summary>Avisaram que ele já está no local esperando (paciente, central ou motorista).</summary>
    NoLocal = 1,

    /// <summary>Ninguém avisou, mas o tempo do procedimento já passou.</summary>
    PelaPrevisao = 2,

    /// <summary>Ainda em atendimento: disponível em <c>DisponivelEmMinutos</c>.</summary>
    EmAtendimento = 3,

    /// <summary>Sem hora marcada nem tempo médio do procedimento: só o aviso diz quando.</summary>
    SemPrevisao = 4,
}

/// <summary>
/// Um paciente da lista "Pacientes disponíveis para resgate": já foi levado à unidade hoje e ainda não foi
/// resgatado nem marcado por nenhum carro. <paramref name="Cabe"/>/<paramref name="MotivoNaoCabe"/>: se cabe
/// agora no carro de quem está olhando (nulo quando a lista não é de um carro).
/// </summary>
public sealed record ResgateDisponivelDto(
    Guid SessaoId,
    string PacienteNome,
    Guid UnidadeId,
    string UnidadeNome,
    string? UnidadeEndereco,
    double? Latitude,
    double? Longitude,
    bool ForaDoMunicipio,
    int Acompanhantes,
    MobilidadeTransporte Mobilidade,
    bool Isolamento,
    DisponibilidadeResgate Disponibilidade,
    TimeOnly? DisponivelAs,
    int? DisponivelEmMinutos,
    DateTime? NoLocalDesde,
    /// <summary>Há quantos minutos ele espera no local (só quando está no local).</summary>
    int? AguardandoMinutos,
    CanalAvisoResgate? AvisadoPor,
    DateTime? NaoEstavaNoLocalEm,
    DateTime? AindaEmAtendimentoEm,
    string? VeiculoDaIda,
    int? DistanciaMetros,
    bool? Cabe,
    string? MotivoNaoCabe);

/// <summary>Quem este carro já marcou para resgatar e ainda não embarcou (pode desmarcar).</summary>
public sealed record ResgateMarcadoDto(
    Guid SessaoId,
    string PacienteNome,
    Guid UnidadeId,
    string UnidadeNome,
    int Acompanhantes,
    DisponibilidadeResgate Disponibilidade,
    TimeOnly? DisponivelAs,
    int? DisponivelEmMinutos);

/// <summary>A lista de resgate. Para um carro, também os lugares livres agora e quem ele já marcou.</summary>
public sealed record ResgatesDto(
    IReadOnlyList<ResgateDisponivelDto> Disponiveis,
    IReadOnlyList<ResgateMarcadoDto> Marcados,
    int? LugaresLivres,
    int? LugaresCadeiranteLivres);

/// <summary>O tablet marca, de uma vez, quem vai resgatar.</summary>
public sealed record MarcarResgateRequest(Guid RotaId, IReadOnlyList<Guid> SessaoIds, Guid? MotoristaId = null);

/// <summary>A central marca, de uma vez, quem a rota vai resgatar.</summary>
public sealed record MarcarResgatesDaRotaRequest(IReadOnlyList<Guid> SessaoIds);

/// <summary>O resgate de hoje como o paciente vê no app: se avisou que está no local, quando fica pronto
/// pela previsão e qual carro vai buscá-lo. <paramref name="PerguntaPendente"/>: a previsão estourou e é
/// hora de perguntar "você já está disponível para retorno?" (com "Sim" e "Não, ainda não terminei");
/// <paramref name="ProximaPerguntaAs"/>: depois de um "não", quando a pergunta volta.</summary>
public sealed record ResgateDoPacienteDto(
    Guid SessaoId,
    string Unidade,
    DisponibilidadeResgate Disponibilidade,
    TimeOnly? DisponivelAs,
    DateTime? NoLocalDesde,
    CanalAvisoResgate? AvisadoPor,
    string? VeiculoPlaca,
    string? VeiculoModelo,
    bool Embarcou,
    bool PerguntaPendente = false,
    DateTime? ProximaPerguntaAs = null);

/// <summary>A resposta à pergunta do resgate: <paramref name="Disponivel"/> = "Sim, já estou disponível";
/// falso = "Não, ainda não terminei o procedimento" (pergunta de novo em 10 minutos).</summary>
public sealed record ResponderResgateRequest(bool Disponivel);
