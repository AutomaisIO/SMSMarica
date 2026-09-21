namespace SMSMais.Core.Notificacoes.Confirmacoes.Dtos;

/// <summary>
/// A produção de UMA atendente na tela de Confirmações, no período. Tudo sai da trilha
/// <c>atendimento_confirmacao_evento</c> — cada número é uma contagem de atos dela, não de fichas.
/// </summary>
/// <param name="Pegou">Começou a atender, assumiu de alguém ou retomou uma estacionada.</param>
/// <param name="Cancelou">Cancelou a vaga AQUI. O que aconteceu no SISREG está nas duas seguintes.</param>
/// <param name="CancelouNoSisreg">Cancelamentos que o SISREG confirmou (ou que já estavam cancelados lá).</param>
/// <param name="SisregRecusou">Tentativas de cancelar que o SISREG recusou — nada mudou aqui.</param>
/// <param name="AvisouPaciente">Avisos de cancelamento que saíram na hora para o paciente.</param>
/// <param name="Desfechos">Confirmou + cancelou + pendente + telefone errado + telefone corrigido +
/// pedido desfeito: toda ficha que ela tirou da frente, para qualquer lado.</param>
/// <param name="TempoAteDesfechoMin">Mediana, em minutos, entre pegar a ficha e dar o desfecho.</param>
/// <param name="RitmoMin">Mediana, em minutos, entre um desfecho e o seguinte no mesmo dia.
/// Intervalos acima de <see cref="EquipeConfirmacoesDto.PausaMin"/> são pausa e não contam.</param>
public sealed record AtendenteProducaoDto(
    Guid UsuarioId,
    string Nome,
    int Pegou,
    int Confirmou,
    int Cancelou,
    int CancelouNoSisreg,
    int SisregRecusou,
    int AvisouPaciente,
    int Pendente,
    int ContatoErrado,
    int ContatoCorrigido,
    int PedidoDesfeito,
    int Liberou,
    int Transferiu,
    int Desfechos,
    double? TempoAteDesfechoMin,
    double? RitmoMin,
    int DiasAtivos,
    DateTime? PrimeiraAcaoEm,
    DateTime? UltimaAcaoEm);

/// <summary>Desfechos da equipe num dia (dia de Brasília).</summary>
public sealed record EquipeDiaDto(DateOnly Dia, int Confirmou, int Cancelou, int Outros);

/// <param name="TrilhaDesde">Primeiro evento que a trilha tem — antes disso não há o que contar,
/// e a tela precisa dizer isso em vez de mostrar zero como se fosse ociosidade.</param>
/// <param name="PausaMin">Teto, em minutos, do intervalo que ainda conta como ritmo.</param>
public sealed record EquipeConfirmacoesDto(
    DateOnly De,
    DateOnly Ate,
    DateTime? TrilhaDesde,
    int PausaMin,
    AtendenteProducaoDto Total,
    IReadOnlyList<AtendenteProducaoDto> Atendentes,
    IReadOnlyList<EquipeDiaDto> PorDia);

/// <summary>Um ato da atendente, para a linha do tempo dela. Sem dado de paciente: o código da
/// solicitação basta para achar a ficha na própria tela.</summary>
public sealed record AtoAtendenteDto(
    string Tipo,
    DateTime OcorridoEm,
    Guid SolicitacaoId,
    string? CodigoSolicitacao,
    string? Procedimento,
    string? Observacao);
