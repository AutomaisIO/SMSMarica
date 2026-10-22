using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Translado.Dtos;

public sealed record AlocacaoDto(
    Guid Id,
    Guid SessaoId,
    Guid TratamentoId,
    Guid PacienteId,
    string PacienteNome,
    Guid UnidadeAtendimentoId,
    string UnidadeAtendimentoNome,
    TimeOnly? HoraPrevistaBusca,
    Guid AssentoId,
    int FileiraOrdem,
    int NumeroAssento,
    TipoAlocacao Tipo,
    /// <summary>Situação da viagem (sessão): o tablet marca realizada/não realizada; a central corrige.</summary>
    StatusSessao StatusSessao = StatusSessao.Pendente,
    /// <summary>A central confirmou/corrigiu a viagem no painel — vale isso, não o tablet.</summary>
    bool CorrigidaNoPainel = false,
    /// <summary>Ida (<see cref="TipoParada.Coleta"/>) ou resgate (<see cref="TipoParada.Retorno"/>). No
    /// resgate o assento volta a ser usado depois que quem foi nele desceu.</summary>
    TipoParada Parada = TipoParada.Coleta,
    /// <summary>No paciente: acompanhantes que vão e estão sem assento no carro.</summary>
    int AcompanhantesSemLugar = 0);
